using FluentValidation;
using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Domain.ValueObjects;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using GestionTurnos.Api.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// 6.1 PROGRAMAR, MODIFICAR, PUBLICAR Y ANULAR TURNOS (RF-04, CU-01, CU-04)
// =====================================================================

public sealed record ItemProgramacionTurnoDto(
    long IdEmpleado,
    int IdTurno,
    DateOnly Fecha,
    string? Observacion = null);

/// <summary>
/// Sección 6.1: Programación por día, semana, mes o grupo.
/// Un INSERT por empleado y fecha, todos en una transacción; si uno falla se revierte todo y se informa cuál falló.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ProgramarTurnosCommand(
    IReadOnlyList<ItemProgramacionTurnoDto> Asignaciones) : IRequest<Result<IReadOnlyList<long>>>;

public sealed class ProgramarTurnosCommandValidator : AbstractValidator<ProgramarTurnosCommand>
{
    public ProgramarTurnosCommandValidator()
    {
        RuleFor(x => x.Asignaciones)
            .NotEmpty().WithMessage("Debe incluir al menos una asignación de turno para programar.");

        RuleForEach(x => x.Asignaciones).ChildRules(item =>
        {
            item.RuleFor(i => i.IdEmpleado).GreaterThan(0);
            item.RuleFor(i => i.IdTurno).GreaterThan(0);
            item.RuleFor(i => i.Fecha).NotEmpty();
            item.RuleFor(i => i.Observacion).MaximumLength(300).When(i => !string.IsNullOrWhiteSpace(i.Observacion));
        });
    }
}

public sealed class ProgramarTurnosCommandHandler
    : IRequestHandler<ProgramarTurnosCommand, Result<IReadOnlyList<long>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public ProgramarTurnosCommandHandler(
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

    public async Task<Result<IReadOnlyList<long>>> Handle(
        ProgramarTurnosCommand request,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;
        var idsCreados = new List<long>(request.Asignaciones.Count);

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Bloquea los empleados (en orden) para que otra programación simultánea no pase la misma revisión.
            await _revisionConflictos.BloquearEmpleadosAsync(
                request.Asignaciones.Select(a => a.IdEmpleado), cancellationToken);

            foreach (var item in request.Asignaciones)
            {
                // Cada asignación se guarda antes de revisar la siguiente: la revisión ve las anteriores del lote.
                var revision = await _revisionConflictos.RevisarAsync(
                    item.IdEmpleado, item.IdTurno, item.Fecha, idAsignacionActual: null, cancellationToken);

                Error? error = revision.IsFailure
                    ? revision.Error
                    : revision.Value.PuedeAsignarse ? null : RevisionConflictosTurno.ErrorDeConflictos(revision.Value);

                if (error is not null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<IReadOnlyList<long>>(new Error(
                        error.Code,
                        $"Error al programar empleado #{item.IdEmpleado} en la fecha {item.Fecha:yyyy-MM-dd}: {error.Message} No se guardó ninguna asignación.",
                        error.Type));
                }

                var asignacion = new Asignacion
                {
                    IdEmpleado = item.IdEmpleado,
                    IdTurno = item.IdTurno,
                    Fecha = item.Fecha,
                    Periodo = new PeriodoRango(revision.Value.PeriodoInicio, revision.Value.PeriodoFin),
                    CodEstadoAsignacion = EstadosAsignacion.Programada,
                    Observacion = string.IsNullOrWhiteSpace(item.Observacion) ? null : item.Observacion.Trim(),
                    CreadoPor = adminId,
                    CreadoEn = DateTimeOffset.UtcNow
                };

                await _context.Asignaciones.AddAsync(asignacion, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                idsCreados.Add(asignacion.IdAsignacion);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success<IReadOnlyList<long>>(idsCreados);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<IReadOnlyList<long>>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 9.1 REVISIÓN PREVIA DE CONFLICTOS (permiso: turnos.programar)
// =====================================================================

/// <summary>
/// Consulta previa (sin guardar nada): informa cruces con otros turnos, vacaciones aprobadas, novedades aprobadas
/// y días de descanso, y las validaciones por parámetro (descanso mínimo, horas máximas por día y semana)
/// indicando si bloquean según CONFLICTO_BLOQUEA.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record RevisarConflictosAsignacionQuery(
    long IdEmpleado,
    int IdTurno,
    DateOnly Fecha,
    long? IdAsignacionActual = null) : IRequest<Result<RevisionConflictosTurnoDto>>;

public sealed class RevisarConflictosAsignacionQueryValidator : AbstractValidator<RevisarConflictosAsignacionQuery>
{
    public RevisarConflictosAsignacionQueryValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.IdTurno).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
    }
}

public sealed class RevisarConflictosAsignacionQueryHandler
    : IRequestHandler<RevisarConflictosAsignacionQuery, Result<RevisionConflictosTurnoDto>>
{
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public RevisarConflictosAsignacionQueryHandler(IRevisionConflictosTurno revisionConflictos)
        => _revisionConflictos = revisionConflictos;

    public Task<Result<RevisionConflictosTurnoDto>> Handle(
        RevisarConflictosAsignacionQuery request,
        CancellationToken cancellationToken)
        => _revisionConflictos.RevisarAsync(
            request.IdEmpleado, request.IdTurno, request.Fecha, request.IdAsignacionActual, cancellationToken);
}

// =====================================================================
// 6.1 MODIFICAR ASIGNACIÓN DE TURNO (CU-04, RG-04)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ModificarAsignacionTurnoCommand(
    long IdAsignacion,
    int IdTurno,
    DateOnly Fecha,
    string? Observacion) : IRequest<Result>;

