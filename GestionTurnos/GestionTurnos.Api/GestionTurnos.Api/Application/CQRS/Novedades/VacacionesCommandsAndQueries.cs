using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Novedades;
using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services;
using GestionTurnos.Api.Domain.Entities.Novedades;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Novedades;

// =====================================================================
// 3.4 SALDO DE VACACIONES DEL EMPLEADO (novedades.v_saldo_vacaciones - RF-06)
// =====================================================================

public sealed record ObtenerMiSaldoVacacionesQuery : IRequest<Result<SaldoVacacionesDto>>, IRequireAuthentication;

public sealed class ObtenerMiSaldoVacacionesQueryHandler
    : IRequestHandler<ObtenerMiSaldoVacacionesQuery, Result<SaldoVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiSaldoVacacionesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<SaldoVacacionesDto>> Handle(
        ObtenerMiSaldoVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<SaldoVacacionesDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var vista = await _context.VistaSaldosVacaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdEmpleado == idEmpleado, cancellationToken);

        if (vista is null)
        {
            return Result.Success(new SaldoVacacionesDto(idEmpleado, 0m, 0m, 0m, 0m, 0m));
        }

        return Result.Success(new SaldoVacacionesDto(
            vista.IdEmpleado,
            vista.DiasOtorgados,
            vista.DiasUtilizados,
            vista.DiasEnSolicitud,
            vista.DiasDisponibles,
            vista.DiasDisponibles - vista.DiasEnSolicitud));
    }
}

// =====================================================================
// 3.7 SOLICITAR Y CANCELAR VACACIONES (EMPLEADO - CU-03)
// =====================================================================

public sealed record SolicitarMisVacacionesCommand(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion) : IRequest<Result<ResultadoSolicitudVacacionesDto>>, IRequireAuthentication;

public sealed class SolicitarMisVacacionesCommandValidator : AbstractValidator<SolicitarMisVacacionesCommand>
{
    public SolicitarMisVacacionesCommandValidator()
    {
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.FechaInicio)
            .WithMessage("La fecha de fin debe ser mayor o igual a la fecha de inicio.");
        RuleFor(x => x.Observacion).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Observacion));
    }
}

public sealed class SolicitarMisVacacionesCommandHandler
    : IRequestHandler<SolicitarMisVacacionesCommand, Result<ResultadoSolicitudVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IDiasVacacionesCalculator _diasCalculator;

    public SolicitarMisVacacionesCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEmpleadoSesionService empleadoSesionService,
        IDiasVacacionesCalculator diasCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _empleadoSesionService = empleadoSesionService;
        _diasCalculator = diasCalculator;
    }

    public async Task<Result<ResultadoSolicitudVacacionesDto>> Handle(
        SolicitarMisVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var idUsuario = _currentUserService.UserId!.Value;

        return await SolicitudesVacacionesComunes.CrearAsync(
            _context,
            _unitOfWork,
            _diasCalculator,
            idEmpleado,
            idUsuario,
            request.FechaInicio,
            request.FechaFin,
            request.Observacion,
            cancellationToken);
    }
}

/// <summary>
/// Sección 3.7: Cancelar solicitud de vacaciones (solo el dueño y solo si sigue PENDIENTE).
/// Si afecta 0 filas, responde "no encontrado" (Sección 2).
/// </summary>
public sealed record CancelarMiSolicitudVacacionesCommand(
    long IdSolicitudVacaciones) : IRequest<Result>, IRequireAuthentication;

