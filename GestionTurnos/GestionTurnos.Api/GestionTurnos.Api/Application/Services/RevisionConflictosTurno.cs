using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.DTOs.Turnos;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Domain.ValueObjects;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.Services;

/// <summary>
/// Revisión de conflictos de una asignación de turno (sección 9.1). Única fuente de las reglas que usan
/// la consulta previa, la asignación individual o masiva y la modificación:
/// <list type="bullet">
/// <item>Siempre bloquean (también los bloquea la base con 23P01/23514): cruce con otro turno,
/// vacaciones aprobadas, novedad aprobada y día de descanso.</item>
/// <item>Bloquean solo si el parámetro CONFLICTO_BLOQUEA es verdadero; si no, son advertencias:
/// descanso mínimo entre turnos y horas máximas por día y por semana.</item>
/// </list>
/// Todos los límites se leen de parametrizacion.parametro_valor vigente en la fecha del turno.
/// </summary>
public interface IRevisionConflictosTurno
{
    /// <summary>
    /// Construye el periodo del turno para la fecha (RG-05) y devuelve todos los conflictos encontrados.
    /// Falla si el empleado no está activo o el turno no existe o está inactivo.
    /// </summary>
    Task<Result<RevisionConflictosTurnoDto>> RevisarAsync(
        long idEmpleado,
        int idTurno,
        DateOnly fecha,
        long? idAsignacionActual,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bloquea las filas de los empleados en orden ascendente dentro de la transacción en curso,
    /// para que dos programaciones simultáneas del mismo empleado no pasen ambas la revisión.
    /// </summary>
    Task BloquearEmpleadosAsync(IEnumerable<long> idsEmpleado, CancellationToken cancellationToken);
}

public sealed class RevisionConflictosTurno : IRevisionConflictosTurno
{
    public const string TipoCruceTurno = "CRUCE_TURNO";
    public const string TipoVacaciones = "VACACIONES";
    public const string TipoNovedad = "NOVEDAD";
    public const string TipoDescanso = "DESCANSO";
    public const string TipoDescansoMinimo = "DESCANSO_MINIMO";
    public const string TipoJornadaDia = "JORNADA_DIA";
    public const string TipoJornadaSemana = "JORNADA_SEMANA";

    private readonly GestionTurnosContext _context;
    private readonly IParametroService _parametroService;
    private readonly IConfiguration _configuration;

    public RevisionConflictosTurno(
        GestionTurnosContext context,
        IParametroService parametroService,
        IConfiguration configuration)
    {
        _context = context;
        _parametroService = parametroService;
        _configuration = configuration;
    }

    public async Task BloquearEmpleadosAsync(IEnumerable<long> idsEmpleado, CancellationToken cancellationToken)
    {
        foreach (var id in idsEmpleado.Distinct().Order())
        {
            await _context.BloquearPorIdAsync<Empleado>(id, cancellationToken);
        }
    }

    public async Task<Result<RevisionConflictosTurnoDto>> RevisarAsync(
        long idEmpleado,
        int idTurno,
        DateOnly fecha,
        long? idAsignacionActual,
        CancellationToken cancellationToken)
    {
        // 1. Solo empleados ACTIVO
        bool empleadoActivo = await _context.Empleados
            .AsNoTracking()
            .AnyAsync(e => e.IdEmpleado == idEmpleado && e.CodEstadoEmpleado == EstadosEmpleado.Activo, cancellationToken);

        if (!empleadoActivo)
        {
            return Result.Failure<RevisionConflictosTurnoDto>(new Error(
                "Turnos.EmpleadoNoActivo",
                "Solo se pueden programar turnos a empleados en estado ACTIVO.",
                ErrorType.Failure));
        }

        // 2. Solo turnos activos
        var turno = await _context.Turnos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTurno == idTurno && t.Activo, cancellationToken);

        if (turno is null)
        {
            return Result.Failure<RevisionConflictosTurnoDto>(new Error(
                "Turnos.TurnoNoActivo",
                "El turno seleccionado no existe o fue desactivado.",
                ErrorType.NotFound));
        }

        // 3. Periodo en la zona horaria de la empresa (RG-05: si la hora final es menor que la inicial
        //    termina al día siguiente; un turno Noche del día 20 termina el 21).
        var offset = TimeZoneHelper.GetOffsetForDate(_configuration, fecha);
        bool cruzaMedianoche = turno.HoraFin <= turno.HoraInicio;
        var periodo = PeriodoRango.DesdeTurno(fecha, turno.HoraInicio, turno.HoraFin, cruzaMedianoche, offset);

        var conflictos = new List<ConflictoTurnoDto>();

        // 4. Conflictos que siempre bloquean (RG-01, RG-02, RG-03)
        bool tieneVacaciones = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .AnyAsync(v => v.IdEmpleado == idEmpleado &&
                           v.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                           fecha >= v.FechaInicio && fecha <= v.FechaFin, cancellationToken);
        if (tieneVacaciones)
        {
            conflictos.Add(new ConflictoTurnoDto(TipoVacaciones,
                $"El empleado tiene vacaciones aprobadas en la fecha {fecha:yyyy-MM-dd}.", true));
        }

        var novedades = await _context.Novedades
            .AsNoTracking()
            .Where(n => n.IdEmpleado == idEmpleado &&
                        n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                        n.Inicio < periodo.Fin && periodo.Inicio < n.Fin)
            .Select(n => n.TipoNovedad.Nombre)
            .ToListAsync(cancellationToken);
        foreach (var nombreNovedad in novedades.Distinct())
        {
            conflictos.Add(new ConflictoTurnoDto(TipoNovedad,
                $"El empleado tiene una novedad aprobada ({nombreNovedad}) que se cruza con el turno.", true));
        }