public sealed class ModificarAsignacionTurnoCommandValidator : AbstractValidator<ModificarAsignacionTurnoCommand>
{
    public ModificarAsignacionTurnoCommandValidator()
    {
        RuleFor(x => x.IdAsignacion).GreaterThan(0);
        RuleFor(x => x.IdTurno).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
        RuleFor(x => x.Observacion).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Observacion));
    }
}

public sealed class ModificarAsignacionTurnoCommandHandler : IRequestHandler<ModificarAsignacionTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public ModificarAsignacionTurnoCommandHandler(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        IRevisionConflictosTurno revisionConflictos)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _revisionConflictos = revisionConflictos;
    }

    public async Task<Result> Handle(ModificarAsignacionTurnoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var idEmpleado = await _context.Asignaciones
                .AsNoTracking()
                .Where(a => a.IdAsignacion == request.IdAsignacion)
                .Select(a => (long?)a.IdEmpleado)
                .FirstOrDefaultAsync(cancellationToken);

            if (idEmpleado is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(AsignacionesTurno.NoEncontrada);
            }

            // Mismo orden de bloqueo que la programación: empleado y después la asignación.
            await _revisionConflictos.BloquearEmpleadosAsync([idEmpleado.Value], cancellationToken);

            var asignacion = await _context.Asignaciones
                .FirstAsync(a => a.IdAsignacion == request.IdAsignacion, cancellationToken);

            var editable = await AsignacionesTurno.ValidarEditableAsync(_context, asignacion, "modificar", cancellationToken);
            if (editable.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return editable;
            }

            // Mismas reglas que al asignar, excluyendo la propia asignación.
            var revision = await _revisionConflictos.RevisarAsync(
                asignacion.IdEmpleado, request.IdTurno, request.Fecha, asignacion.IdAsignacion, cancellationToken);
            if (revision.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return revision;
            }

            if (!revision.Value.PuedeAsignarse)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(RevisionConflictosTurno.ErrorDeConflictos(revision.Value));
            }

            // El disparador tg_aud_asignacion guarda el valor anterior y el nuevo en la bitácora.
            asignacion.IdTurno = request.IdTurno;
            asignacion.Fecha = request.Fecha;
            asignacion.Periodo = new PeriodoRango(revision.Value.PeriodoInicio, revision.Value.PeriodoFin);
            asignacion.Observacion = string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim();

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
/// Sección 9.4: muestra el cambio antes de aplicarlo (actual contra nuevo) y los conflictos de la propuesta.
/// No guarda nada.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record PrevisualizarModificacionAsignacionQuery(
    long IdAsignacion,
    int IdTurno,
    DateOnly Fecha) : IRequest<Result<PrevisualizacionModificacionAsignacionDto>>;

public sealed class PrevisualizarModificacionAsignacionQueryValidator
    : AbstractValidator<PrevisualizarModificacionAsignacionQuery>
{
    public PrevisualizarModificacionAsignacionQueryValidator()
    {
        RuleFor(x => x.IdAsignacion).GreaterThan(0);
        RuleFor(x => x.IdTurno).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
    }
}

