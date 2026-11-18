using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// 3.4 CALENDARIO E HISTORIAL DEL EMPLEADO EN SESIÓN (RF-09)
// =====================================================================

/// <summary>
/// Sección 3.4: Calendario del empleado por día, semana o mes usando turnos.v_calendario:
/// SELECT tipo, detalle, inicio, fin FROM turnos.v_calendario WHERE id_empleado = $1 AND inicio &lt; $hasta AND fin &gt; $desde ORDER BY inicio;
/// </summary>
public sealed record ObtenerMiCalendarioQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta) : IRequest<Result<IReadOnlyList<EventoCalendarioEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMiCalendarioQueryHandler
    : IRequestHandler<ObtenerMiCalendarioQuery, Result<IReadOnlyList<EventoCalendarioEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;

    public ObtenerMiCalendarioQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
    }

    public async Task<Result<IReadOnlyList<EventoCalendarioEmpleadoDto>>> Handle(
        ObtenerMiCalendarioQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EventoCalendarioEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;

        var eventos = await _context.VistaCalendario
            .AsNoTracking()
            .Where(v => v.IdEmpleado == idEmpleado && v.Inicio < request.Hasta && v.Fin > request.Desde)
            .OrderBy(v => v.Inicio)
            .Select(v => new EventoCalendarioEmpleadoDto(v.Tipo, v.Detalle, v.Inicio, v.Fin))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<EventoCalendarioEmpleadoDto>>(eventos);
    }
}

/// <summary>
/// Sección 3.4: Historial de turnos pasados del empleado:
/// turnos.asignacion con fecha &lt; current_date, unida a turnos.turno.
/// </summary>
public sealed record ObtenerMiHistorialTurnosQuery(
    DateOnly? Desde = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<HistorialTurnoEmpleadoDto>>>, IRequireAuthentication;

public sealed class ObtenerMiHistorialTurnosQueryHandler
    : IRequestHandler<ObtenerMiHistorialTurnosQuery, Result<PagedResult<HistorialTurnoEmpleadoDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IConfiguration _configuration;

    public ObtenerMiHistorialTurnosQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService,
        IConfiguration configuration)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
        _configuration = configuration;
    }

    public async Task<Result<PagedResult<HistorialTurnoEmpleadoDto>>> Handle(
        ObtenerMiHistorialTurnosQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<PagedResult<HistorialTurnoEmpleadoDto>>(idEmpleadoResult.Error);
        }

        var idEmpleado = idEmpleadoResult.Value;
        var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);

        int pageNumber = Paginacion.NormalizarPagina(request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.Fecha < hoy &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada);

        if (request.Desde.HasValue)
        {
            query = query.Where(a => a.Fecha >= request.Desde.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.Fecha)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.IdAsignacion,
                a.Fecha,
                a.IdTurno,
                NombreTurno = a.Turno.Nombre,
                a.Turno.HoraInicio,
                a.Turno.HoraFin,
                a.Turno.MinutosDescanso,
                a.Periodo,
                a.CodEstadoAsignacion,
                a.Observacion
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new HistorialTurnoEmpleadoDto(
                r.IdAsignacion,
                r.Fecha,
                r.IdTurno,
                r.NombreTurno,
                r.HoraInicio,
                r.HoraFin,
                r.MinutosDescanso,
                r.Periodo.Inicio,
                r.Periodo.Fin,
                r.CodEstadoAsignacion,
                r.Observacion))
            .ToList();

        return Result.Success(new PagedResult<HistorialTurnoEmpleadoDto>(items, totalCount, pageNumber, pageSize));
    }
}

// =====================================================================
// 5. CALENDARIO GENERAL ADMINISTRATIVO (RF-09)
// =====================================================================

[RequirePermission(PermisosSistema.ReportesConsultar)]
public sealed record ObtenerCalendarioGeneralQuery(
    DateTimeOffset Desde,
    DateTimeOffset Hasta,
    long? IdEmpleado = null,
    int? IdArea = null,
    string? TipoEvento = null,
    int? IdTurno = null) : IRequest<Result<IReadOnlyList<EventoCalendarioGeneralDto>>>;