public sealed class CancelarMiSolicitudVacacionesCommandHandler
    : IRequestHandler<CancelarMiSolicitudVacacionesCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public CancelarMiSolicitudVacacionesCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(
        CancelarMiSolicitudVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var solicitud = await _context.SolicitudesVacaciones
            .FirstOrDefaultAsync(
                s => s.IdSolicitudVacaciones == request.IdSolicitudVacaciones &&
                     s.IdEmpleado == idEmpleado &&
                     s.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                cancellationToken);

        if (solicitud is null)
        {
            return Result.Failure(new Error(
                "Vacaciones.SolicitudNoEncontrada",
                "La solicitud de vacaciones no fue encontrada o ya no se encuentra pendiente.",
                ErrorType.NotFound));
        }

        solicitud.CodEstadoSolicitud = EstadosSolicitud.Cancelada;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

public sealed record ObtenerMisSolicitudesVacacionesQuery
    : IRequest<Result<IReadOnlyList<SolicitudVacacionesDto>>>, IRequireAuthentication;

public sealed class ObtenerMisSolicitudesVacacionesQueryHandler
    : IRequestHandler<ObtenerMisSolicitudesVacacionesQuery, Result<IReadOnlyList<SolicitudVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisSolicitudesVacacionesQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<SolicitudVacacionesDto>>> Handle(
        ObtenerMisSolicitudesVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SolicitudVacacionesDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(s => s.IdEmpleado == idEmpleado)
            .OrderByDescending(s => s.SolicitadoEn)
            .Select(s => new SolicitudVacacionesDto(
                s.IdSolicitudVacaciones,
                s.IdEmpleado,
                s.Empleado.Nombres + " " + s.Empleado.Apellidos,
                s.FechaInicio,
                s.FechaFin,
                s.DiasSolicitados,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.Observacion,
                s.SolicitadoPor,
                s.SolicitadoEn,
                s.ResueltoPor,
                s.ResueltoEn,
                s.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SolicitudVacacionesDto>>(items);
    }
}

// =====================================================================
// 4 y 5. GESTIÓN Y RESOLUCIÓN ADMINISTRATIVA DE VACACIONES (permiso: vacaciones.aprobar)
// =====================================================================

/// <summary>
/// Sección 5 (RF-06): Registrar los días causados por período en novedades.periodo_vacacional.
/// Valor base: parámetro VACACIONES_DIAS_POR_ANIO si no se envía dias_otorgados explícito.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record RegistrarPeriodoVacacionalCommand(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal? DiasOtorgados = null) : IRequest<Result<long>>;

public sealed class RegistrarPeriodoVacacionalCommandValidator : AbstractValidator<RegistrarPeriodoVacacionalCommand>
{
    public RegistrarPeriodoVacacionalCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin)
            .GreaterThan(x => x.FechaInicio)
            .WithMessage("La fecha de fin del período vacacional debe ser posterior a la de inicio.");
        RuleFor(x => x.DiasOtorgados)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DiasOtorgados.HasValue);
    }
}