public sealed class PrevisualizarModificacionAsignacionQueryHandler
    : IRequestHandler<PrevisualizarModificacionAsignacionQuery, Result<PrevisualizacionModificacionAsignacionDto>>
{
    private readonly GestionTurnosContext _context;
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public PrevisualizarModificacionAsignacionQueryHandler(
        GestionTurnosContext context,
        IRevisionConflictosTurno revisionConflictos)
    {
        _context = context;
        _revisionConflictos = revisionConflictos;
    }

    public async Task<Result<PrevisualizacionModificacionAsignacionDto>> Handle(
        PrevisualizarModificacionAsignacionQuery request,
        CancellationToken cancellationToken)
    {
        var asignacion = await _context.Asignaciones
            .AsNoTracking()
            .Include(a => a.Turno)
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (asignacion is null)
        {
            return Result.Failure<PrevisualizacionModificacionAsignacionDto>(AsignacionesTurno.NoEncontrada);
        }

        var editable = await AsignacionesTurno.ValidarEditableAsync(_context, asignacion, "modificar", cancellationToken);
        if (editable.IsFailure)
        {
            return Result.Failure<PrevisualizacionModificacionAsignacionDto>(editable.Error);
        }

        var revision = await _revisionConflictos.RevisarAsync(
            asignacion.IdEmpleado, request.IdTurno, request.Fecha, asignacion.IdAsignacion, cancellationToken);
        if (revision.IsFailure)
        {
            return Result.Failure<PrevisualizacionModificacionAsignacionDto>(revision.Error);
        }

        var nuevo = revision.Value;
        return Result.Success(new PrevisualizacionModificacionAsignacionDto(
            asignacion.IdAsignacion,
            asignacion.IdEmpleado,
            new DatosAsignacionDto(
                asignacion.IdTurno,
                asignacion.Turno.Nombre,
                asignacion.Fecha,
                asignacion.Periodo.Inicio,
                asignacion.Periodo.Fin),
            new DatosAsignacionDto(nuevo.IdTurno, nuevo.NombreTurno, nuevo.Fecha, nuevo.PeriodoInicio, nuevo.PeriodoFin),
            nuevo.PuedeAsignarse,
            nuevo.Conflictos));
    }
}

/// <summary>
/// Reglas comunes para modificar o anular una asignación (secciones 9.4 y 9.5).
/// </summary>
internal static class AsignacionesTurno
{
    public static readonly Error NoEncontrada = new(
        "Turnos.AsignacionNoEncontrada", "La asignación de turno no existe.", ErrorType.NotFound);

    /// <summary>
    /// Una asignación anulada o con asistencia registrada no se modifica ni se anula.
    /// </summary>
    public static async Task<Result> ValidarEditableAsync(
        GestionTurnosContext context,
        Asignacion asignacion,
        string accion,
        CancellationToken cancellationToken)
    {
        if (asignacion.CodEstadoAsignacion == EstadosAsignacion.Anulada)
        {
            return Result.Failure(new Error(
                "Turnos.AsignacionAnulada",
                $"No se puede {accion} una asignación anulada.",
                ErrorType.Conflict));
        }

        bool tieneAsistencia = await context.RegistrosAsistencia
            .AsNoTracking()
            .AnyAsync(r => r.IdAsignacion == asignacion.IdAsignacion, cancellationToken);

        if (tieneAsistencia)
        {
            return Result.Failure(new Error(
                "Turnos.AsignacionConAsistencia",
                $"No se puede {accion} una asignación que ya tiene asistencia registrada.",
                ErrorType.Conflict));
        }

        return Result.Success();
    }
}

// =====================================================================
// 6.1 ANULAR Y PUBLICAR ASIGNACIONES DE TURNO
// =====================================================================

/// <summary>
/// Sección 9.5: Anular un turno con motivo obligatorio = cod_estado_asignacion = 'ANULADA'. No se borra.
/// Una asignación con registro de asistencia no se anula ni se cambia de empleado.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record AnularAsignacionTurnoCommand(
    long IdAsignacion,
    string? Observacion) : IRequest<Result>;

public sealed class AnularAsignacionTurnoCommandValidator : AbstractValidator<AnularAsignacionTurnoCommand>
{
    public AnularAsignacionTurnoCommandValidator()
    {
        RuleFor(x => x.IdAsignacion).GreaterThan(0);
        RuleFor(x => x.Observacion)
            .NotEmpty().WithMessage("El motivo de la anulación es obligatorio.")
            .MaximumLength(300);
    }
}

