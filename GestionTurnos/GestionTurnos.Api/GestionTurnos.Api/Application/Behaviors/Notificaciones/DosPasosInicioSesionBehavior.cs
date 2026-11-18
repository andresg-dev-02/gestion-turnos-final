using System.Text.Json;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services.DosPasos;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Behaviors.Notificaciones;

/// <summary>
/// Segundo factor por correo sobre el inicio de sesión existente, sin modificar <see cref="IniciarSesionCommandHandler"/>.
///
/// 1. El handler valida contraseña, estado del usuario y del rol, y emite el JWT como siempre.
/// 2. Si el sistema tiene <see cref="DosPasosSettings.Disponible"/> y el usuario activó la opción, el JWT NO se entrega:
///    queda cifrado del lado del servidor junto al código, y la respuesta es 401 con code = Auth.SegundoFactorRequerido.
/// 3. POST auth/dos-pasos/verificar entrega el mismo LoginResponseDto solo si el código es correcto.
///
/// Con la opción desactivada (por defecto) el comportamiento del login no cambia en absoluto.
/// </summary>
public sealed class DosPasosInicioSesionBehavior
    : IPipelineBehavior<IniciarSesionCommand, Result<LoginResponseDto>>
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GestionTurnosContext _context;
    private readonly IServicioCodigosDosPasos _codigos;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public DosPasosInicioSesionBehavior(
        GestionTurnosContext context,
        IServicioCodigosDosPasos codigos,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _context = context;
        _codigos = codigos;
        _settings = settings;
    }

    public async Task<Result<LoginResponseDto>> Handle(
        IniciarSesionCommand request,
        RequestHandlerDelegate<Result<LoginResponseDto>> next,
        CancellationToken cancellationToken)
    {
        var result = await next();

        if (result.IsFailure || !_settings.CurrentValue.Disponible)
        {
            return result;
        }

        var login = result.Value;

        bool requiereSegundoFactor = await _context.Set<UsuarioDosPasos>()
            .AsNoTracking()
            .AnyAsync(x => x.IdUsuario == login.IdUsuario && x.Habilitado, cancellationToken);

        if (!requiereSegundoFactor)
        {
            return result;
        }

        var desafio = await _codigos.EmitirYEnviarAsync(
            PropositosCodigo.InicioSesion,
            ClaveInicioSesion(login.Correo),
            JsonSerializer.Serialize(login, JsonOptions),
            new DestinatarioCorreo(login.Correo, login.NombreCompleto),
            cancellationToken);

        // Si el correo no pudo encolarse se niega el acceso (fail-closed): nunca se entrega el token sin el segundo factor.
        return desafio is null
            ? Result.Failure<LoginResponseDto>(DosPasosErrors.CorreoNoEnviado)
            : Result.Failure<LoginResponseDto>(
                DosPasosErrors.SegundoFactorRequerido(desafio.CorreoEnmascarado, _settings.CurrentValue.MinutosVigencia));
    }

    /// <summary>
    /// Clave del desafío de inicio de sesión: el correo normalizado (el cliente lo conoce y no hay que exponer ids).
    /// </summary>
    internal static string ClaveInicioSesion(string correo) => correo.Trim().ToLowerInvariant();
}