public sealed class RegistrarPeriodoVacacionalCommandHandler
    : IRequestHandler<RegistrarPeriodoVacacionalCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParametroService _parametroService;

    public RegistrarPeriodoVacacionalCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IParametroService parametroService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _parametroService = parametroService;
    }

    public async Task<Result<long>> Handle(
        RegistrarPeriodoVacacionalCommand request,
        CancellationToken cancellationToken)
    {
        decimal dias = request.DiasOtorgados ?? await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.VacacionesDiasPorAnio, request.FechaInicio, cancellationToken);

        var periodo = new PeriodoVacacional
        {
            IdEmpleado = request.IdEmpleado,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            DiasOtorgados = dias
        };

        try
        {
            await _context.PeriodosVacacionales.AddAsync(periodo, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(periodo.IdPeriodoVacacional);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Sección 4: Resolución de solicitudes de vacaciones (APROBADA o RECHAZADA).
/// - resuelto_por y resuelto_en van siempre juntos.
/// - motivo_rechazo es obligatorio al rechazar.
/// - Al aprobar revalida el saldo dentro de la transacción y evalúa CONFLICTO_BLOQUEA frente a turnos programados.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ResolverSolicitudVacacionesCommand(
    long IdSolicitudVacaciones,
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false) : IRequest<Result>;

public sealed class ResolverSolicitudVacacionesCommandValidator
    : AbstractValidator<ResolverSolicitudVacacionesCommand>
{
    public ResolverSolicitudVacacionesCommandValidator()
    {
        RuleFor(x => x.IdSolicitudVacaciones).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio al rechazar una solicitud.")
            .MaximumLength(300)
            .When(x => !x.Aprobar);
    }
}

public sealed class ResolverSolicitudVacacionesCommandHandler
    : IRequestHandler<ResolverSolicitudVacacionesCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParametroService _parametroService;

    public ResolverSolicitudVacacionesCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IParametroService parametroService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _parametroService = parametroService;
    }

    public async Task<Result> Handle(
        ResolverSolicitudVacacionesCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloqueos: empleado (serializa saldo y programación de turnos) y después la solicitud (FOR UPDATE).
            var solicitud = await SolicitudesVacacionesComunes.BloquearSolicitudAsync(
                _context, request.IdSolicitudVacaciones, cancellationToken);

            if (solicitud is null || solicitud.CodEstadoSolicitud != EstadosSolicitud.Pendiente)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Vacaciones.SolicitudNoPendiente",
                    "La solicitud de vacaciones no existe o ya fue resuelta.",
                    ErrorType.NotFound));
            }

            if (request.Aprobar)
            {
                // 1. Revalidar el saldo dentro de la transacción (el saldo nunca se guarda: se lee de la vista).
                var saldo = await _context.VistaSaldosVacaciones
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.IdEmpleado == solicitud.IdEmpleado, cancellationToken);

                decimal diasDisponibles = saldo?.DiasDisponibles ?? 0m;
                if (diasDisponibles < solicitud.DiasSolicitados)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(new Error(
                        "Vacaciones.SaldoInsuficiente",
                        $"El empleado no cuenta con saldo disponible suficiente ({diasDisponibles:0.##} días) para aprobar {solicitud.DiasSolicitados:0.##} días.",
                        ErrorType.Conflict));
                }

                // 2. Turnos que chocan: asistencia, CONFLICTO_BLOQUEA y confirmación (sección 11).
                var turnos = await ConflictosTurnosNovedad.TurnosEnRangoVacacionesAsync(
                    _context, solicitud.IdEmpleado, solicitud.FechaInicio, solicitud.FechaFin, asTracking: true, cancellationToken);

                var regla = await ConflictosTurnosNovedad.AplicarReglaAsync(
                    _context,
                    _parametroService,
                    turnos,
                    solicitud.FechaInicio,
                    request.ConfirmarAnulacionTurnos,
                    "Vacaciones",
                    $"Anulada por vacaciones aprobadas (solicitud #{solicitud.IdSolicitudVacaciones})",
                    cancellationToken);

                if (regla.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return regla;
                }

                solicitud.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
                solicitud.ResueltoPor = adminId;
                solicitud.ResueltoEn = DateTimeOffset.UtcNow;
                solicitud.MotivoRechazo = null;
            }
            else
            {
                solicitud.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
                solicitud.ResueltoPor = adminId;
                solicitud.ResueltoEn = DateTimeOffset.UtcNow;
                solicitud.MotivoRechazo = request.MotivoRechazo!.Trim();
            }

            // No insertar filas manuales en bitácora: el disparador tg_aud_vacaciones ya lo registra.
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ListarPeriodosVacacionalesAdminQuery(long? IdEmpleado = null)
    : IRequest<Result<IReadOnlyList<PeriodoVacacionalDto>>>;

public sealed class ListarPeriodosVacacionalesAdminQueryHandler
    : IRequestHandler<ListarPeriodosVacacionalesAdminQuery, Result<IReadOnlyList<PeriodoVacacionalDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarPeriodosVacacionalesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<PeriodoVacacionalDto>>> Handle(
        ListarPeriodosVacacionalesAdminQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.PeriodosVacacionales.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(p => p.IdEmpleado == request.IdEmpleado.Value);
        }

        var items = await query
            .OrderByDescending(p => p.FechaInicio)
            .Select(p => new PeriodoVacacionalDto(
                p.IdPeriodoVacacional,
                p.IdEmpleado,
                p.Empleado.Nombres + " " + p.Empleado.Apellidos,
                p.FechaInicio,
                p.FechaFin,
                p.DiasOtorgados))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PeriodoVacacionalDto>>(items);
    }
}

