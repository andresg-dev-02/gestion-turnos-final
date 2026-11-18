using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// 3.9 APOYO A LA SOLICITUD DE CAMBIO DE TURNO (OPC-03)
// El empleado necesita los id_asignacion de sus turnos futuros y de los
// turnos de sus compañeros para armar la solicitud de intercambio.
// =====================================================================

/// <summary>
/// Turno futuro (no anulado) que puede participar en un intercambio.
/// </summary>
public sealed record TurnoIntercambiableDto(
    long IdAsignacion,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly Fecha,
    int IdTurno,
    string NombreTurno,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string? ColorTurno,
    string CodEstadoAsignacion);

/// <summary>
/// Turnos del empleado en sesión desde hoy hasta <c>Dias</c> días adelante.
/// </summary>
public sealed record ObtenerMisProximosTurnosQuery(int Dias = 60)
    : IRequest<Result<IReadOnlyList<TurnoIntercambiableDto>>>, IRequireAuthentication;

public sealed class ObtenerMisProximosTurnosQueryHandler
    : IRequestHandler<ObtenerMisProximosTurnosQuery, Result<IReadOnlyList<TurnoIntercambiableDto>>>
{
    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IConfiguration _configuration;

    public ObtenerMisProximosTurnosQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService,
        IConfiguration configuration)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
        _configuration = configuration;
    }

    public async Task<Result<IReadOnlyList<TurnoIntercambiableDto>>> Handle(
        ObtenerMisProximosTurnosQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TurnoIntercambiableDto>>(idEmpleadoResult.Error);
        }

        var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
        var hasta = hoy.AddDays(Math.Clamp(request.Dias, 1, 120));

        var items = await TurnosIntercambiables.Consultar(
            _context.Asignaciones
                .AsNoTracking()
                .Where(a => a.IdEmpleado == idEmpleadoResult.Value &&
                            a.Fecha >= hoy && a.Fecha <= hasta &&
                            a.CodEstadoAsignacion != EstadosAsignacion.Anulada),
            cancellationToken);

        return Result.Success(items);
    }
}

/// <summary>
/// Turnos futuros de los compañeros activos de la misma área del empleado en sesión.
/// </summary>
public sealed record ObtenerTurnosCompanerosQuery(DateOnly Desde, DateOnly Hasta)
    : IRequest<Result<IReadOnlyList<TurnoIntercambiableDto>>>, IRequireAuthentication;

public sealed class ObtenerTurnosCompanerosQueryHandler
    : IRequestHandler<ObtenerTurnosCompanerosQuery, Result<IReadOnlyList<TurnoIntercambiableDto>>>
{
    private const int MaximoDiasRango = 62;

    private readonly GestionTurnosContext _context;
    private readonly IEmpleadoSesionService _empleadoSesionService;
    private readonly IConfiguration _configuration;

    public ObtenerTurnosCompanerosQueryHandler(
        GestionTurnosContext context,
        IEmpleadoSesionService empleadoSesionService,
        IConfiguration configuration)
    {
        _context = context;
        _empleadoSesionService = empleadoSesionService;
        _configuration = configuration;
    }

    public async Task<Result<IReadOnlyList<TurnoIntercambiableDto>>> Handle(
        ObtenerTurnosCompanerosQuery request,
        CancellationToken cancellationToken)
    {
        var idEmpleadoResult = await _empleadoSesionService.ObtenerIdEmpleadoActivoAsync(cancellationToken);
        if (idEmpleadoResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TurnoIntercambiableDto>>(idEmpleadoResult.Error);
        }

        if (request.Hasta < request.Desde || request.Hasta.DayNumber - request.Desde.DayNumber > MaximoDiasRango)
        {
            return Result.Failure<IReadOnlyList<TurnoIntercambiableDto>>(new Error(
                "Turnos.RangoInvalido",
                $"El rango de fechas debe ser válido y de máximo {MaximoDiasRango} días.",
                ErrorType.Validation));
        }

        var idEmpleado = idEmpleadoResult.Value;
        var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
        var desde = request.Desde < hoy ? hoy : request.Desde;

        var idArea = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.IdEmpleado == idEmpleado)
            .Select(e => e.IdArea)
            .FirstAsync(cancellationToken);

        var items = await TurnosIntercambiables.Consultar(
            _context.Asignaciones
                .AsNoTracking()
                .Where(a => a.IdEmpleado != idEmpleado &&
                            a.Empleado.IdArea == idArea &&
                            a.Empleado.CodEstadoEmpleado == EstadosEmpleado.Activo &&
                            a.Fecha >= desde && a.Fecha <= request.Hasta &&
                            a.CodEstadoAsignacion != EstadosAsignacion.Anulada),
            cancellationToken);

        return Result.Success(items);
    }
}

internal static class TurnosIntercambiables
{
    public static async Task<IReadOnlyList<TurnoIntercambiableDto>> Consultar(
        IQueryable<Domain.Entities.Turnos.Asignacion> query,
        CancellationToken cancellationToken)
    {
        return await query
            .OrderBy(a => a.Fecha)
            .ThenBy(a => a.Turno.HoraInicio)
            .Take(300)
            .Select(a => new TurnoIntercambiableDto(
                a.IdAsignacion,
                a.IdEmpleado,
                a.Empleado.Nombres + " " + a.Empleado.Apellidos,
                a.Fecha,
                a.IdTurno,
                a.Turno.Nombre,
                a.Turno.HoraInicio,
                a.Turno.HoraFin,
                a.Turno.Color,
                a.CodEstadoAsignacion))
            .ToListAsync(cancellationToken);
    }
}
