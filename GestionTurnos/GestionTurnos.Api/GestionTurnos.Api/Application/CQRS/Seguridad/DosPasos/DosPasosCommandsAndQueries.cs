using System.Text.Json;
using FluentValidation;
using GestionTurnos.Api.Application.Behaviors.Notificaciones;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Api.Application.Services.DosPasos;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.CQRS.Seguridad.DosPasos;

// Nota de diseño: el código viaja en propiedades llamadas "ClaveVerificacion" a propósito: LoggingBehavior
// enmascara toda propiedad cuyo nombre contenga "clave", así el código nunca queda en los logs.

public sealed record EstadoDosPasosDto(bool Disponible, bool Habilitado, DateTimeOffset? HabilitadoEn);

internal static class ValidacionClaveVerificacion
{
    public static IRuleBuilderOptions<T, string> EsCodigoNumerico<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El código de verificación es obligatorio.")
            .Matches("^[0-9]{6,8}$").WithMessage("El código de verificación debe tener entre 6 y 8 dígitos.");
}

// =====================================================================
// INICIO DE SESIÓN: VERIFICAR CÓDIGO (PÚBLICO)
// =====================================================================

public sealed record VerificarInicioSesionDosPasosCommand(string Correo, string ClaveVerificacion)
    : IRequest<Result<LoginResponseDto>>;

public sealed class VerificarInicioSesionDosPasosCommandValidator : AbstractValidator<VerificarInicioSesionDosPasosCommand>
{
    public VerificarInicioSesionDosPasosCommandValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.ClaveVerificacion).EsCodigoNumerico();
    }
}

public sealed class VerificarInicioSesionDosPasosCommandHandler
    : IRequestHandler<VerificarInicioSesionDosPasosCommand, Result<LoginResponseDto>>
{
    private readonly ICodigoVerificacionStore _store;
    private readonly GestionTurnosContext _context;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public VerificarInicioSesionDosPasosCommandHandler(
        ICodigoVerificacionStore store,
        GestionTurnosContext context,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _store = store;
        _context = context;
        _settings = settings;
    }

    public async Task<Result<LoginResponseDto>> Handle(
        VerificarInicioSesionDosPasosCommand request,
        CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.Disponible)
        {
            return Result.Failure<LoginResponseDto>(DosPasosErrors.NoDisponible);
        }

        var verificacion = await _store.VerificarAsync(
            PropositosCodigo.InicioSesion,
            DosPasosInicioSesionBehavior.ClaveInicioSesion(request.Correo),
            request.ClaveVerificacion,
            cancellationToken);

        var error = ErroresVerificacion.Traducir(verificacion);
        if (error is not null)
        {
            return Result.Failure<LoginResponseDto>(error);
        }

        var login = JsonSerializer.Deserialize<LoginResponseDto>(verificacion.Carga!, DosPasosInicioSesionBehavior.JsonOptions);
        if (login is null)
        {
            return Result.Failure<LoginResponseDto>(DosPasosErrors.CodigoExpirado);
        }

        // La cuenta pudo bloquearse durante los minutos de vigencia del código.
        bool sigueActivo = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.IdUsuario == login.IdUsuario && u.CodEstadoUsuario == EstadosUsuario.Activo, cancellationToken);

        return sigueActivo
            ? Result.Success(login)
            : Result.Failure<LoginResponseDto>(AuthErrors.CredencialesInvalidas);
    }
}

// =====================================================================
// INICIO DE SESIÓN: REENVIAR CÓDIGO (PÚBLICO)
// =====================================================================

/// <summary>
/// Respuesta siempre exitosa y genérica: no revela si el correo existe ni si hay un desafío pendiente.
/// </summary>
public sealed record ReenviarCodigoInicioSesionCommand(string Correo) : IRequest<Result>;

public sealed class ReenviarCodigoInicioSesionCommandValidator : AbstractValidator<ReenviarCodigoInicioSesionCommand>
{
    public ReenviarCodigoInicioSesionCommandValidator()
    {
        RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(150);
    }
}

public sealed class ReenviarCodigoInicioSesionCommandHandler : IRequestHandler<ReenviarCodigoInicioSesionCommand, Result>
{
    private readonly IServicioCodigosDosPasos _codigos;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public ReenviarCodigoInicioSesionCommandHandler(IServicioCodigosDosPasos codigos, IOptionsMonitor<DosPasosSettings> settings)
    {
        _codigos = codigos;
        _settings = settings;
    }