[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ListarSaldosVacacionesAdminQuery(long? IdEmpleado = null)
    : IRequest<Result<IReadOnlyList<SaldoVacacionesDto>>>;

public sealed class ListarSaldosVacacionesAdminQueryHandler
    : IRequestHandler<ListarSaldosVacacionesAdminQuery, Result<IReadOnlyList<SaldoVacacionesDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarSaldosVacacionesAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<SaldoVacacionesDto>>> Handle(
        ListarSaldosVacacionesAdminQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.VistaSaldosVacaciones.AsNoTracking();
        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(v => v.IdEmpleado == request.IdEmpleado.Value);
        }

        var items = await query
            .OrderBy(v => v.IdEmpleado)
            .Select(v => new SaldoVacacionesDto(
                v.IdEmpleado,
                v.DiasOtorgados,
                v.DiasUtilizados,
                v.DiasEnSolicitud,
                v.DiasDisponibles,
                v.DiasDisponibles - v.DiasEnSolicitud))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SaldoVacacionesDto>>(items);
    }
}


// =====================================================================
// SECCIÓN 11: SOLICITUD A NOMBRE DEL EMPLEADO, TURNOS QUE CHOCAN Y ANULACIÓN
// =====================================================================

/// <summary>
/// Sección 11: el administrador crea una solicitud a nombre del empleado. Mismas reglas que la del empleado
/// (días hábiles, saldo efectivo, sin cruce) y nace PENDIENTE; se aprueba con el flujo normal.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record RegistrarSolicitudVacacionesAdminCommand(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion) : IRequest<Result<ResultadoSolicitudVacacionesDto>>;

public sealed class RegistrarSolicitudVacacionesAdminCommandValidator
    : AbstractValidator<RegistrarSolicitudVacacionesAdminCommand>
{
    public RegistrarSolicitudVacacionesAdminCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.FechaInicio).NotEmpty();
        RuleFor(x => x.FechaFin)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.FechaInicio)
            .WithMessage("La fecha de fin debe ser mayor o igual a la fecha de inicio.");
        RuleFor(x => x.Observacion).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Observacion));
    }
}

public sealed class RegistrarSolicitudVacacionesAdminCommandHandler
    : IRequestHandler<RegistrarSolicitudVacacionesAdminCommand, Result<ResultadoSolicitudVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDiasVacacionesCalculator _diasCalculator;

    public RegistrarSolicitudVacacionesAdminCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDiasVacacionesCalculator diasCalculator)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _diasCalculator = diasCalculator;
    }

    public async Task<Result<ResultadoSolicitudVacacionesDto>> Handle(
        RegistrarSolicitudVacacionesAdminCommand request,
        CancellationToken cancellationToken)
    {
        bool empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.IdEmpleado == request.IdEmpleado && e.CodEstadoEmpleado == EstadosEmpleado.Activo, cancellationToken);

        if (!empleadoActivo)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                "Vacaciones.EmpleadoNoActivo",
                "Solo se pueden registrar solicitudes de vacaciones para empleados activos.",
                ErrorType.Failure));
        }

        return await SolicitudesVacacionesComunes.CrearAsync(
            _context,
            _unitOfWork,
            _diasCalculator,
            request.IdEmpleado,
            _currentUserService.UserId!.Value,
            request.FechaInicio,
            request.FechaFin,
            request.Observacion,
            cancellationToken);
    }
}

/// <summary>
/// Sección 11: turnos que chocan con una solicitud de vacaciones, para decidir antes de aprobar.
/// Indica el valor vigente de CONFLICTO_BLOQUEA y qué turnos ya tienen asistencia (esos impiden aprobar).
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record ConsultarTurnosEnConflictoVacacionesQuery(long IdSolicitudVacaciones)
    : IRequest<Result<ConflictosSolicitudVacacionesDto>>;