public sealed class AnularAsignacionTurnoCommandHandler : IRequestHandler<AnularAsignacionTurnoCommand, Result>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AnularAsignacionTurnoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AnularAsignacionTurnoCommand request, CancellationToken cancellationToken)
    {
        var asignacion = await _context.Asignaciones
            .FirstOrDefaultAsync(a => a.IdAsignacion == request.IdAsignacion, cancellationToken);

        if (asignacion is null)
        {
            return Result.Failure(AsignacionesTurno.NoEncontrada);
        }

        var editable = await AsignacionesTurno.ValidarEditableAsync(_context, asignacion, "anular", cancellationToken);
        if (editable.IsFailure)
        {
            return editable;
        }

        // No se borra: queda ANULADA con su motivo (el disparador audita el cambio).
        asignacion.CodEstadoAsignacion = EstadosAsignacion.Anulada;
        asignacion.Observacion = request.Observacion!.Trim();

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

/// <summary>
/// Sección 6.1: Publicar programación = cod_estado_asignacion = 'CONFIRMADA'.
/// </summary>
[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record PublicarAsignacionesTurnoCommand(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    int? IdArea = null,
    long? IdEmpleado = null) : IRequest<Result<int>>;

public sealed class PublicarAsignacionesTurnoCommandValidator : AbstractValidator<PublicarAsignacionesTurnoCommand>
{
    public PublicarAsignacionesTurnoCommandValidator()
    {
        RuleFor(x => x.FechaHasta)
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha final debe ser mayor o igual a la fecha inicial.");
    }
}

public sealed class PublicarAsignacionesTurnoCommandHandler
    : IRequestHandler<PublicarAsignacionesTurnoCommand, Result<int>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public PublicarAsignacionesTurnoCommandHandler(GestionTurnosContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(PublicarAsignacionesTurnoCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Asignaciones
            .Where(a => a.Fecha >= request.FechaDesde &&
                        a.Fecha <= request.FechaHasta &&
                        a.CodEstadoAsignacion == EstadosAsignacion.Programada);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        var asignaciones = await query.ToListAsync(cancellationToken);
        if (asignaciones.Count == 0)
        {
            return Result.Success(0);
        }

        foreach (var asignacion in asignaciones)
        {
            asignacion.CodEstadoAsignacion = EstadosAsignacion.Confirmada;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(asignaciones.Count);
        }
        catch (Exception ex)
        {
            return Result.Failure<int>(DatabaseErrorTranslator.Translate(ex));
        }
    }
}

// =====================================================================
// 6.1 PROGRAMAR DÍA DE DESCANSO (turnos.dia_descanso)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ProgramarDiaDescansoCommand(
    long IdEmpleado,
    DateOnly Fecha) : IRequest<Result<long>>;

public sealed class ProgramarDiaDescansoCommandValidator : AbstractValidator<ProgramarDiaDescansoCommand>
{
    public ProgramarDiaDescansoCommandValidator()
    {
        RuleFor(x => x.IdEmpleado).GreaterThan(0);
        RuleFor(x => x.Fecha).NotEmpty();
    }
}

public sealed class ProgramarDiaDescansoCommandHandler : IRequestHandler<ProgramarDiaDescansoCommand, Result<long>>
{
    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRevisionConflictosTurno _revisionConflictos;

    public ProgramarDiaDescansoCommandHandler(
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

    public async Task<Result<long>> Handle(ProgramarDiaDescansoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Mismo bloqueo que la programación: un turno y un descanso simultáneos no pasan ambos la validación.
            await _revisionConflictos.BloquearEmpleadosAsync([request.IdEmpleado], cancellationToken);

            var resultado = await RegistrarAsync(request, cancellationToken);
            if (resultado.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return resultado;
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return resultado;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<long>(DatabaseErrorTranslator.Translate(ex));
        }
    }

    private async Task<Result<long>> RegistrarAsync(ProgramarDiaDescansoCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId!.Value;

        bool empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.IdEmpleado == request.IdEmpleado && e.CodEstadoEmpleado == EstadosEmpleado.Activo, cancellationToken);

        if (!empleadoActivo)
        {
            return Result.Failure<long>(new Error(
                "Turnos.EmpleadoNoActivo",
                "Solo se pueden programar días de descanso a empleados activos.",
                ErrorType.Failure));
        }

        // Sección 6.1: Día de descanso: INSERT en turnos.dia_descanso; valida antes que no haya turno ese día.
        bool tieneTurnoEseDia = await _context.Asignaciones
            .AsNoTracking()
            .AnyAsync(a => a.IdEmpleado == request.IdEmpleado &&
                           a.Fecha == request.Fecha &&
                           a.CodEstadoAsignacion != EstadosAsignacion.Anulada, cancellationToken);

        if (tieneTurnoEseDia)
        {
            return Result.Failure<long>(new Error(
                "Turnos.EmpleadoTieneTurnoEnDescanso",
                $"El empleado ya tiene un turno programado en la fecha {request.Fecha:yyyy-MM-dd}. Anule el turno antes de asignar descanso.",
                ErrorType.Conflict));
        }

        var descanso = new DiaDescanso
        {
            IdEmpleado = request.IdEmpleado,
            Fecha = request.Fecha,
            CreadoPor = adminId,
            CreadoEn = DateTimeOffset.UtcNow
        };

        await _context.DiasDescanso.AddAsync(descanso, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(descanso.IdDiaDescanso);
    }
}

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ListarDiasDescansoQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null) : IRequest<Result<IReadOnlyList<DiaDescansoDto>>>;

public sealed class ListarDiasDescansoQueryHandler
    : IRequestHandler<ListarDiasDescansoQuery, Result<IReadOnlyList<DiaDescansoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarDiasDescansoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<DiaDescansoDto>>> Handle(
        ListarDiasDescansoQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.DiasDescanso
            .AsNoTracking()
            .Where(d => d.Fecha >= request.FechaDesde && d.Fecha <= request.FechaHasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(d => d.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(d => d.Empleado.IdArea == request.IdArea.Value);
        }

        var items = await query
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.Empleado.Apellidos)
            .Select(d => new DiaDescansoDto(
                d.IdDiaDescanso,
                d.IdEmpleado,
                d.Empleado.Nombres + " " + d.Empleado.Apellidos,
                d.Fecha,
                d.CreadoPor,
                d.CreadoEn))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<DiaDescansoDto>>(items);
    }
}