    public async Task<Result> Handle(ReenviarCodigoInicioSesionCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.Disponible)
        {
            return Result.Failure(DosPasosErrors.NoDisponible);
        }

        await _codigos.ReenviarAsync(
            PropositosCodigo.InicioSesion,
            DosPasosInicioSesionBehavior.ClaveInicioSesion(request.Correo),
            carga =>
            {
                var login = carga is null ? null : JsonSerializer.Deserialize<LoginResponseDto>(carga, DosPasosInicioSesionBehavior.JsonOptions);
                return login is null ? null : new DestinatarioCorreo(login.Correo, login.NombreCompleto);
            },
            cancellationToken);

        return Result.Success();
    }
}

// =====================================================================
// MI CUENTA: CONSULTAR / ACTIVAR / CONFIRMAR / DESACTIVAR
// =====================================================================

public sealed record ObtenerMiEstadoDosPasosQuery : IRequest<Result<EstadoDosPasosDto>>, IRequireAuthentication;

public sealed class ObtenerMiEstadoDosPasosQueryHandler : IRequestHandler<ObtenerMiEstadoDosPasosQuery, Result<EstadoDosPasosDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public ObtenerMiEstadoDosPasosQueryHandler(
        GestionTurnosContext context,
        ICurrentUserService currentUser,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _context = context;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<Result<EstadoDosPasosDto>> Handle(ObtenerMiEstadoDosPasosQuery request, CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.Disponible)
        {
            return Result.Success(new EstadoDosPasosDto(false, false, null));
        }

        var registro = await _context.Set<UsuarioDosPasos>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdUsuario == _currentUser.UserId!.Value, cancellationToken);

        return Result.Success(new EstadoDosPasosDto(true, registro?.Habilitado ?? false, registro?.Habilitado == true ? registro.HabilitadoEn : null));
    }
}

/// <summary>
/// Paso 1 de la activación: re-autenticación con la contraseña y envío de un código al correo de la cuenta.
/// Confirmar con un código evita que un correo mal escrito deje al usuario sin poder entrar.
/// </summary>
public sealed record SolicitarActivacionDosPasosCommand(string Contrasena)
    : IRequest<Result<DesafioDosPasosDto>>, IRequireAuthentication;

public sealed class SolicitarActivacionDosPasosCommandValidator : AbstractValidator<SolicitarActivacionDosPasosCommand>
{
    public SolicitarActivacionDosPasosCommandValidator()
    {
        RuleFor(x => x.Contrasena).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
    }
}

public sealed class SolicitarActivacionDosPasosCommandHandler
    : IRequestHandler<SolicitarActivacionDosPasosCommand, Result<DesafioDosPasosDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IServicioCodigosDosPasos _codigos;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public SolicitarActivacionDosPasosCommandHandler(
        GestionTurnosContext context,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IServicioCodigosDosPasos codigos,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _codigos = codigos;
        _settings = settings;
    }

    public async Task<Result<DesafioDosPasosDto>> Handle(SolicitarActivacionDosPasosCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.Disponible)
        {
            return Result.Failure<DesafioDosPasosDto>(DosPasosErrors.NoDisponible);
        }

        var idUsuario = _currentUser.UserId!.Value;
        var usuario = await CuentaDosPasos.ObtenerUsuarioAsync(_context, idUsuario, cancellationToken);
        if (usuario is null)
        {
            return Result.Failure<DesafioDosPasosDto>(AuthErrors.UsuarioNoEncontrado);
        }

        if (!_passwordHasher.Verify(request.Contrasena, usuario.HashContrasena))
        {
            return Result.Failure<DesafioDosPasosDto>(DosPasosErrors.ContrasenaIncorrecta);
        }

        bool yaHabilitado = await _context.Set<UsuarioDosPasos>()
            .AsNoTracking()
            .AnyAsync(x => x.IdUsuario == idUsuario && x.Habilitado, cancellationToken);
        if (yaHabilitado)
        {
            return Result.Failure<DesafioDosPasosDto>(DosPasosErrors.YaHabilitado);
        }

        var desafio = await _codigos.EmitirYEnviarAsync(
            PropositosCodigo.Activacion,
            idUsuario.ToString(),
            carga: null,
            new DestinatarioCorreo(usuario.Correo, usuario.Nombre),
            cancellationToken);

        return desafio is null
            ? Result.Failure<DesafioDosPasosDto>(DosPasosErrors.CorreoNoEnviado)
            : Result.Success(desafio);
    }
}