public sealed class ConsultarTurnosEnConflictoVacacionesQueryHandler
    : IRequestHandler<ConsultarTurnosEnConflictoVacacionesQuery, Result<ConflictosSolicitudVacacionesDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IParametroService _parametroService;

    public ConsultarTurnosEnConflictoVacacionesQueryHandler(
        GestionTurnosContext context,
        IParametroService parametroService)
    {
        _context = context;
        _parametroService = parametroService;
    }

    public async Task<Result<ConflictosSolicitudVacacionesDto>> Handle(
        ConsultarTurnosEnConflictoVacacionesQuery request,
        CancellationToken cancellationToken)
    {
        var solicitud = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdSolicitudVacaciones == request.IdSolicitudVacaciones, cancellationToken);

        if (solicitud is null)
        {
            return Result.Failure<ConflictosSolicitudVacacionesDto>(SolicitudesVacacionesComunes.NoEncontrada);
        }

        var turnos = await ConflictosTurnosNovedad.TurnosEnRangoVacacionesAsync(
            _context, solicitud.IdEmpleado, solicitud.FechaInicio, solicitud.FechaFin, asTracking: false, cancellationToken);

        bool conflictoBloquea = await _parametroService.ObtenerBooleanoAsync(
            ParametrosCodigos.ConflictoBloquea, solicitud.FechaInicio, cancellationToken);

        return Result.Success(new ConflictosSolicitudVacacionesDto(
            solicitud.IdSolicitudVacaciones,
            solicitud.IdEmpleado,
            solicitud.FechaInicio,
            solicitud.FechaFin,
            conflictoBloquea,
            await ConflictosTurnosNovedad.DetallarAsync(_context, turnos, cancellationToken)));
    }
}

/// <summary>
/// Sección 11: anula unas vacaciones APROBADAS que todavía no inician. Quedan CANCELADA con el motivo,
/// y el saldo vuelve solo porque la vista solo descuenta las APROBADA. Los turnos que se anularon al aprobar
/// no se restauran: se reprograman con el flujo normal.
/// </summary>
[RequirePermission(PermisosSistema.VacacionesAprobar)]
public sealed record AnularVacacionesAprobadasCommand(
    long IdSolicitudVacaciones,
    string Motivo) : IRequest<Result>;

public sealed class AnularVacacionesAprobadasCommandValidator : AbstractValidator<AnularVacacionesAprobadasCommand>
{
    public AnularVacacionesAprobadasCommandValidator()
    {
        RuleFor(x => x.IdSolicitudVacaciones).GreaterThan(0);
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("El motivo de la anulación es obligatorio.")
            .MaximumLength(300);
    }
}

public sealed class AnularVacacionesAprobadasCommandHandler : IRequestHandler<AnularVacacionesAprobadasCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public AnularVacacionesAprobadasCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    public async Task<Result> Handle(AnularVacacionesAprobadasCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var solicitud = await SolicitudesVacacionesComunes.BloquearSolicitudAsync(
                _context, request.IdSolicitudVacaciones, cancellationToken);

            if (solicitud is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(SolicitudesVacacionesComunes.NoEncontrada);
            }

            if (solicitud.CodEstadoSolicitud != EstadosSolicitud.Aprobada)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Vacaciones.NoAprobada",
                    "Solo se pueden anular vacaciones aprobadas. Una solicitud pendiente se rechaza o se cancela.",
                    ErrorType.Conflict));
            }

            var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
            if (solicitud.FechaInicio <= hoy)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Vacaciones.YaIniciadas",
                    $"No se pueden anular vacaciones que ya iniciaron (inicio: {solicitud.FechaInicio:yyyy-MM-dd}).",
                    ErrorType.Conflict));
            }

            solicitud.CodEstadoSolicitud = EstadosSolicitud.Cancelada;
            solicitud.ResueltoPor = _currentUserService.UserId!.Value;
            solicitud.ResueltoEn = DateTimeOffset.UtcNow;
            solicitud.MotivoRechazo = request.Motivo.Trim();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

/// <summary>
/// Lógica común de solicitudes de vacaciones (empleado y administrador).
/// </summary>
internal static class SolicitudesVacacionesComunes
{
    public static readonly Error NoEncontrada = new(
        "Vacaciones.SolicitudNoEncontrada",
        "La solicitud de vacaciones no existe.",
        ErrorType.NotFound);

    /// <summary>
    /// Bloquea el empleado y después la solicitud (orden único, igual que la programación de turnos).
    /// Debe llamarse dentro de una transacción.
    /// </summary>
    public static async Task<SolicitudVacaciones?> BloquearSolicitudAsync(
        GestionTurnosContext context,
        long idSolicitud,
        CancellationToken cancellationToken)
    {
        var idEmpleado = await context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(s => s.IdSolicitudVacaciones == idSolicitud)
            .Select(s => (long?)s.IdEmpleado)
            .FirstOrDefaultAsync(cancellationToken);

        if (idEmpleado is null)
        {
            return null;
        }

        await context.BloquearPorIdAsync<Empleado>(idEmpleado.Value, cancellationToken);
        return await context.BloquearPorIdAsync<SolicitudVacaciones>(idSolicitud, cancellationToken);
    }

