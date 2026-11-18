using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Asistencia;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services.Asistencia;
using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Asistencia;

// =====================================================================
// 3.6 REGISTRAR ASISTENCIA DEL EMPLEADO (CU-02)
// =====================================================================

/// <summary>
/// Sección 3.6: Entrada.
/// - La hora siempre es now() del servidor; no acepta la hora del cliente.
/// - Busca el turno que corresponde con ventana previa de 60 minutos.
/// - No permite entrada si el empleado ya tiene un registro con hora_salida IS NULL.
/// </summary>
public sealed record RegistrarMiEntradaCommand(
    string CodFuenteMarcacion = FuentesMarcacionCodigos.Web) : IRequest<Result<long>>, IRequireAuthentication;

public sealed class RegistrarMiEntradaCommandValidator : AbstractValidator<RegistrarMiEntradaCommand>
{
    public RegistrarMiEntradaCommandValidator()
    {
        RuleFor(x => x.CodFuenteMarcacion)
            .NotEmpty()
            .Must(f => f.Equals(FuentesMarcacionCodigos.Web, StringComparison.OrdinalIgnoreCase) ||
                       f.Equals(FuentesMarcacionCodigos.Movil, StringComparison.OrdinalIgnoreCase))
            .WithMessage("La fuente de marcación del empleado debe ser 'WEB' o 'MOVIL'.");
    }
}

public sealed class RegistrarMiEntradaCommandHandler : IRequestHandler<RegistrarMiEntradaCommand, Result<long>>
{
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly MarcacionEmpleadoService _marcacion;

    public RegistrarMiEntradaCommandHandler(IEmpleadoSesionService empleadoSesionService, MarcacionEmpleadoService marcacion)
    {
        _empleadoSesionService = empleadoSesionService;
        _marcacion = marcacion;
    }

    public async Task<Result<long>> Handle(RegistrarMiEntradaCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        // Reglas comunes (jornada vencida, bloqueo de concurrencia, turno vigente): MarcacionEmpleadoService.
        var resultado = await _marcacion.MarcarAsync(
            idEmpleadoResult.Value, AccionMarcacion.Entrada, request.CodFuenteMarcacion, null, cancellationToken);

        return resultado.IsSuccess
            ? Result.Success(resultado.Value.IdRegistro)
            : Result.Failure<long>(resultado.Error);
    }
}

/// <summary>
/// Sección 3.6: Salida.
/// Actualiza hora_salida = now() donde id_empleado = $1 AND hora_salida IS NULL,
/// y en la misma transacción ejecuta la liquidación de horas (Sección 6.3).
/// </summary>
public sealed record RegistrarMiSalidaCommand : IRequest<Result<long>>, IRequireAuthentication;

public sealed class RegistrarMiSalidaCommandHandler : IRequestHandler<RegistrarMiSalidaCommand, Result<long>>
{
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly MarcacionEmpleadoService _marcacion;

    public RegistrarMiSalidaCommandHandler(IEmpleadoSesionService empleadoSesionService, MarcacionEmpleadoService marcacion)
    {
        _empleadoSesionService = empleadoSesionService;
        _marcacion = marcacion;
    }

    public async Task<Result<long>> Handle(RegistrarMiSalidaCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        // Cierra la jornada y liquida horas en la misma transacción (Sección 6.3).
        var resultado = await _marcacion.MarcarAsync(
            idEmpleadoResult.Value, AccionMarcacion.Salida, FuentesMarcacionCodigos.Web, null, cancellationToken);

        return resultado.IsSuccess
            ? Result.Success(resultado.Value.IdRegistro)
            : Result.Failure<long>(resultado.Error);
    }
}

// =====================================================================
// MARCAJE POR QR ROTATIVO / TOTEM DE ENTRADA (ESTILO TOTP AUTHENTICATOR)
// =====================================================================

public sealed record ObtenerTokenQrEstacionQuery(string? CodigoEstacion = null) : IRequest<Result<TokenQrEstacionDto>>;