/// <summary>
/// Paso 2 de la activación: el código recibido confirma que el usuario controla el correo de su cuenta.
/// </summary>
public sealed record ConfirmarActivacionDosPasosCommand(string ClaveVerificacion) : IRequest<Result>, IRequireAuthentication;

public sealed class ConfirmarActivacionDosPasosCommandValidator : AbstractValidator<ConfirmarActivacionDosPasosCommand>
{
    public ConfirmarActivacionDosPasosCommandValidator()
    {
        RuleFor(x => x.ClaveVerificacion).EsCodigoNumerico();
    }
}

public sealed class ConfirmarActivacionDosPasosCommandHandler : IRequestHandler<ConfirmarActivacionDosPasosCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodigoVerificacionStore _store;
    private readonly INotificadorCorreo _notificador;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;

    public ConfirmarActivacionDosPasosCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ICodigoVerificacionStore store,
        INotificadorCorreo notificador,
        IOptionsMonitor<DosPasosSettings> settings)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _store = store;
        _notificador = notificador;
        _settings = settings;
    }

    public async Task<Result> Handle(ConfirmarActivacionDosPasosCommand request, CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.Disponible)
        {
            return Result.Failure(DosPasosErrors.NoDisponible);
        }

        var idUsuario = _currentUser.UserId!.Value;
        var verificacion = await _store.VerificarAsync(
            PropositosCodigo.Activacion, idUsuario.ToString(), request.ClaveVerificacion, cancellationToken);

        var error = ErroresVerificacion.Traducir(verificacion);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        try
        {
            await CuentaDosPasos.CambiarEstadoAsync(_context, idUsuario, habilitar: true, actualizadoPor: idUsuario, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }

        await CuentaDosPasos.AvisarAsync(
            _context, _notificador, idUsuario,
            "Verificación en dos pasos activada",
            "Desde ahora, cada vez que inicie sesión le enviaremos un código a este correo. Si usted no hizo este cambio, comuníquese de inmediato con el administrador del sistema.",
            CancellationToken.None);

        return Result.Success();
    }
}

public sealed record DesactivarMisDosPasosCommand(string Contrasena) : IRequest<Result>, IRequireAuthentication;

public sealed class DesactivarMisDosPasosCommandValidator : AbstractValidator<DesactivarMisDosPasosCommand>
{
    public DesactivarMisDosPasosCommandValidator()
    {
        RuleFor(x => x.Contrasena).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
    }
}

public sealed class DesactivarMisDosPasosCommandHandler : IRequestHandler<DesactivarMisDosPasosCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly INotificadorCorreo _notificador;

    public DesactivarMisDosPasosCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        INotificadorCorreo notificador)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _notificador = notificador;
    }

    public async Task<Result> Handle(DesactivarMisDosPasosCommand request, CancellationToken cancellationToken)
    {
        var idUsuario = _currentUser.UserId!.Value;
        var usuario = await CuentaDosPasos.ObtenerUsuarioAsync(_context, idUsuario, cancellationToken);
        if (usuario is null)
        {
            return Result.Failure(AuthErrors.UsuarioNoEncontrado);
        }

        if (!_passwordHasher.Verify(request.Contrasena, usuario.HashContrasena))
        {
            return Result.Failure(DosPasosErrors.ContrasenaIncorrecta);
        }

        try
        {
            if (!await CuentaDosPasos.CambiarEstadoAsync(_context, idUsuario, habilitar: false, actualizadoPor: idUsuario, cancellationToken))
            {
                return Result.Failure(DosPasosErrors.NoHabilitado);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }

        await CuentaDosPasos.AvisarAsync(
            _context, _notificador, idUsuario,
            "Verificación en dos pasos desactivada",
            "Su cuenta ya no pedirá un código al iniciar sesión. Si usted no hizo este cambio, cambie su contraseña y comuníquese con el administrador del sistema.",
            CancellationToken.None);

        return Result.Success();
    }
}

// =====================================================================
// ADMINISTRACIÓN: RESTABLECER (permiso usuarios.gestionar)
// Camino de recuperación cuando el usuario perdió el acceso a su correo.
// =====================================================================

[RequirePermission(PermisosSistema.UsuariosGestionar)]
public sealed record RestablecerDosPasosUsuarioCommand(long IdUsuario) : IRequest<Result>;

public sealed class RestablecerDosPasosUsuarioCommandValidator : AbstractValidator<RestablecerDosPasosUsuarioCommand>
{
    public RestablecerDosPasosUsuarioCommandValidator()
    {
        RuleFor(x => x.IdUsuario).GreaterThan(0);
    }
}

