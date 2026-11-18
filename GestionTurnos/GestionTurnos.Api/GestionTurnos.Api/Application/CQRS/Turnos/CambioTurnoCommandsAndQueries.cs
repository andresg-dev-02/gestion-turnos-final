using FluentValidation;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// 3.9 CAMBIO DE TURNO CON UN COMPAÑERO (OPC-03, Fase 2 / Final)
// =====================================================================

/// <summary>
/// Sección 3.9: Solicitar cambio de turno con un compañero.
/// Valida que id_asignacion_solicitante pertenezca al empleado de la sesión,
/// que ambas asignaciones sean futuras y no estén ANULADA.
/// </summary>
public sealed record SolicitarMiCambioTurnoCommand(
    long IdAsignacionSolicitante,
    long IdAsignacionCompanero,
    string? Motivo) : IRequest<Result<long>>, IRequireAuthentication;

public sealed class SolicitarMiCambioTurnoCommandValidator : AbstractValidator<SolicitarMiCambioTurnoCommand>
{
    public SolicitarMiCambioTurnoCommandValidator()
    {
        RuleFor(x => x.IdAsignacionSolicitante).GreaterThan(0);
        RuleFor(x => x.IdAsignacionCompanero)
            .GreaterThan(0)
            .NotEqual(x => x.IdAsignacionSolicitante)
            .WithMessage("Las asignaciones a intercambiar deben ser distintas.");
        RuleFor(x => x.Motivo)
            .MaximumLength(300)
            .When(x => !string.IsNullOrWhiteSpace(x.Motivo));
    }
}

