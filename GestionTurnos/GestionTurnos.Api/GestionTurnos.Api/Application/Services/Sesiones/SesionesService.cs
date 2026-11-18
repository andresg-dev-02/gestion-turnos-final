using System.Security.Cryptography;
using System.Text;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.Services.Sesiones;

/// <summary>Resultado de rotar un refresh token válido.</summary>
public sealed record SesionRotada(long IdUsuario, Guid Familia, string RefreshToken, DateTimeOffset RefreshExpiraEn);

/// <summary>
/// Refresh tokens de larga duración, revocables, con rotación en cada uso.
/// <list type="bullet">
/// <item>Solo se guarda el hash SHA-256: una copia de la base de datos no sirve para iniciar sesión.</item>
/// <item>"Recordar" = vigencia deslizante (Jwt:RefreshTokenExpirationDays); sin recordar, dura una jornada
///   (Jwt:RefreshTokenSesionHoras) desde el inicio de sesión.</item>
/// <item>Reutilizar un token ya rotado revoca toda la familia (indicio de robo). Se tolera una ventana corta
///   para dos pestañas que renuevan al mismo tiempo.</item>
/// </list>
/// </summary>
public sealed class SesionesService
{
    private static readonly TimeSpan VentanaRenovacionConcurrente = TimeSpan.FromSeconds(30);

    private readonly GestionTurnosContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _configuration;

    public SesionesService(GestionTurnosContext context, ICurrentUserService currentUser, IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;
        _configuration = configuration;
    }

    private int DiasRecordar => Math.Clamp(_configuration.GetValue("Jwt:RefreshTokenExpirationDays", 60), 1, 365);
    private int HorasSinRecordar => Math.Clamp(_configuration.GetValue("Jwt:RefreshTokenSesionHoras", 12), 1, 72);

    /// <summary>Crea la sesión del dispositivo tras un inicio de sesión exitoso y adjunta el refresh token.</summary>
    public async Task<LoginResponseDto> EmitirAsync(
        LoginResponseDto login,
        bool recordar,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var expira = recordar ? ahora.AddDays(DiasRecordar) : ahora.AddHours(HorasSinRecordar);
        var token = NuevoToken();

        _context.Set<SesionDispositivo>().Add(new SesionDispositivo
        {
            IdUsuario = login.IdUsuario,
            HashToken = Hash(token),
            Familia = Guid.NewGuid(),
            Recordar = recordar,
            CreadoEn = ahora,
            ExpiraEn = expira,
            UserAgent = Recortar(userAgent, 300),
            IpOrigen = Recortar(_currentUser.IpAddress, 64)
        });

        await _context.SaveChangesAsync(cancellationToken);

        return login with { RefreshToken = token, RefreshExpiraEn = expira };
    }

    /// <summary>Valida el refresh token y lo reemplaza por uno nuevo. Null si no es válido.</summary>
    public async Task<SesionRotada?> RotarAsync(string refreshToken, string? userAgent, CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var hash = Hash(refreshToken);

        var sesion = await _context.Set<SesionDispositivo>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.HashToken == hash, cancellationToken);

        if (sesion is null)
        {
            return null;
        }

        if (sesion.RevocadaEn is not null)
        {
            bool renovacionConcurrente = sesion.MotivoRevocacion == MotivosRevocacionSesion.Rotada &&
                                         ahora - sesion.RevocadaEn.Value < VentanaRenovacionConcurrente;
            if (!renovacionConcurrente)
            {
                await RevocarFamiliaAsync(sesion.Familia, MotivosRevocacionSesion.Reutilizada, cancellationToken);
            }

            return null;
        }

        if (sesion.ExpiraEn <= ahora)
        {
            return null;
        }