public sealed class ObtenerTokenQrEstacionQueryHandler : IRequestHandler<ObtenerTokenQrEstacionQuery, Result<TokenQrEstacionDto>>
{
    private readonly IQrEstacionService _qrEstacionService;

    public ObtenerTokenQrEstacionQueryHandler(IQrEstacionService qrEstacionService)
    {
        _qrEstacionService = qrEstacionService;
    }

    public Task<Result<TokenQrEstacionDto>> Handle(ObtenerTokenQrEstacionQuery request, CancellationToken cancellationToken)
    {
        var tokenDto = _qrEstacionService.GenerarTokenActual(request.CodigoEstacion);
        return Task.FromResult(Result.Success(tokenDto));
    }
}

public sealed record MarcarAsistenciaPorQrCommand(
    string TokenQr,
    string Accion = "AUTO",
    double? Latitud = null,
    double? Longitud = null) : IRequest<Result<ResultadoMarcacionQrDto>>, IRequireAuthentication;

public sealed class MarcarAsistenciaPorQrCommandValidator : AbstractValidator<MarcarAsistenciaPorQrCommand>
{
    public MarcarAsistenciaPorQrCommandValidator()
    {
        RuleFor(x => x.TokenQr)
            .NotEmpty()
            .WithMessage("El código QR de la estación es obligatorio.");

        RuleFor(x => x.Accion)
            .NotEmpty()
            .Must(a => a.Equals("AUTO", StringComparison.OrdinalIgnoreCase) ||
                       a.Equals("ENTRADA", StringComparison.OrdinalIgnoreCase) ||
                       a.Equals("SALIDA", StringComparison.OrdinalIgnoreCase))
            .WithMessage("La acción debe ser 'AUTO', 'ENTRADA' o 'SALIDA'.");
    }
}

public sealed class MarcarAsistenciaPorQrCommandHandler : IRequestHandler<MarcarAsistenciaPorQrCommand, Result<ResultadoMarcacionQrDto>>
{
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IQrEstacionService _qrEstacionService;
    private readonly MarcacionEmpleadoService _marcacion;

    public MarcarAsistenciaPorQrCommandHandler(
        IEmpleadoSesionService empleadoSesionService,
        IQrEstacionService qrEstacionService,
        MarcacionEmpleadoService marcacion)
    {
        _empleadoSesionService = empleadoSesionService;
        _qrEstacionService = qrEstacionService;
        _marcacion = marcacion;
    }

    public async Task<Result<ResultadoMarcacionQrDto>> Handle(MarcarAsistenciaPorQrCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<ResultadoMarcacionQrDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        // 1. Validar el token rotativo con la estación y tolerancias de tiempo
        var validacion = _qrEstacionService.ValidarToken(request.TokenQr, idEmpleado);
        if (!validacion.EsValido)
        {
            return Result.Failure<ResultadoMarcacionQrDto>(new Error(
                "Asistencia.QrInvalido",
                validacion.MensajeError ?? "El código QR es inválido o ha expirado. Por favor enfoca el QR actual en pantalla.",
                ErrorType.Validation));
        }

        // 2. AUTO: entrada si no hay jornada abierta, salida si la hay (con las reglas de MarcacionEmpleadoService).
        var accion = request.Accion.Trim().ToUpperInvariant() switch
        {
            "ENTRADA" => AccionMarcacion.Entrada,
            "SALIDA" => AccionMarcacion.Salida,
            _ => AccionMarcacion.Auto
        };

        // La ubicación (si el teléfono la entregó) queda en la observación para auditoría.
        string ubicacion = request.Latitud.HasValue && request.Longitud.HasValue
            ? FormattableString.Invariant($" @ {request.Latitud.Value:F5},{request.Longitud.Value:F5}")
            : string.Empty;

        // La etiqueta Entrada/Salida se decide después; se usa "QR" para no adivinar el tipo.
        var resultado = await _marcacion.MarcarAsync(
            idEmpleado,
            accion,
            FuentesMarcacionCodigos.Movil,
            $"QR {validacion.CodigoEstacion}{ubicacion}",
            cancellationToken);

        if (resultado.IsFailure)
        {
            return Result.Failure<ResultadoMarcacionQrDto>(resultado.Error);
        }

        var r = resultado.Value;
        var mensaje = r.TipoAccion == MarcacionEmpleadoService.TipoSalida
            ? $"¡Salida registrada con éxito a las {r.HoraLocal}!"
            : $"¡Entrada registrada con éxito a las {r.HoraLocal}!";

        return Result.Success(new ResultadoMarcacionQrDto(
            r.IdRegistro,
            r.TipoAccion,
            r.Aviso is null ? mensaje : $"{mensaje} {r.Aviso}",
            r.Hora,
            validacion.CodigoEstacion));
    }
}