public sealed class SolicitarMiCambioTurnoCommandHandler
    : IRequestHandler<SolicitarMiCambioTurnoCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public SolicitarMiCambioTurnoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<long>> Handle(
        SolicitarMiCambioTurnoCommand request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<long>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var ahora = DateTimeOffset.UtcNow;

        var asigSolicitante = await _context.Asignaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacionSolicitante, cancellationToken);

        // 1. Valida que id_asignacion_solicitante pertenezca al empleado de la sesión
        if (asigSolicitante is null || asigSolicitante.IdEmpleado != idEmpleado)
        {
            return Result.Failure<long>(new Error(
                "Turnos.AsignacionNoPerteneceAlEmpleado",
                "La asignación solicitante no existe o no pertenece al empleado de la sesión.",
                ErrorType.NotFound));
        }

        var asigCompanero = await _context.Asignaciones
            .AsNoTracking()
            .Include(a => a.Empleado)
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacionCompanero, cancellationToken);

        if (asigCompanero is null || asigCompanero.Empleado.CodEstadoEmpleado != EstadosEmpleado.Activo)
        {
            return Result.Failure<long>(new Error(
                "Turnos.AsignacionCompaneroInvalida",
                "La asignación del compañero no existe o el empleado no se encuentra activo.",
                ErrorType.NotFound));
        }

        if (asigCompanero.IdEmpleado == idEmpleado)
        {
            return Result.Failure<long>(new Error(
                "Turnos.CambioMismoEmpleado",
                "El cambio de turno debe solicitarse con una asignación de otro compañero.",
                ErrorType.Failure));
        }

        // 2. Valida que ambas asignaciones no estén ANULADA y sean futuras
        if (asigSolicitante.CodEstadoAsignacion == EstadosAsignacion.Anulada ||
            asigCompanero.CodEstadoAsignacion == EstadosAsignacion.Anulada)
        {
            return Result.Failure<long>(new Error(
                "Turnos.AsignacionAnulada",
                "No se puede solicitar cambio sobre una asignación anulada.",
                ErrorType.Conflict));
        }

        if (asigSolicitante.Periodo.Inicio <= ahora || asigCompanero.Periodo.Inicio <= ahora)
        {
            return Result.Failure<long>(new Error(
                "Turnos.AsignacionNoFutura",
                "Ambas asignaciones deben ser futuras para solicitar un cambio de turno.",
                ErrorType.Conflict));
        }

        var solicitud = new SolicitudCambioTurno
        {
            IdAsignacionSolicitante = request.IdAsignacionSolicitante,
            IdAsignacionCompanero = request.IdAsignacionCompanero,
            Motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            CodEstadoSolicitud = EstadosSolicitud.Pendiente,
            SolicitadoEn = ahora
        };

        try
        {
            await _context.SolicitudesCambioTurno.AddAsync(solicitud, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(solicitud.IdSolicitudCambio);
        }
        catch (Exception ex)
        {
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

public sealed record CancelarMiCambioTurnoCommand(long IdSolicitudCambio)
    : IRequest<Result>, IRequireAuthentication;

public sealed class CancelarMiCambioTurnoCommandHandler
    : IRequestHandler<CancelarMiCambioTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public CancelarMiCambioTurnoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result> Handle(CancelarMiCambioTurnoCommand request, CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var solicitud = await _context.SolicitudesCambioTurno
            .FirstOrDefaultAsync(
                s => s.IdSolicitudCambio == request.IdSolicitudCambio &&
                     s.AsignacionSolicitante.IdEmpleado == idEmpleado &&
                     s.CodEstadoSolicitud == EstadosSolicitud.Pendiente,
                cancellationToken);

        if (solicitud is null)
        {
            return Result.Failure(new Error(
                "Turnos.SolicitudCambioNoEncontrada",
                "La solicitud de cambio de turno no fue encontrada o ya no está pendiente.",
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

public sealed record ObtenerMisCambiosTurnoQuery
    : IRequest<Result<IReadOnlyList<SolicitudCambioTurnoDto>>>, IRequireAuthentication;

public sealed class ObtenerMisCambiosTurnoQueryHandler
    : IRequestHandler<ObtenerMisCambiosTurnoQuery, Result<IReadOnlyList<SolicitudCambioTurnoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMisCambiosTurnoQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<SolicitudCambioTurnoDto>>> Handle(
        ObtenerMisCambiosTurnoQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SolicitudCambioTurnoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var items = await _context.SolicitudesCambioTurno
            .AsNoTracking()
            .Where(s => s.AsignacionSolicitante.IdEmpleado == idEmpleado ||
                        s.AsignacionCompanero.IdEmpleado == idEmpleado)
            .OrderByDescending(s => s.SolicitadoEn)
            .Select(s => new SolicitudCambioTurnoDto(
                s.IdSolicitudCambio,
                s.IdAsignacionSolicitante,
                s.AsignacionSolicitante.IdEmpleado,
                s.AsignacionSolicitante.Empleado.Nombres + " " + s.AsignacionSolicitante.Empleado.Apellidos,
                s.AsignacionSolicitante.Fecha,
                s.AsignacionSolicitante.Turno.Nombre,
                s.IdAsignacionCompanero,
                s.AsignacionCompanero.IdEmpleado,
                s.AsignacionCompanero.Empleado.Nombres + " " + s.AsignacionCompanero.Empleado.Apellidos,
                s.AsignacionCompanero.Fecha,
                s.AsignacionCompanero.Turno.Nombre,
                s.Motivo,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.SolicitadoEn,
                s.ResueltoPor,
                s.ResueltoEn,
                s.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SolicitudCambioTurnoDto>>(items);
    }
}

/// <summary>
/// Sección 3.9: Resolución administrativa de cambio de turno.
/// Al aprobar, NO se intercambia id_empleado con un UPDATE:
/// se anulan las dos asignaciones y se crean dos nuevas con los empleados cruzados,
/// todo en una sola transacción para que las validaciones de cruce y la auditoría funcionen.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ResolverCambioTurnoCommand(
    long IdSolicitudCambio,
    bool Aprobar,
    string? MotivoRechazo = null) : IRequest<Result>;

public sealed class ResolverCambioTurnoCommandValidator : AbstractValidator<ResolverCambioTurnoCommand>
{
    public ResolverCambioTurnoCommandValidator()
    {
        RuleFor(x => x.IdSolicitudCambio).GreaterThan(0);
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio al rechazar un cambio de turno.")
            .MaximumLength(300)
            .When(x => !x.Aprobar);
    }
}

public sealed class ResolverCambioTurnoCommandHandler : IRequestHandler<ResolverCambioTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public ResolverCambioTurnoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IRevisionConflictosTurno revisionConflictos)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _revisionConflictos = revisionConflictos;
    }

    private static readonly Error SolicitudNoPendiente = new(
        "Turnos.SolicitudCambioNoPendiente",
        "La solicitud de cambio de turno no existe o ya fue resuelta.",
        ErrorType.NotFound);

    public async Task<Result> Handle(ResolverCambioTurnoCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        var ahora = DateTimeOffset.UtcNow;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Identificadores para bloquear en orden (sin rastrear todavía).
            var datos = await _context.SolicitudesCambioTurno
                .AsNoTracking()
                .Where(s => s.IdSolicitudCambio == request.IdSolicitudCambio)
                .Select(s => new
                {
                    s.IdAsignacionSolicitante,
                    s.IdAsignacionCompanero,
                    IdEmpleadoSolicitante = s.AsignacionSolicitante.IdEmpleado,
                    IdEmpleadoCompanero = s.AsignacionCompanero.IdEmpleado
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (datos is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(SolicitudNoPendiente);
            }

            // Orden de bloqueo igual al de la programación (empleados y después asignaciones):
            // empleados → solicitud → asignaciones en orden ascendente. Evita interbloqueos y
            // que otra programación o resolución simultánea pase las mismas validaciones.
            if (request.Aprobar)
            {
                await _revisionConflictos.BloquearEmpleadosAsync(
                    [datos.IdEmpleadoSolicitante, datos.IdEmpleadoCompanero], cancellationToken);
            }

            var solicitud = await _context.BloquearPorIdAsync<SolicitudCambioTurno>(
                request.IdSolicitudCambio, cancellationToken);

            if (solicitud is null || solicitud.CodEstadoSolicitud != EstadosSolicitud.Pendiente)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(SolicitudNoPendiente);
            }

            if (!request.Aprobar)
            {
                solicitud.CodEstadoSolicitud = EstadosSolicitud.Rechazada;
                solicitud.ResueltoPor = adminId;
                solicitud.ResueltoEn = ahora;
                solicitud.MotivoRechazo = request.MotivoRechazo!.Trim();

                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success();
            }

            Asignacion? asig1 = null;
            Asignacion? asig2 = null;
            foreach (var idAsignacion in new[] { solicitud.IdAsignacionSolicitante, solicitud.IdAsignacionCompanero }.Order())
            {
                var bloqueada = await _context.BloquearPorIdAsync<Asignacion>(idAsignacion, cancellationToken);
                if (idAsignacion == solicitud.IdAsignacionSolicitante)
                {
                    asig1 = bloqueada;
                }
                else
                {
                    asig2 = bloqueada;
                }
            }

            if (asig1 is null || asig2 is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AsignacionesTurno.NoEncontrada);
            }

            // Ninguna de las dos puede estar anulada ni tener asistencia registrada.
            foreach (var asignacion in new[] { asig1, asig2 })
            {
                var editable = await AsignacionesTurno.ValidarEditableAsync(
                    _context, asignacion, "intercambiar", cancellationToken);
                if (editable.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return editable;
                }
            }

            if (asig1.Periodo.Inicio <= ahora || asig2.Periodo.Inicio <= ahora)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(new Error(
                    "Turnos.AsignacionNoFutura",
                    "No se puede aprobar un cambio de turno sobre asignaciones que ya iniciaron o pasaron.",
                    ErrorType.Conflict));
            }

            // 1. Anular las dos asignaciones originales (no se cambia id_empleado con un UPDATE).
            var estadoOriginal1 = asig1.CodEstadoAsignacion;
            var estadoOriginal2 = asig2.CodEstadoAsignacion;
            var motivo = $"Cambio de turno aprobado (#{solicitud.IdSolicitudCambio})";

            asig1.CodEstadoAsignacion = EstadosAsignacion.Anulada;
            asig1.Observacion = $"Anulada por cambio de turno aprobado (#{solicitud.IdSolicitudCambio})";
            asig2.CodEstadoAsignacion = EstadosAsignacion.Anulada;
            asig2.Observacion = asig1.Observacion;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 2. Crear las dos nuevas con los empleados cruzados, cada una con las mismas
            //    validaciones de la programación (cruces, vacaciones, novedades, descansos y parámetros).
            //    Se conserva el periodo publicado de cada turno.
            var cruces = new[]
            {
                (IdEmpleado: asig1.IdEmpleado, Origen: asig2, Estado: estadoOriginal2),
                (IdEmpleado: asig2.IdEmpleado, Origen: asig1, Estado: estadoOriginal1)
            };

            foreach (var (idEmpleado, origen, estado) in cruces)
            {
                var revision = await _revisionConflictos.RevisarAsync(
                    idEmpleado, origen.IdTurno, origen.Fecha, idAsignacionActual: null, cancellationToken);

                Error? error = revision.IsFailure
                    ? revision.Error
                    : revision.Value.PuedeAsignarse ? null : RevisionConflictosTurno.ErrorDeConflictos(revision.Value);

                if (error is not null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(new Error(
                        error.Code,
                        $"El cambio de turno no se puede aprobar para el empleado #{idEmpleado} el {origen.Fecha:yyyy-MM-dd}: {error.Message} No se aplicó ningún cambio.",
                        error.Type));
                }

                await _context.Asignaciones.AddAsync(new Asignacion
                {
                    IdEmpleado = idEmpleado,
                    IdTurno = origen.IdTurno,
                    Fecha = origen.Fecha,
                    Periodo = origen.Periodo,
                    CodEstadoAsignacion = estado,
                    Observacion = motivo,
                    CreadoPor = adminId,
                    CreadoEn = ahora
                }, cancellationToken);

                // Se guarda antes de revisar la siguiente; la base (23P01 / 23514) también valida el cruce.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // 3. Cerrar la solicitud.
            solicitud.CodEstadoSolicitud = EstadosSolicitud.Aprobada;
            solicitud.ResueltoPor = adminId;
            solicitud.ResueltoEn = ahora;
            solicitud.MotivoRechazo = null;

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            // Cualquier fallo (incluido un cruce detectado por la base) revierte las anulaciones y las altas.
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ListarCambiosTurnoAdminQuery(
    string? CodEstadoSolicitud = null,
    long? IdEmpleado = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SolicitudCambioTurnoDto>>>;

public sealed class ListarCambiosTurnoAdminQueryHandler
    : IRequestHandler<ListarCambiosTurnoAdminQuery, Result<PagedResult<SolicitudCambioTurnoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarCambiosTurnoAdminQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<SolicitudCambioTurnoDto>>> Handle(
        ListarCambiosTurnoAdminQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.SolicitudesCambioTurno.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.CodEstadoSolicitud))
        {
            var estado = request.CodEstadoSolicitud.Trim().ToUpperInvariant();
            query = query.Where(s => s.CodEstadoSolicitud == estado);
        }

        if (request.IdEmpleado.HasValue)
        {
            var idEmp = request.IdEmpleado.Value;
            query = query.Where(s =>
                s.AsignacionSolicitante.IdEmpleado == idEmp ||
                s.AsignacionCompanero.IdEmpleado == idEmp);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.SolicitadoEn)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SolicitudCambioTurnoDto(
                s.IdSolicitudCambio,
                s.IdAsignacionSolicitante,
                s.AsignacionSolicitante.IdEmpleado,
                s.AsignacionSolicitante.Empleado.Nombres + " " + s.AsignacionSolicitante.Empleado.Apellidos,
                s.AsignacionSolicitante.Fecha,
                s.AsignacionSolicitante.Turno.Nombre,
                s.IdAsignacionCompanero,
                s.AsignacionCompanero.IdEmpleado,
                s.AsignacionCompanero.Empleado.Nombres + " " + s.AsignacionCompanero.Empleado.Apellidos,
                s.AsignacionCompanero.Fecha,
                s.AsignacionCompanero.Turno.Nombre,
                s.Motivo,
                s.CodEstadoSolicitud,
                s.EstadoSolicitud.Nombre,
                s.SolicitadoEn,
                s.ResueltoPor,
                s.ResueltoEn,
                s.MotivoRechazo))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SolicitudCambioTurnoDto>(items, totalCount, pageNumber, pageSize));
    }
}