        // Revocación condicional: si dos solicitudes llegan a la vez, solo una gana la rotación.
        int revocadas = await _context.Set<SesionDispositivo>()
            .Where(s => s.IdSesion == sesion.IdSesion && s.RevocadaEn == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RevocadaEn, ahora)
                .SetProperty(s => s.MotivoRevocacion, MotivosRevocacionSesion.Rotada), cancellationToken);

        if (revocadas == 0)
        {
            return null;
        }

        var token = NuevoToken();
        var expira = sesion.Recordar ? ahora.AddDays(DiasRecordar) : sesion.ExpiraEn;

        _context.Set<SesionDispositivo>().Add(new SesionDispositivo
        {
            IdUsuario = sesion.IdUsuario,
            HashToken = Hash(token),
            Familia = sesion.Familia,
            Recordar = sesion.Recordar,
            CreadoEn = ahora,
            ExpiraEn = expira,
            UserAgent = Recortar(userAgent, 300) ?? sesion.UserAgent,
            IpOrigen = Recortar(_currentUser.IpAddress, 64)
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new SesionRotada(sesion.IdUsuario, sesion.Familia, token, expira);
    }

    /// <summary>Cierra la sesión del dispositivo que presenta el token (y sus rotaciones).</summary>
    public async Task CerrarAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var familia = await _context.Set<SesionDispositivo>()
            .AsNoTracking()
            .Where(s => s.HashToken == hash)
            .Select(s => (Guid?)s.Familia)
            .FirstOrDefaultAsync(cancellationToken);

        if (familia.HasValue)
        {
            await RevocarFamiliaAsync(familia.Value, MotivosRevocacionSesion.CierreSesion, cancellationToken);
        }
    }

    public Task RevocarFamiliaAsync(Guid familia, string motivo, CancellationToken cancellationToken) =>
        _context.Set<SesionDispositivo>()
            .Where(s => s.Familia == familia && s.RevocadaEn == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RevocadaEn, DateTimeOffset.UtcNow)
                .SetProperty(s => s.MotivoRevocacion, motivo), cancellationToken);

    /// <summary>Cierra todas las sesiones del usuario en todos sus dispositivos.</summary>
    public Task RevocarTodasAsync(long idUsuario, string motivo, CancellationToken cancellationToken) =>
        RevocarTodasAsync(_context, idUsuario, motivo, cancellationToken);

    /// <summary>Variante estática para handlers que ya tienen el contexto (participa en su transacción).</summary>
    public static Task RevocarTodasAsync(GestionTurnosContext context, long idUsuario, string motivo, CancellationToken cancellationToken) =>
        context.Set<SesionDispositivo>()
            .Where(s => s.IdUsuario == idUsuario && s.RevocadaEn == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RevocadaEn, DateTimeOffset.UtcNow)
                .SetProperty(s => s.MotivoRevocacion, motivo), cancellationToken);

    /// <summary>Arma la respuesta de sesión con datos frescos del usuario (rol, permisos, empleado activo).</summary>
    public static async Task<LoginResponseDto?> ConstruirSesionAsync(
        GestionTurnosContext context,
        IJwtTokenGenerator jwt,
        long idUsuario,
        CancellationToken cancellationToken)
    {
        var usuario = await context.Usuarios
            .AsNoTracking()
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancellationToken);

        if (usuario is null || usuario.CodEstadoUsuario != EstadosUsuario.Activo || !usuario.Rol.Activo)
        {
            return null;
        }

        var permisos = await context.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.IdRol == usuario.IdRol)
            .Select(rp => rp.Permiso.Codigo)
            .ToListAsync(cancellationToken);

        var empleado = await context.Empleados
            .AsNoTracking()
            .Where(e => e.IdUsuario == usuario.IdUsuario && e.CodEstadoEmpleado == EstadosEmpleado.Activo)
            .Select(e => new { e.IdEmpleado, NombreCompleto = e.Nombres + " " + e.Apellidos })
            .FirstOrDefaultAsync(cancellationToken);

        var (token, expiraEn) = jwt.GenerateToken(
            usuario.IdUsuario, usuario.Correo, usuario.Rol.Nombre, empleado?.IdEmpleado, empleado?.NombreCompleto, permisos);

        return new LoginResponseDto(
            AccessToken: token,
            ExpiraEn: expiraEn,
            IdUsuario: usuario.IdUsuario,
            Correo: usuario.Correo,
            Rol: usuario.Rol.Nombre,
            IdEmpleado: empleado?.IdEmpleado,
            NombreCompleto: empleado?.NombreCompleto,
            Permisos: permisos);
    }

    private static string NuevoToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string? Recortar(string? texto, int max) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= max ? texto : texto[..max];
}
