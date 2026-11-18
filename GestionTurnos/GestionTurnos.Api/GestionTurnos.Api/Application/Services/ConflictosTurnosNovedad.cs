using GestionTurnos.Api.Application.DTOs.Novedades;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.Services;

/// <summary>
/// Regla de las secciones 11 y 12 para aprobar vacaciones o permisos sobre turnos ya programados.
/// La base impide programar turnos sobre vacaciones o novedades aprobadas, pero no al revés,
/// por eso el backend revisa los turnos antes de aprobar:
/// <list type="bullet">
/// <item>Si algún turno que choca ya tiene asistencia, no se aprueba.</item>
/// <item>CONFLICTO_BLOQUEA verdadero: no se aprueba si hay turnos que chocan.</item>
/// <item>CONFLICTO_BLOQUEA falso: se pide confirmación y, confirmada, los turnos se anulan en la misma transacción.</item>
/// </list>
/// </summary>
public static class ConflictosTurnosNovedad
{
    /// <summary>
    /// Turnos no anulados del empleado cuyas fechas caen en el rango de unas vacaciones.
    /// </summary>
    public static Task<List<Asignacion>> TurnosEnRangoVacacionesAsync(
        GestionTurnosContext context,
        long idEmpleado,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = context.Asignaciones
            .Include(a => a.Turno)
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= fechaInicio &&
                        a.Fecha <= fechaFin);

        return (asTracking ? query : query.AsNoTracking()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Turnos no anulados del empleado cuyo periodo se cruza con el intervalo de una novedad.
    /// El periodo se guarda como texto (tstzrange): se acota por fecha en la base y se cruza en memoria.
    /// </summary>
    public static async Task<List<Asignacion>> TurnosQueSeCruzanAsync(
        GestionTurnosContext context,
        long idEmpleado,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        CancellationToken cancellationToken)
    {
        var fechaDesde = DateOnly.FromDateTime(inicio.UtcDateTime).AddDays(-1);
        var fechaHasta = DateOnly.FromDateTime(fin.UtcDateTime).AddDays(1);

        var candidatos = await context.Asignaciones
            .Include(a => a.Turno)
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= fechaDesde &&
                        a.Fecha <= fechaHasta)
            .ToListAsync(cancellationToken);

        return candidatos.Where(a => a.Periodo.SeCruzaCon(inicio, fin)).ToList();
    }

    /// <summary>
    /// Arma el detalle de los turnos que chocan (para la consulta previa), indicando cuáles tienen asistencia.
    /// </summary>
    public static async Task<IReadOnlyList<TurnoEnConflictoDto>> DetallarAsync(
        GestionTurnosContext context,
        IReadOnlyCollection<Asignacion> turnos,
        CancellationToken cancellationToken)
    {
        var ids = turnos.Select(t => t.IdAsignacion).ToList();
        var conAsistencia = await context.RegistrosAsistencia
            .AsNoTracking()
            .Where(r => r.IdAsignacion != null && ids.Contains(r.IdAsignacion.Value))
            .Select(r => r.IdAsignacion!.Value)
            .ToListAsync(cancellationToken);

        return turnos
            .OrderBy(t => t.Periodo.Inicio)
            .Select(t => new TurnoEnConflictoDto(
                t.IdAsignacion,
                t.Fecha,
                t.IdTurno,
                t.Turno.Nombre,
                t.Periodo.Inicio,
                t.Periodo.Fin,
                t.CodEstadoAsignacion,
                conAsistencia.Contains(t.IdAsignacion)))
            .ToList();
    }

    /// <summary>
    /// Aplica la regla sobre los turnos (rastreados) que chocan. Si la aprobación procede y hay turnos,
    /// los anula con <paramref name="observacionAnulacion"/> (el disparador audita cada cambio).
    /// </summary>
    /// <param name="prefijoError">"Vacaciones" o "Novedades": prefijo de los códigos de error.</param>
    public static async Task<Result> AplicarReglaAsync(
        GestionTurnosContext context,
        IParametroService parametroService,
        IReadOnlyList<Asignacion> turnosQueChocan,
        DateOnly fechaParametro,
        bool confirmarAnulacion,
        string prefijoError,
        string observacionAnulacion,
        CancellationToken cancellationToken)
    {
        if (turnosQueChocan.Count == 0)
        {
            return Result.Success();
        }

        var fechas = string.Join(", ", turnosQueChocan.Select(t => t.Fecha.ToString("yyyy-MM-dd")).Distinct());
        var ids = turnosQueChocan.Select(t => t.IdAsignacion).ToList();

        // 1. Un turno con asistencia no se anula: la aprobación no procede con ningún valor del parámetro.
        bool algunoConAsistencia = await context.RegistrosAsistencia
            .AsNoTracking()
            .AnyAsync(r => r.IdAsignacion != null && ids.Contains(r.IdAsignacion.Value), cancellationToken);
        if (algunoConAsistencia)
        {
            return Result.Failure(new Error(
                $"{prefijoError}.TurnoConAsistencia",
                $"No se puede aprobar: uno o más turnos que chocan ({fechas}) ya tienen asistencia registrada.",
                ErrorType.Conflict));
        }

        // 2. CONFLICTO_BLOQUEA verdadero: no se aprueba.
        bool conflictoBloquea = await parametroService.ObtenerBooleanoAsync(
            ParametrosCodigos.ConflictoBloquea, fechaParametro, cancellationToken);
        if (conflictoBloquea)
        {
            return Result.Failure(new Error(
                $"{prefijoError}.ConflictoConTurnos",
                $"No se puede aprobar porque el empleado tiene turnos programados que chocan en las fechas: {fechas} (parámetro CONFLICTO_BLOQUEA activo).",
                ErrorType.Conflict));
        }

        // 3. CONFLICTO_BLOQUEA falso: se pide confirmación y se anulan.
        if (!confirmarAnulacion)
        {
            return Result.Failure(new Error(
                $"{prefijoError}.ConfirmacionRequerida",
                $"El empleado tiene turnos programados que chocan en las fechas: {fechas}. Confirme para aprobar y anular esos turnos en la misma transacción.",
                ErrorType.Conflict));
        }

        foreach (var turno in turnosQueChocan)
        {
            turno.CodEstadoAsignacion = EstadosAsignacion.Anulada;
            turno.Observacion = observacionAnulacion;
        }

        return Result.Success();
    }
}