        bool tieneDescanso = await _context.DiasDescanso
            .AsNoTracking()
            .AnyAsync(d => d.IdEmpleado == idEmpleado && d.Fecha == fecha, cancellationToken);
        if (tieneDescanso)
        {
            conflictos.Add(new ConflictoTurnoDto(TipoDescanso,
                $"El empleado tiene día de descanso programado el {fecha:yyyy-MM-dd}.", true));
        }

        // Semana lunes-domingo de la fecha, más un día a cada lado para el descanso entre turnos
        int diffLunes = ((int)fecha.DayOfWeek + 6) % 7;
        var inicioSemana = fecha.AddDays(-diffLunes);
        var finSemana = inicioSemana.AddDays(6);

        var asignacionesCercanas = await _context.Asignaciones
            .AsNoTracking()
            .Include(a => a.Turno)
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        (idAsignacionActual == null || a.IdAsignacion != idAsignacionActual.Value) &&
                        a.Fecha >= inicioSemana.AddDays(-1) &&
                        a.Fecha <= finSemana.AddDays(1))
            .ToListAsync(cancellationToken);

        foreach (var existente in asignacionesCercanas.Where(a => periodo.SeCruzaCon(a.Periodo)))
        {
            conflictos.Add(new ConflictoTurnoDto(TipoCruceTurno,
                $"El empleado ya tiene el turno {existente.Turno.Nombre} del {existente.Fecha:yyyy-MM-dd} que se cruza con ese horario.", true));
        }

        // 5. Validaciones por parámetro: bloquean solo si CONFLICTO_BLOQUEA es verdadero
        bool conflictoBloquea = await _parametroService.ObtenerBooleanoAsync(
            ParametrosCodigos.ConflictoBloquea, fecha, cancellationToken);
        decimal descansoMinHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.DescansoMinEntreTurnos, fecha, cancellationToken);
        decimal jornadaMaxDiaHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.JornadaMaxHorasDia, fecha, cancellationToken);
        decimal jornadaMaxSemanaHoras = await _parametroService.ObtenerDecimalAsync(
            ParametrosCodigos.JornadaMaxHorasSemana, fecha, cancellationToken);

        foreach (var existente in asignacionesCercanas.Where(a => !periodo.SeCruzaCon(a.Periodo)))
        {
            decimal horasDescanso = existente.Periodo.Fin <= periodo.Inicio
                ? (decimal)(periodo.Inicio - existente.Periodo.Fin).TotalHours
                : (decimal)(existente.Periodo.Inicio - periodo.Fin).TotalHours;

            if (horasDescanso < descansoMinHoras)
            {
                conflictos.Add(new ConflictoTurnoDto(TipoDescansoMinimo,
                    $"Entre este turno y el turno del {existente.Fecha:yyyy-MM-dd} hay {horasDescanso:0.##} h de descanso; el mínimo es {descansoMinHoras:0.##} h.",
                    conflictoBloquea));
            }
        }

        int minutosNetosNuevo = Math.Max(0, periodo.DuracionMinutos - turno.MinutosDescanso);

        int minutosDia = asignacionesCercanas
            .Where(a => a.Fecha == fecha)
            .Sum(a => Math.Max(0, a.Periodo.DuracionMinutos - a.Turno.MinutosDescanso));
        decimal horasDia = (minutosDia + minutosNetosNuevo) / 60m;
        if (horasDia > jornadaMaxDiaHoras)
        {
            conflictos.Add(new ConflictoTurnoDto(TipoJornadaDia,
                $"La programación del {fecha:yyyy-MM-dd} suma {horasDia:0.##} h; el máximo diario es {jornadaMaxDiaHoras:0.##} h.",
                conflictoBloquea));
        }

        int minutosSemana = asignacionesCercanas
            .Where(a => a.Fecha >= inicioSemana && a.Fecha <= finSemana)
            .Sum(a => Math.Max(0, a.Periodo.DuracionMinutos - a.Turno.MinutosDescanso));
        decimal horasSemana = (minutosSemana + minutosNetosNuevo) / 60m;
        if (horasSemana > jornadaMaxSemanaHoras)
        {
            conflictos.Add(new ConflictoTurnoDto(TipoJornadaSemana,
                $"La semana del {inicioSemana:yyyy-MM-dd} suma {horasSemana:0.##} h; el máximo semanal es {jornadaMaxSemanaHoras:0.##} h.",
                conflictoBloquea));
        }

        return Result.Success(new RevisionConflictosTurnoDto(
            idEmpleado,
            turno.IdTurno,
            turno.Nombre,
            fecha,
            periodo.Inicio,
            periodo.Fin,
            PuedeAsignarse: conflictos.All(c => !c.Bloquea),
            conflictos));
    }

    /// <summary>
    /// Convierte los conflictos bloqueantes de una revisión en un error de dominio (409).
    /// </summary>
    public static Error ErrorDeConflictos(RevisionConflictosTurnoDto revision)
    {
        var bloqueantes = revision.Conflictos.Where(c => c.Bloquea).ToList();
        var codigo = bloqueantes.Count == 1 && bloqueantes[0].Tipo == TipoCruceTurno
            ? "Turnos.CruceHorario"
            : "Turnos.ValidacionAsignacion";

        return new Error(codigo, string.Join(" ", bloqueantes.Select(c => c.Mensaje)), ErrorType.Conflict);
    }
}