// =====================================================================
// 3.6 REGISTRO O CORRECCIÓN MANUAL (ADMINISTRADOR - permiso: asistencia.registrar_manual)
// =====================================================================

/// <summary>
/// Sección 3.6: Registro o corrección manual por el administrador.
/// cod_fuente_marcacion = 'ADMIN', registrado_por = admin y observacion obligatoria. Reliquida después.
/// </summary>
[RequirePermission(PermisosSistema.AsistenciaRegistrarManual)]
public sealed record RegistrarOCorregirAsistenciaManualCommand(
    long? IdRegistro,
    long IdEmpleado,
    long? IdAsignacion,
    DateTimeOffset HoraEntrada,
    DateTimeOffset? HoraSalida,
    string Observacion) : IRequest<Result<long>>;

public sealed class RegistrarOCorregirAsistenciaManualCommandValidator
    : AbstractValidator<RegistrarOCorregirAsistenciaManualCommand>
{
    public RegistrarOCorregirAsistenciaManualCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.HoraEntrada).NotEmpty();
        RuleFor(x => x.HoraSalida)
            .GreaterThan(x => x.HoraEntrada)
            .WithMessage("La hora de salida debe ser posterior a la hora de entrada.")
            .When(x => x.HoraSalida.HasValue);

        RuleFor(x => x.Observacion)
            .NotEmpty().WithMessage("La observación es obligatoria para el registro o corrección manual de asistencia.")
            .MaximumLength(300);
    }
}