// =====================================================================
// LISTAR ASIGNACIONES DE TURNO (ADMINISTRADOR)
// =====================================================================

[RequirePermission(PermisosSistema.TurnosProgramar)]
public sealed record ListarAsignacionesTurnoQuery(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    int? IdTurno = null,
    string? CodEstadoAsignacion = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<AsignacionTurnoDto>>>;

public sealed class ListarAsignacionesTurnoQueryHandler
    : IRequestHandler<ListarAsignacionesTurnoQuery, Result<PagedResult<AsignacionTurnoDto>>>
{
    private readonly GestionTurnosContext _context;

    public ListarAsignacionesTurnoQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<PagedResult<AsignacionTurnoDto>>> Handle(
        ListarAsignacionesTurnoQuery request,
        CancellationToken cancellationToken)
    {
        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.Fecha >= request.FechaDesde && a.Fecha <= request.FechaHasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        if (request.IdTurno.HasValue)
        {
            query = query.Where(a => a.IdTurno == request.IdTurno.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CodEstadoAsignacion))
        {
            var estado = request.CodEstadoAsignacion.Trim().ToUpperInvariant();
            query = query.Where(a => a.CodEstadoAsignacion == estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(a => a.Fecha)
            .ThenBy(a => a.Empleado.Apellidos)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.IdAsignacion,
                a.IdEmpleado,
                NombreEmpleado = a.Empleado.Nombres + " " + a.Empleado.Apellidos,
                a.Empleado.NumeroDocumento,
                a.Empleado.IdArea,
                NombreArea = a.Empleado.Area.Nombre,
                a.IdTurno,
                NombreTurno = a.Turno.Nombre,
                ColorTurno = a.Turno.Color,
                a.Fecha,
                a.Periodo,
                a.CodEstadoAsignacion,
                NombreEstadoAsignacion = a.EstadoAsignacion.Nombre,
                a.Observacion,
                a.CreadoPor,
                a.CreadoEn
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AsignacionTurnoDto(
                r.IdAsignacion,
                r.IdEmpleado,
                r.NombreEmpleado,
                r.NumeroDocumento,
                r.IdArea,
                r.NombreArea,
                r.IdTurno,
                r.NombreTurno,
                r.ColorTurno,
                r.Fecha,
                r.Periodo.Inicio,
                r.Periodo.Fin,
                r.CodEstadoAsignacion,
                r.NombreEstadoAsignacion,
                r.Observacion,
                r.CreadoPor,
                r.CreadoEn))
            .ToList();

        return Result.Success(new PagedResult<AsignacionTurnoDto>(items, totalCount, pageNumber, pageSize));
    }
}

