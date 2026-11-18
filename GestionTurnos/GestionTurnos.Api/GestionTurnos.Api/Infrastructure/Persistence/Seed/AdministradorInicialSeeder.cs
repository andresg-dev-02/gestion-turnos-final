using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence.Seed;

/// <summary>
/// Credenciales del primer administrador. Se leen de configuración (user-secrets o variables de entorno
/// <c>AdministradorInicial__Correo</c> / <c>AdministradorInicial__Contrasena</c>); nunca se escriben en el repositorio.
/// </summary>
public sealed class AdministradorInicialSettings
{
    public const string SectionName = "AdministradorInicial";

    public string? Correo { get; set; }
    public string? Contrasena { get; set; }
}

/// <summary>
/// Datos iniciales (Sección 2): crea el primer usuario activo con rol Administrador.
/// Se ejecuta solo desde la línea de comandos (<c>dotnet run -- seed-admin</c>); no existe endpoint público.
/// Es idempotente: si ya hay un administrador activo no hace cambios.
/// </summary>
public static class AdministradorInicialSeeder
{
    public const string ComandoCli = "seed-admin";

    /// <summary>Nombre del rol creado por gestion_turnos.sql. Solo se usa para ubicarlo al sembrar datos, nunca para autorizar.</summary>
    private const string NombreRolAdministrador = "Administrador";

    private const int LongitudMinimaContrasena = 12;

    /// <returns>Código de salida del proceso: 0 si quedó un administrador activo, 1 si hubo un error.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdministradorInicialSeeder));
        var settings = sp.GetRequiredService<IConfiguration>()
            .GetSection(AdministradorInicialSettings.SectionName)
            .Get<AdministradorInicialSettings>() ?? new AdministradorInicialSettings();

        var correo = settings.Correo?.Trim().ToLowerInvariant();
        var contrasena = settings.Contrasena;

        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@') || correo.Length > 150)
        {
            logger.LogError("Configure un correo válido en AdministradorInicial:Correo.");
            return 1;
        }

        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena)
        {
            logger.LogError(
                "Configure en AdministradorInicial:Contrasena una contraseña de al menos {Minimo} caracteres.",
                LongitudMinimaContrasena);
            return 1;
        }

        var context = sp.GetRequiredService<GestionTurnosContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        var rol = await context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Nombre == NombreRolAdministrador, cancellationToken);

        if (rol is null)
        {
            logger.LogError("No existe el rol {Rol}. Ejecute primero sql/gestion_turnos.sql.", NombreRolAdministrador);
            return 1;
        }

        // El rol debe tener ya los 13 permisos; sin ellos el administrador recibiría 403 en todo.
        var permisosRol = await context.RolesPermisos
            .AsNoTracking()
            .Where(rp => rp.IdRol == rol.IdRol)
            .Select(rp => rp.Permiso.Codigo)
            .ToListAsync(cancellationToken);

        var faltantes = PermisosSistema.Todos
            .Except(permisosRol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (faltantes.Count > 0)
        {
            logger.LogError(
                "El rol {Rol} no tiene los permisos: {Faltantes}. Ejecute primero sql/seed_permisos_administrador.sql.",
                NombreRolAdministrador,
                string.Join(", ", faltantes));
            return 1;
        }

        var existente = await context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == correo, cancellationToken);

        if (existente is not null)
        {
            if (existente.IdRol == rol.IdRol && existente.CodEstadoUsuario == EstadosUsuario.Activo)
            {
                logger.LogInformation("El administrador {Correo} ya existe y está activo. No se hicieron cambios.", correo);
                return 0;
            }

            logger.LogError(
                "Ya existe un usuario con el correo {Correo} pero no es un administrador activo. No se modificó; revíselo manualmente.",
                correo);
            return 1;
        }

        var hayAdministradorActivo = await context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.IdRol == rol.IdRol && u.CodEstadoUsuario == EstadosUsuario.Activo, cancellationToken);

        if (hayAdministradorActivo)
        {
            logger.LogInformation("Ya existe un administrador activo. No se creó otro.");
            return 0;
        }

        context.Usuarios.Add(new Usuario
        {
            Correo = correo,
            HashContrasena = hasher.Hash(contrasena),
            IdRol = rol.IdRol,
            CodEstadoUsuario = EstadosUsuario.Activo,
            CreadoEn = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Administrador inicial {Correo} creado.", correo);
        return 0;
    }
}