public sealed class RegistrarOCorregirAsistenciaManualCommandHandler
    : IRequestHandler<RegistrarOCorregirAsistenciaManualCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;

    public RegistrarOCorregirAsistenciaManualCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILiquidacionHorasCalculator liquidacionCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _liquidacionCalculator = liquidacionCalculator;
    }

    public async Task<Result<long>> Handle(
        RegistrarOCorregirAsistenciaManualCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        bool esCorreccion = request.IdRegistro.HasValue && request.IdRegistro.Value > 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloquea el empleado: dos marcaciones simultáneas no pueden pasar ambas la revisión de cruces.
            var empleado = await _context.BloquearPorIdAsync<Empleado>(request.IdEmpleado, cancellationToken);
            if (empleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Fallo("Asistencia.EmpleadoNoEncontrado", "El empleado no existe.", ErrorType.NotFound);
            }

            if (request.IdAsignacion.HasValue)
            {
                bool asignacionDeEmpleado = await _context.Asignaciones
                    .AsNoTracking()
                    .AnyAsync(a => a.IdAsignacion == request.IdAsignacion.Value &&
                                   a.IdEmpleado == request.IdEmpleado &&
                                   a.CodEstadoAsignacion != EstadosAsignacion.Anulada, cancellationToken);

                if (!asignacionDeEmpleado)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Fallo("Asistencia.AsignacionInvalida",
                        "La asignación seleccionada no pertenece al empleado o se encuentra anulada.", ErrorType.Failure);
                }

                // Una asignación tiene a lo sumo un registro (UNIQUE id_asignacion).
                bool asignacionUsada = await _context.RegistrosAsistencia
                    .AsNoTracking()
                    .AnyAsync(r => r.IdAsignacion == request.IdAsignacion.Value &&
                                   (!esCorreccion || r.IdRegistro != request.IdRegistro!.Value), cancellationToken);
                if (asignacionUsada)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Fallo("Asistencia.EntradaYaRegistrada",
                        "Esa asignación ya tiene otro registro de asistencia; corrija ese registro en lugar de crear uno nuevo.", ErrorType.Conflict);
                }
            }

            // Sin cruces con otras marcaciones del empleado, y a lo sumo un registro abierto.
            var otros = _context.RegistrosAsistencia
                .AsNoTracking()
                .Where(r => r.IdEmpleado == request.IdEmpleado &&
                            (!esCorreccion || r.IdRegistro != request.IdRegistro!.Value))
                // Un registro con "salida no registrada" no tiene fin conocido: no bloquea marcaciones posteriores.
                .Where(r => (r.HoraSalida == null && !r.SalidaNoRegistrada) || r.HoraSalida > request.HoraEntrada);

            // Sin salida, el registro queda abierto hacia adelante: cualquier otro que termine después de la entrada se cruza.
            if (request.HoraSalida.HasValue)
            {
                var salida = request.HoraSalida.Value;
                otros = otros.Where(r => r.HoraEntrada < salida);
            }

            bool seCruza = await otros.AnyAsync(cancellationToken);
            if (seCruza)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Fallo("Asistencia.CruceMarcaciones",
                    "La marcación se cruza con otro registro de asistencia del empleado (o el empleado ya tiene una entrada abierta).",
                    ErrorType.Conflict);
            }

            RegistroAsistencia registro;
            if (esCorreccion)
            {
                var existente = await _context.BloquearPorIdAsync<RegistroAsistencia>(request.IdRegistro!.Value, cancellationToken);

                if (existente is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Fallo("Asistencia.RegistroNoEncontrado", "El registro de asistencia no existe.", ErrorType.NotFound);
                }

                // Corregir una marcación no la traslada a otro empleado.
                if (existente.IdEmpleado != request.IdEmpleado)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Fallo("Asistencia.EmpleadoNoCoincide",
                        "El registro pertenece a otro empleado; una corrección no cambia el empleado de la marcación.", ErrorType.Conflict);
                }

                // El disparador tg_aud_registro deja en la bitácora el valor anterior y el nuevo.
                existente.IdAsignacion = request.IdAsignacion;
                existente.HoraEntrada = request.HoraEntrada;
                existente.HoraSalida = request.HoraSalida;
                existente.SalidaNoRegistrada = existente.SalidaNoRegistrada && !request.HoraSalida.HasValue;
                existente.CodFuenteMarcacion = FuentesMarcacionCodigos.Admin;
                existente.Observacion = request.Observacion.Trim();
                existente.RegistradoPor = adminId;
                registro = existente;
            }
            else
            {
                registro = new RegistroAsistencia
                {
                    IdEmpleado = request.IdEmpleado,
                    IdAsignacion = request.IdAsignacion,
                    HoraEntrada = request.HoraEntrada,
                    HoraSalida = request.HoraSalida,
                    CodFuenteMarcacion = FuentesMarcacionCodigos.Admin,
                    Observacion = request.Observacion.Trim(),
                    RegistradoPor = adminId,
                    CreadoEn = DateTimeOffset.UtcNow
                };
                await _context.RegistrosAsistencia.AddAsync(registro, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reliquidar en la misma transacción (sección 6.3). Sin salida, la liquidación limpia el detalle anterior.
            await _liquidacionCalculator.LiquidarRegistroAsync(registro.IdRegistro, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success(registro.IdRegistro);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }

    private static Result<long> Fallo(string codigo, string mensaje, ErrorType tipo) =>
        Result.Failure<long>(new Error(codigo, mensaje, tipo));
}

// =====================================================================
// 3.4 CONSULTAS DE ASISTENCIA Y HORAS DEL EMPLEADO (RF-05, CU-05)
// =====================================================================

public sealed record ObtenerMisHorasPorTurnoQuery(
    DateOnly Desde,
    DateOnly Hasta) : IRequest<Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMisHorasPorTurnoQueryHandler
    : IRequestHandler<ObtenerMisHorasPorTurnoQuery, Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisHorasPorTurnoQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<HorasPorTurnoEmpleadoDto>>> Handle(
        ObtenerMisHorasPorTurnoQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HorasPorTurnoEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await (from v in _context.VistaHorasPorTurno.AsNoTracking()
                           join t in _context.Turnos.AsNoTracking() on v.IdTurno equals t.IdTurno
                           join a in _context.Asignaciones.AsNoTracking() on v.IdAsignacion equals a.IdAsignacion
                           where v.IdEmpleado == idEmpleado &&
                                 v.Fecha >= request.Desde &&
                                 v.Fecha <= request.Hasta
                           orderby v.Fecha descending
                           select new
                           {
                               v.IdAsignacion,
                               v.Fecha,
                               v.IdTurno,
                               NombreTurno = t.Nombre,
                               v.MinutosProgramados,
                               v.MinutosTrabajados,
                               v.HoraEntrada,
                               v.HoraSalida,
                               v.MinutosTarde,
                               v.SinAsistencia,
                               PeriodoInicio = a.Periodo.Inicio,
                               PeriodoFin = a.Periodo.Fin
                           })
                          .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result.Success<IReadOnlyList<HorasPorTurnoEmpleadoDto>>([]);
        }

        var desdeUtc = items.Min(x => x.PeriodoInicio);
        var hastaUtc = items.Max(x => x.PeriodoFin);

        var novedades = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdEmpleado == idEmpleado &&
                        n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                        n.Inicio < hastaUtc && n.Fin > desdeUtc)
            .Select(n => new { n.Inicio, n.Fin, Nombre = n.TipoNovedad.Nombre })
            .ToListAsync(cancellationToken);

        var vacaciones = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(sv => sv.IdEmpleado == idEmpleado &&
                         sv.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                         sv.FechaInicio <= request.Hasta && sv.FechaFin >= request.Desde)
            .Select(sv => new { sv.FechaInicio, sv.FechaFin })
            .ToListAsync(cancellationToken);

        var resultado = items.Select(x =>
        {
            string? novedadQueExplica = novedades
                .Where(n => n.Inicio < x.PeriodoFin && n.Fin > x.PeriodoInicio)
                .Select(n => n.Nombre)
                .FirstOrDefault();

            if (novedadQueExplica == null && vacaciones.Any(v => v.FechaInicio <= x.Fecha && v.FechaFin >= x.Fecha))
            {
                novedadQueExplica = "Vacaciones";
            }

            bool esJustificado = !string.IsNullOrEmpty(novedadQueExplica);
            bool sinAsistenciaEfectiva = x.SinAsistencia && !esJustificado;
            int diferenciaMinutos = (esJustificado && x.MinutosTrabajados == 0)
                ? 0
                : (x.MinutosTrabajados - x.MinutosProgramados);

            return new HorasPorTurnoEmpleadoDto(
                x.IdAsignacion,
                x.Fecha,
                x.IdTurno,
                x.NombreTurno,
                x.MinutosProgramados,
                x.MinutosTrabajados,
                diferenciaMinutos,
                x.HoraEntrada,
                x.HoraSalida,
                x.MinutosTarde,
                sinAsistenciaEfectiva,
                novedadQueExplica,
                esJustificado);
        }).ToList();

        return Result.Success<IReadOnlyList<HorasPorTurnoEmpleadoDto>>(resultado);
    }
}