public sealed class RestablecerDosPasosUsuarioCommandHandler : IRequestHandler<RestablecerDosPasosUsuarioCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodigoVerificacionStore _store;
    private readonly INotificadorCorreo _notificador;

    public RestablecerDosPasosUsuarioCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ICodigoVerificacionStore store,
        INotificadorCorreo notificador)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _store = store;
        _notificador = notificador;
    }

    public async Task<Result> Handle(RestablecerDosPasosUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await CuentaDosPasos.ObtenerUsuarioAsync(_context, request.IdUsuario, cancellationToken);
        if (usuario is null)
        {
            return Result.Failure(AuthErrors.UsuarioNoEncontrado);
        }

        try
        {
            if (!await CuentaDosPasos.CambiarEstadoAsync(_context, request.IdUsuario, habilitar: false, actualizadoPor: _currentUser.UserId, cancellationToken))
            {
                return Result.Failure(DosPasosErrors.NoHabilitado);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }

        // Invalida cualquier código de inicio de sesión pendiente del usuario.
        await _store.DescartarAsync(PropositosCodigo.InicioSesion, DosPasosInicioSesionBehavior.ClaveInicioSesion(usuario.Correo), CancellationToken.None);

        await CuentaDosPasos.AvisarAsync(
            _context, _notificador, request.IdUsuario,
            "Verificación en dos pasos restablecida",
            "Un administrador desactivó la verificación en dos pasos de su cuenta. Puede volver a activarla desde su perfil. Si no lo solicitó, comuníquese con el administrador del sistema.",
            CancellationToken.None);

        return Result.Success();
    }
}

// =====================================================================
// APOYO COMÚN
// =====================================================================

internal static class ErroresVerificacion
{
    public static Error? Traducir(VerificacionCodigo verificacion) => verificacion.Estado switch
    {
        EstadoVerificacionCodigo.Valido => null,
        EstadoVerificacionCodigo.Invalido => DosPasosErrors.CodigoInvalido(verificacion.IntentosRestantes),
        EstadoVerificacionCodigo.IntentosAgotados => DosPasosErrors.IntentosAgotados,
        _ => DosPasosErrors.CodigoExpirado
    };
}

internal static class CuentaDosPasos
{
    internal sealed record DatosUsuario(string Correo, string HashContrasena, string? Nombre);

    public static Task<DatosUsuario?> ObtenerUsuarioAsync(GestionTurnosContext context, long idUsuario, CancellationToken cancellationToken) =>
        context.Usuarios
            .AsNoTracking()
            .Where(u => u.IdUsuario == idUsuario)
            .Select(u => new DatosUsuario(
                u.Correo,
                u.HashContrasena,
                u.Empleado != null ? u.Empleado.Nombres + " " + u.Empleado.Apellidos : null))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Inserta o actualiza la preferencia (sin borrar filas). Devuelve <c>false</c> si ya estaba en el estado pedido.
    /// </summary>
    public static async Task<bool> CambiarEstadoAsync(
        GestionTurnosContext context, long idUsuario, bool habilitar, long? actualizadoPor, CancellationToken cancellationToken)
    {
        var registro = await context.Set<UsuarioDosPasos>()
            .FirstOrDefaultAsync(x => x.IdUsuario == idUsuario, cancellationToken);

        if (registro is null)
        {
            if (!habilitar)
            {
                return false;
            }

            registro = new UsuarioDosPasos { IdUsuario = idUsuario };
            await context.Set<UsuarioDosPasos>().AddAsync(registro, cancellationToken);
        }
        else if (registro.Habilitado == habilitar)
        {
            return false;
        }

        var ahora = DateTimeOffset.UtcNow;
        registro.Habilitado = habilitar;
        registro.HabilitadoEn = habilitar ? ahora : registro.HabilitadoEn;
        registro.ActualizadoEn = ahora;
        registro.ActualizadoPor = actualizadoPor;
        return true;
    }

    public static async Task AvisarAsync(
        GestionTurnosContext context, INotificadorCorreo notificador, long idUsuario, string titulo, string mensaje, CancellationToken cancellationToken)
    {
        var usuario = await ObtenerUsuarioAsync(context, idUsuario, cancellationToken);
        if (usuario is null)
        {
            return;
        }

        await notificador.EncolarAsync(
            PlantillasCorreo.AvisoSeguridad,
            new DestinatarioCorreo(usuario.Correo, usuario.Nombre),
            titulo,
            new AvisoSeguridadModelo(usuario.Nombre ?? usuario.Correo, titulo, mensaje),
            cancellationToken);
    }
}