    /// <summary>
    /// Crea una solicitud PENDIENTE: calcula los días hábiles en el backend, exige saldo efectivo
    /// (disponible menos lo que ya está en solicitud) y que no se cruce con otra solicitud vigente.
    /// El empleado queda bloqueado durante la transacción: dos solicitudes simultáneas no gastan el mismo saldo.
    /// </summary>
    public static async Task<Result<ResultadoSolicitudVacacionesDto>> CrearAsync(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IDiasVacacionesCalculator diasCalculator,
        long idEmpleado,
        long idUsuarioSolicitante,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        string? observacion,
        CancellationToken cancellationToken)
    {
        // 1. Días solicitados excluyendo festivos y fines de semana
        decimal diasSolicitados = await diasCalculator.CalcularDiasSolicitadosAsync(fechaInicio, fechaFin, cancellationToken);

        if (diasSolicitados <= 0)
        {
            return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                "Vacaciones.SinDiasHabiles",
                "El rango de fechas seleccionado no contiene días hábiles laborables.",
                ErrorType.Failure));
        }

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await context.BloquearPorIdAsync<Empleado>(idEmpleado, cancellationToken);

            // 2. Saldo efectivo leído de novedades.v_saldo_vacaciones (nunca se guarda)
            var saldo = await context.VistaSaldosVacaciones
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.IdEmpleado == idEmpleado, cancellationToken);

            decimal saldoEfectivo = (saldo?.DiasDisponibles ?? 0m) - (saldo?.DiasEnSolicitud ?? 0m);
            if (saldoEfectivo < diasSolicitados)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                    "Vacaciones.SaldoInsuficiente",
                    $"Saldo de vacaciones insuficiente. Días requeridos: {diasSolicitados:0.##}, saldo disponible descontando solicitudes en curso: {saldoEfectivo:0.##}.",
                    ErrorType.Conflict));
            }

            // 3. Sin cruce con solicitudes vigentes (ex_vacaciones_sin_cruce)
            bool existeCruce = await context.SolicitudesVacaciones
                .AsNoTracking()
                .AnyAsync(v => v.IdEmpleado == idEmpleado &&
                               (v.CodEstadoSolicitud == EstadosSolicitud.Pendiente ||
                                v.CodEstadoSolicitud == EstadosSolicitud.Aprobada) &&
                               fechaInicio <= v.FechaFin &&
                               v.FechaInicio <= fechaFin, cancellationToken);

            if (existeCruce)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<ResultadoSolicitudVacacionesDto>(new Error(
                    "Vacaciones.CruceFechas",
                    "Ya existe una solicitud de vacaciones en esas fechas.",
                    ErrorType.Conflict));
            }

            var solicitud = new SolicitudVacaciones
            {
                IdEmpleado = idEmpleado,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                DiasSolicitados = diasSolicitados,
                CodEstadoSolicitud = EstadosSolicitud.Pendiente,
                Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim(),
                SolicitadoPor = idUsuarioSolicitante,
                SolicitadoEn = DateTimeOffset.UtcNow
            };

            await context.SolicitudesVacaciones.AddAsync(solicitud, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            // 4. Informa (sin bloquear) si hay turnos programados en el rango
            var fechasTurnosEnRango = await context.Asignaciones
                .AsNoTracking()
                .Where(a => a.IdEmpleado == idEmpleado &&
                            a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                            a.Fecha >= fechaInicio &&
                            a.Fecha <= fechaFin)
                .OrderBy(a => a.Fecha)
                .Select(a => a.Fecha)
                .Distinct()
                .ToListAsync(cancellationToken);

            string? advertencia = fechasTurnosEnRango.Count > 0
                ? $"Advertencia: existen {fechasTurnosEnRango.Count} día(s) con turnos programados en el rango solicitado."
                : null;

            return Result.Success(new ResultadoSolicitudVacacionesDto(
                solicitud.IdSolicitudVacaciones,
                diasSolicitados,
                fechasTurnosEnRango,
                advertencia));
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<ResultadoSolicitudVacacionesDto>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}