/// <summary>
/// Sección 3.4: Consolidado de asistencia y horas con $grano = 'day', 'week', 'month' o 'year'
/// (validado contra esa lista blanca estricta).
/// </summary>
public sealed record ObtenerMiConsolidadoHorasQuery(
    DateOnly Desde,
    DateOnly Hasta,
    string Grano = "week") : IRequest<Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMiConsolidadoHorasQueryValidator : AbstractValidator<ObtenerMiConsolidadoHorasQuery>
{
    private static readonly HashSet<string> GranosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "day", "week", "month", "year"
    };

    public ObtenerMiConsolidadoHorasQueryValidator()
    {
        RuleFor(x => x.Hasta).GreaterThanOrEqualTo(x => x.Desde);
        RuleFor(x => x.Grano)
            .NotEmpty()
            .Must(g => GranosPermitidos.Contains(g.Trim()))
            .WithMessage("El grano de agrupación debe ser 'day', 'week', 'month' o 'year'.");
    }
}

public sealed class ObtenerMiConsolidadoHorasQueryHandler
    : IRequestHandler<ObtenerMiConsolidadoHorasQuery, Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiConsolidadoHorasQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>> Handle(
        ObtenerMiConsolidadoHorasQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var grano = request.Grano.Trim().ToLowerInvariant();

        var filas = await _context.VistaHorasPorTurno
            .AsNoTracking()
            .Where(v => v.IdEmpleado == idEmpleado &&
                        v.Fecha >= request.Desde &&
                        v.Fecha <= request.Hasta)
            .ToListAsync(cancellationToken);

        var consolidado = filas
            .GroupBy(v => TruncarFechaPorGrano(v.Fecha, grano))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int programados = g.Sum(x => x.MinutosProgramados);
                int trabajados = g.Sum(x => x.MinutosTrabajados);
                int sinAsistencia = g.Count(x => x.SinAsistencia);
                return new ConsolidadoHorasEmpleadoDto(
                    Periodo: g.Key,
                    MinutosProgramados: programados,
                    MinutosTrabajados: trabajados,
                    Diferencia: trabajados - programados,
                    TurnosSinAsistencia: sinAsistencia);
            })
            .ToList();

        return Result.Success<IReadOnlyList<ConsolidadoHorasEmpleadoDto>>(consolidado);
    }

    private static DateOnly TruncarFechaPorGrano(DateOnly fecha, string grano)
    {
        return grano switch
        {
            "day" => fecha,
            "week" => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7)), // Lunes de la semana (equivalente a date_trunc('week', fecha))
            "month" => new DateOnly(fecha.Year, fecha.Month, 1),
            "year" => new DateOnly(fecha.Year, 1, 1),
            _ => fecha
        };
    }
}