public sealed class ObtenerCalendarioGeneralQueryHandler
    : IRequestHandler<ObtenerCalendarioGeneralQuery, Result<IReadOnlyList<EventoCalendarioGeneralDto>>>
{
    private readonly GestionTurnosContext _context;

    public ObtenerCalendarioGeneralQueryHandler(GestionTurnosContext context) => _context = context;

    public async Task<Result<IReadOnlyList<EventoCalendarioGeneralDto>>> Handle(
        ObtenerCalendarioGeneralQuery request,
        CancellationToken cancellationToken)
    {
        // Filtro por tipo de turno: la vista no tiene id_turno, así que se consulta turnos.asignacion.
        // Solo devuelve eventos TURNO (los descansos, vacaciones y novedades no tienen tipo de turno).
        if (request.IdTurno.HasValue)
        {
            return await ObtenerTurnosPorTipoAsync(request, cancellationToken);
        }

        var query = from vc in _context.VistaCalendario.AsNoTracking()
                    join emp in _context.Empleados.AsNoTracking() on vc.IdEmpleado equals emp.IdEmpleado
                    join area in _context.Areas.AsNoTracking() on emp.IdArea equals area.IdArea
                    where vc.Inicio < request.Hasta && vc.Fin > request.Desde
                    select new
                    {
                        vc.IdEmpleado,
                        NombreEmpleado = emp.Nombres + " " + emp.Apellidos,
                        emp.IdArea,
                        NombreArea = area.Nombre,
                        vc.Tipo,
                        vc.Detalle,
                        vc.Inicio,
                        vc.Fin
                    };

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(x => x.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(x => x.IdArea == request.IdArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.TipoEvento))
        {
            var tipo = request.TipoEvento.Trim().ToUpperInvariant();
            query = query.Where(x => x.Tipo == tipo);
        }

        var items = await query
            .OrderBy(x => x.Inicio)
            .ThenBy(x => x.NombreEmpleado)
            .Select(x => new EventoCalendarioGeneralDto(
                x.IdEmpleado,
                x.NombreEmpleado,
                x.IdArea,
                x.NombreArea,
                x.Tipo,
                x.Detalle,
                x.Inicio,
                x.Fin))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<EventoCalendarioGeneralDto>>(items);
    }

    private async Task<Result<IReadOnlyList<EventoCalendarioGeneralDto>>> ObtenerTurnosPorTipoAsync(
        ObtenerCalendarioGeneralQuery request,
        CancellationToken cancellationToken)
    {
        const string tipoTurno = "TURNO";

        if (!string.IsNullOrWhiteSpace(request.TipoEvento) &&
            !string.Equals(request.TipoEvento.Trim(), tipoTurno, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success<IReadOnlyList<EventoCalendarioGeneralDto>>(Array.Empty<EventoCalendarioGeneralDto>());
        }

        // El periodo se guarda como texto (tstzrange): se acota por fecha en la base y se cruza en memoria.
        var fechaDesde = DateOnly.FromDateTime(request.Desde.UtcDateTime).AddDays(-1);
        var fechaHasta = DateOnly.FromDateTime(request.Hasta.UtcDateTime).AddDays(1);

        var query = _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.IdTurno == request.IdTurno!.Value &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= fechaDesde &&
                        a.Fecha <= fechaHasta);

        if (request.IdEmpleado.HasValue)
        {
            query = query.Where(a => a.IdEmpleado == request.IdEmpleado.Value);
        }

        if (request.IdArea.HasValue)
        {
            query = query.Where(a => a.Empleado.IdArea == request.IdArea.Value);
        }

        var filas = await query
            .Select(a => new
            {
                a.IdEmpleado,
                NombreEmpleado = a.Empleado.Nombres + " " + a.Empleado.Apellidos,
                a.Empleado.IdArea,
                NombreArea = a.Empleado.Area.Nombre,
                NombreTurno = a.Turno.Nombre,
                a.Periodo
            })
            .ToListAsync(cancellationToken);

        var items = filas
            .Where(f => f.Periodo.Inicio < request.Hasta && f.Periodo.Fin > request.Desde)
            .OrderBy(f => f.Periodo.Inicio)
            .ThenBy(f => f.NombreEmpleado)
            .Select(f => new EventoCalendarioGeneralDto(
                f.IdEmpleado,
                f.NombreEmpleado,
                f.IdArea,
                f.NombreArea,
                tipoTurno,
                f.NombreTurno,
                f.Periodo.Inicio,
                f.Periodo.Fin))
            .ToList();

        return Result.Success<IReadOnlyList<EventoCalendarioGeneralDto>>(items);
    }
}