/// <summary>
/// Sección 3.4: Horas por categoría (ordinaria, extra, recargos):
/// suma asistencia.detalle_horas unida a asistencia.registro y parametrizacion.categoria_hora.
/// </summary>
public sealed record ObtenerMisHorasPorCategoriaQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta) : IRequest<Result<IReadOnlyList<HorasPorCategoriaDto>>>, IRequireAuthentication;

public sealed class ObtenerMisHorasPorCategoriaQueryHandler
    : IRequestHandler<ObtenerMisHorasPorCategoriaQuery, Result<IReadOnlyList<HorasPorCategoriaDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisHorasPorCategoriaQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<HorasPorCategoriaDto>>> Handle(
        ObtenerMisHorasPorCategoriaQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HorasPorCategoriaDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var agrupado = await (from dh in _context.DetallesHoras.AsNoTracking()
                              join r in _context.RegistrosAsistencia.AsNoTracking() on dh.IdRegistro equals r.IdRegistro
                              join ch in _context.CategoriasHora.AsNoTracking() on dh.CodCategoriaHora equals ch.CodCategoriaHora
                              where r.IdEmpleado == idEmpleado &&
                                    r.HoraEntrada >= request.Desde &&
                                    r.HoraEntrada <= request.Hasta
                              group dh by new { ch.CodCategoriaHora, ch.Nombre, ch.FactorPago } into g
                              select new
                              {
                                  g.Key.CodCategoriaHora,
                                  g.Key.Nombre,
                                  g.Key.FactorPago,
                                  TotalMinutos = g.Sum(x => x.Minutos)
                              })
                             .ToListAsync(cancellationToken);

        var resultado = agrupado
            .OrderBy(x => x.CodCategoriaHora)
            .Select(x => new HorasPorCategoriaDto(
                x.CodCategoriaHora,
                x.Nombre,
                x.FactorPago,
                x.TotalMinutos,
                Math.Round(x.TotalMinutos / 60m, 2)))
            .ToList();

        return Result.Success<IReadOnlyList<HorasPorCategoriaDto>>(resultado);
    }
}

// =====================================================================
// 5. CONSULTA ADMINISTRATIVA DE ASISTENCIA (permiso: asistencia.consultar)
// =====================================================================

[RequirePermission(PermisosSistema.AsistenciaConsultar)]
public sealed record ListarRegistrosAsistenciaAdminQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    string? CodFuenteMarcacion = null,
    bool? SoloAbiertos = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<RegistroAsistenciaDto>>>;

public sealed class ListarRegistrosAsistenciaAdminQueryHandler
    : IRequestHandler<ListarRegistrosAsistenciaAdminQuery, Result<PagedResult<RegistroAsistenciaDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarRegistrosAsistenciaAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<RegistroAsistenciaDto>>> Handle(
        ListarRegistrosAsistenciaAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.RegistrosAsistencia
            .AsNoTracking()
            .Where(r => r.HoraEntrada >= request.Desde && r.HoraEntrada <= request.Hasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(r => r.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(r => r.Empleado.IdArea == request.IdArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodFuenteMarcacion))
        {
            var fuente = request.CodFuenteMarcacion.Trim().ToUpperInvariant();
            query = query.Where(r => r.CodFuenteMarcacion == fuente);
        }

        if (request.SoloAbiertos == true)
        {
            query = query.Where(r => r.HoraSalida == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.HoraEntrada)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistroAsistenciaDto(
                r.IdRegistro,
                r.IdEmpleado,
                r.Empleado.Nombres + " " + r.Empleado.Apellidos,
                r.IdAsignacion,
                r.HoraEntrada,
                r.HoraSalida,
                r.CodFuenteMarcacion,
                r.FuenteMarcacion.Nombre,
                r.Observacion,
                r.RegistradoPor,
                r.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<RegistroAsistenciaDto>(items, totalCount, pageNumber, pageSize));
    }
}


// =====================================================================
// SECCIÓN 10: PANEL DE ASISTENCIA DEL DÍA (permiso: asistencia.consultar)
// =====================================================================

/// <summary>
/// Panel del día: cada turno programado (no anulado) contra su marcación real, con tardanza y turnos sin asistencia.
/// La tardanza se mide contra el inicio programado y se marca cuando supera TOLERANCIA_ENTRADA_MIN.
/// Un turno sin asistencia muestra la novedad aprobada que lo explica (una ausencia no anula el turno).
/// Estados: PROGRAMADO (no ha iniciado), SIN_ENTRADA (ya inició y no hay marcación), EN_CURSO,
/// COMPLETADO y SIN_ASISTENCIA (terminó sin marcación).
/// </summary>
[RequirePermission(PermisosSistema.AsistenciaConsultar)]
public sealed record ObtenerPanelAsistenciaDiaQuery(
    DateOnly? Fecha = null,
    int? IdArea = null) : IRequest<Result<PanelAsistenciaDiaDto>>;

public sealed class ObtenerPanelAsistenciaDiaQueryHandler
    : IRequestHandler<ObtenerPanelAsistenciaDiaQuery, Result<PanelAsistenciaDiaDto>>
{
    public const string EstadoProgramado = "PROGRAMADO";
    public const string EstadoSinEntrada = "SIN_ENTRADA";
    public const string EstadoEnCurso = "EN_CURSO";
    public const string EstadoCompletado = "COMPLETADO";
    public const string EstadoSinAsistencia = "SIN_ASISTENCIA";

    private readonly GestionTurnosContext _context;
    private readonly IParametroService _parametroService;
    private readonly IConfiguration _configuration;

    public ObtenerPanelAsistenciaDiaQueryHandler(
        GestionTurnosContext context,
        IParametroService parametroService,
        IConfiguration configuration)
    {
        _context = context;
        _parametroService = parametroService;
        _configuration = configuration;
    }

    public async Task<Result<PanelAsistenciaDiaDto>> Handle(
        ObtenerPanelAsistenciaDiaQuery request,
        CancellationToken cancellationToken)
    {
        var fecha = request.Fecha ?? Application.Common.TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
        var ahora = DateTimeOffset.UtcNow;

        int tolerancia = await _parametroService.ObtenerEnteroAsync(
            ParametrosCodigos.ToleranciaEntradaMin, fecha, cancellationToken);

        var query = _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.Fecha == fecha && a.CodEstadoAsignacion != EstadosAsignacion.Anulada);

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        var filas = await query
            .Select(a => new
            {
                a.IdAsignacion,
                a.IdEmpleado,
                NombreEmpleado = a.Empleado.Nombres + " " + a.Empleado.Apellidos,
                a.Empleado.IdArea,
                NombreArea = a.Empleado.Area.Nombre,
                a.IdTurno,
                NombreTurno = a.Turno.Nombre,
                a.Periodo,
                Registro = _context.RegistrosAsistencia
                    .Where(r => r.IdAsignacion == a.IdAsignacion)
                    .Select(r => new { r.IdRegistro, r.HoraEntrada, r.HoraSalida })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Novedades aprobadas que pueden explicar los turnos sin marcación
        var idsEmpleado = filas.Where(f => f.Registro is null).Select(f => f.IdEmpleado).Distinct().ToList();
        var desde = filas.Count > 0 ? filas.Min(f => f.Periodo.Inicio) : DateTimeOffset.MinValue;
        var hasta = filas.Count > 0 ? filas.Max(f => f.Periodo.Fin) : DateTimeOffset.MinValue;

        var novedades = idsEmpleado.Count == 0
            ? []
            : await _context.Novedades
                .AsNoTracking()
                .Where(n => idsEmpleado.Contains(n.IdEmpleado) &&
                            n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                            n.Inicio < hasta && n.Fin > desde)
                .Select(n => new { n.IdEmpleado, n.Inicio, n.Fin, Nombre = n.TipoNovedad.Nombre })
                .ToListAsync(cancellationToken);

        var turnos = filas
            .OrderBy(f => f.Periodo.Inicio)
            .ThenBy(f => f.NombreEmpleado)
            .Select(f =>
            {
                int minutosTarde = f.Registro is null
                    ? 0
                    : Math.Max(0, (int)Math.Floor((f.Registro.HoraEntrada - f.Periodo.Inicio).TotalMinutes));

                string estado = f.Registro is not null
                    ? (f.Registro.HoraSalida.HasValue ? EstadoCompletado : EstadoEnCurso)
                    : ahora < f.Periodo.Inicio ? EstadoProgramado
                    : ahora < f.Periodo.Fin ? EstadoSinEntrada
                    : EstadoSinAsistencia;

                string? novedad = f.Registro is null
                    ? novedades
                        .Where(n => n.IdEmpleado == f.IdEmpleado && n.Inicio < f.Periodo.Fin && n.Fin > f.Periodo.Inicio)
                        .Select(n => n.Nombre)
                        .FirstOrDefault()
                    : null;

                return new PanelAsistenciaTurnoDto(
                    f.IdAsignacion,
                    f.IdEmpleado,
                    f.NombreEmpleado,
                    f.IdArea,
                    f.NombreArea,
                    f.IdTurno,
                    f.NombreTurno,
                    f.Periodo.Inicio,
                    f.Periodo.Fin,
                    f.Registro?.IdRegistro,
                    f.Registro?.HoraEntrada,
                    f.Registro?.HoraSalida,
                    minutosTarde,
                    minutosTarde > tolerancia,
                    estado,
                    novedad);
            })
            .ToList();

        return Result.Success(new PanelAsistenciaDiaDto(
            fecha,
            tolerancia,
            turnos.Count,
            turnos.Count(t => t.Estado == EstadoCompletado),
            turnos.Count(t => t.Estado == EstadoEnCurso),
            turnos.Count(t => t.Tarde),
            turnos.Count(t => t.Estado == EstadoSinAsistencia),
            turnos));
    }
}
