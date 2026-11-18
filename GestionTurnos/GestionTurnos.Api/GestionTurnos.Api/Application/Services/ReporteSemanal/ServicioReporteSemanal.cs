using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Notificaciones;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Application.Services.ReporteSemanal;

/// <summary>Resultado de un envío del reporte semanal.</summary>
public sealed record ResultadoReporteSemanal(
    DateOnly Desde,
    DateOnly Hasta,
    int Encolados,
    int SinCorreo,
    int SinActividad,
    int Fallidos);

/// <summary>
/// Reporte semanal al trabajador: por cada empleado activo, sus turnos publicados de la semana con la asistencia
/// marcada, faltas (y si están justificadas por vacaciones o novedades aprobadas), llegadas tarde,
/// horas liquidadas por categoría y novedades del periodo.
/// </summary>
public interface IServicioReporteSemanal
{
    /// <summary>Lunes de la semana anterior completa (zona horaria de la empresa).</summary>
    DateOnly LunesSemanaAnterior();

    /// <param name="lunes">Primer día de la semana a reportar; el reporte cubre lunes a domingo.</param>
    /// <param name="idEmpleado">Solo ese empleado; <c>null</c> = todos los empleados activos.</param>
    /// <param name="incluirSinActividad">Enviar también a quien no tuvo turnos, horas ni novedades.</param>
    Task<ResultadoReporteSemanal> EnviarAsync(
        DateOnly lunes,
        long? idEmpleado,
        bool incluirSinActividad,
        CancellationToken cancellationToken);
}

public sealed class ServicioReporteSemanal : IServicioReporteSemanal
{
    private readonly GestionTurnosContext _context;
    private readonly INotificadorCorreo _notificador;
    private readonly IParametroService _parametroService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServicioReporteSemanal> _logger;

    public ServicioReporteSemanal(
        GestionTurnosContext context,
        INotificadorCorreo notificador,
        IParametroService parametroService,
        IConfiguration configuration,
        ILogger<ServicioReporteSemanal> logger)
    {
        _context = context;
        _notificador = notificador;
        _parametroService = parametroService;
        _configuration = configuration;
        _logger = logger;
    }

    public DateOnly LunesSemanaAnterior()
    {
        var hoy = TimeZoneHelper.GetCurrentDateInEmpresaTimeZone(_configuration);
        int desdeLunes = ((int)hoy.DayOfWeek + 6) % 7; // lunes = 0 … domingo = 6
        return hoy.AddDays(-desdeLunes - 7);
    }

    public async Task<ResultadoReporteSemanal> EnviarAsync(
        DateOnly lunes,
        long? idEmpleado,
        bool incluirSinActividad,
        CancellationToken cancellationToken)
    {
        var domingo = lunes.AddDays(6);
        var zona = TimeZoneHelper.ResolveEmpresaTimeZone(_configuration);
        var (desde, hasta) = AInstantes(lunes, domingo.AddDays(1));

        // ---- Empleados destino --------------------------------------------------------------
        var empleadosQuery = _context.Empleados.AsNoTracking()
            .Where(e => e.CodEstadoEmpleado == EstadosEmpleado.Activo);
        if (idEmpleado.HasValue)
        {
            empleadosQuery = empleadosQuery.Where(e => e.IdEmpleado == idEmpleado.Value);
        }

        var empleados = await empleadosQuery
            .OrderBy(e => e.IdEmpleado)
            .Select(e => new
            {
                e.IdEmpleado,
                Nombre = e.Nombres + " " + e.Apellidos,
                Correo = e.CorreoContacto ?? (e.Usuario != null ? e.Usuario.Correo : null)
            })
            .ToListAsync(cancellationToken);

        if (empleados.Count == 0)
        {
            return new ResultadoReporteSemanal(lunes, domingo, 0, 0, 0, 0);
        }

        var ids = empleados.Select(e => e.IdEmpleado).ToList();

        // ---- Datos de la semana (una consulta por tipo, sin N+1) ------------------------------
        // Solo turnos publicados (CONFIRMADA): los PROGRAMADA aún no son visibles para el empleado.
        var turnos = await (
                from v in _context.VistaHorasPorTurno.AsNoTracking()
                join a in _context.Asignaciones.AsNoTracking() on v.IdAsignacion equals a.IdAsignacion
                where v.Fecha >= lunes && v.Fecha <= domingo &&
                      a.CodEstadoAsignacion == EstadosAsignacion.Confirmada &&
                      ids.Contains(v.IdEmpleado)
                select new
                {
                    v.IdEmpleado,
                    v.Fecha,
                    Turno = a.Turno.Nombre,
                    a.Turno.HoraInicio,
                    a.Turno.HoraFin,
                    a.Turno.CruzaMedianoche,
                    v.MinutosProgramados,
                    v.MinutosTrabajados,
                    v.HoraEntrada,
                    v.HoraSalida,
                    v.MinutosTarde,
                    v.SinAsistencia
                })
            .ToListAsync(cancellationToken);

        var horasCategoria = await (
                from dh in _context.DetallesHoras.AsNoTracking()
                join r in _context.RegistrosAsistencia.AsNoTracking() on dh.IdRegistro equals r.IdRegistro
                where r.HoraEntrada >= desde && r.HoraEntrada < hasta && ids.Contains(r.IdEmpleado)
                group dh by new { r.IdEmpleado, dh.CodCategoriaHora, dh.CategoriaHora.Nombre } into g
                select new { g.Key.IdEmpleado, g.Key.CodCategoriaHora, g.Key.Nombre, Minutos = g.Sum(x => x.Minutos) })
            .ToListAsync(cancellationToken);

        var novedades = await _context.Novedades.AsNoTracking()
            .Where(n => n.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                        n.Inicio < hasta && n.Fin > desde &&
                        ids.Contains(n.IdEmpleado))
            .Select(n => new { n.IdEmpleado, Tipo = n.TipoNovedad.Nombre, n.Inicio, n.Fin })
            .ToListAsync(cancellationToken);

        var vacaciones = await _context.SolicitudesVacaciones.AsNoTracking()
            .Where(s => s.CodEstadoSolicitud == EstadosSolicitud.Aprobada &&
                        s.FechaInicio <= domingo && s.FechaFin >= lunes &&
                        ids.Contains(s.IdEmpleado))
            .Select(s => new { s.IdEmpleado, s.FechaInicio, s.FechaFin })
            .ToListAsync(cancellationToken);

        var tolerancias = new Dictionary<DateOnly, int>();
        foreach (var fecha in turnos.Select(t => t.Fecha).Distinct())
        {
            tolerancias[fecha] = await ToleranciaAsync(fecha, cancellationToken);
        }

        var turnosPorEmpleado = turnos.ToLookup(t => t.IdEmpleado);
        var horasPorEmpleado = horasCategoria.ToLookup(h => h.IdEmpleado);
        var novedadesPorEmpleado = novedades.ToLookup(n => n.IdEmpleado);
        var vacacionesPorEmpleado = vacaciones.ToLookup(v => v.IdEmpleado);

        int encolados = 0, sinCorreo = 0, sinActividad = 0, fallidos = 0;

        foreach (var empleado in empleados)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var susTurnos = turnosPorEmpleado[empleado.IdEmpleado].OrderBy(t => t.Fecha).ThenBy(t => t.HoraInicio).ToList();
            var susHoras = horasPorEmpleado[empleado.IdEmpleado].OrderBy(h => h.CodCategoriaHora).ToList();
            var susNovedades = novedadesPorEmpleado[empleado.IdEmpleado].OrderBy(n => n.Inicio).ToList();
            var susVacaciones = vacacionesPorEmpleado[empleado.IdEmpleado].OrderBy(v => v.FechaInicio).ToList();

            if (!incluirSinActividad && susTurnos.Count == 0 && susHoras.Count == 0 &&
                susNovedades.Count == 0 && susVacaciones.Count == 0)
            {
                sinActividad++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(empleado.Correo))
            {
                sinCorreo++;
                _logger.LogWarning("El empleado {IdEmpleado} no tiene correo; no recibió el reporte semanal.", empleado.IdEmpleado);
                continue;
            }

            // Una falta está justificada si el día cae en vacaciones aprobadas o se cruza con una novedad aprobada.
            string? Justificacion(DateOnly fecha)
            {
                if (susVacaciones.Any(v => v.FechaInicio <= fecha && v.FechaFin >= fecha))
                {
                    return "Vacaciones";
                }

                var (iniDia, finDia) = AInstantes(fecha, fecha.AddDays(1));
                return susNovedades.FirstOrDefault(n => n.Inicio < finDia && n.Fin > iniDia)?.Tipo;
            }

            int faltas = 0, faltasJustificadas = 0, llegadasTarde = 0, minutosTarde = 0;
            var dias = new List<ReporteSemanalDiaItem>(susTurnos.Count);

            foreach (var t in susTurnos)
            {
                string estado;
                bool alerta;
                int tarde = t.HoraEntrada is null ? 0 : t.MinutosTarde ?? 0;

                if (t.SinAsistencia)
                {
                    var motivo = Justificacion(t.Fecha);
                    if (motivo is null)
                    {
                        faltas++;
                        (estado, alerta) = ("Falta", true);
                    }
                    else
                    {
                        faltasJustificadas++;
                        (estado, alerta) = ($"Justificada ({motivo})", false);
                    }
                }
                else if (t.HoraEntrada is null)
                {
                    (estado, alerta) = ("Pendiente", false);
                }
                else if (t.HoraSalida is null)
                {
                    (estado, alerta) = ("Sin marcar salida", true);
                }
                else if (tarde > tolerancias.GetValueOrDefault(t.Fecha))
                {
                    llegadasTarde++;
                    minutosTarde += tarde;
                    (estado, alerta) = ($"Tarde ({FormatoCorreo.Duracion(tarde)})", true);
                }
                else
                {
                    (estado, alerta) = ("Cumplido", false);
                }

                dias.Add(new ReporteSemanalDiaItem(
                    FormatoCorreo.FechaCorta(t.Fecha),
                    t.Turno,
                    FormatoCorreo.Horario(t.HoraInicio, t.HoraFin, t.CruzaMedianoche),
                    FormatoCorreo.Hora(t.HoraEntrada, zona),
                    FormatoCorreo.Hora(t.HoraSalida, zona),
                    t.HoraEntrada is null ? "—" : FormatoCorreo.Duracion(t.MinutosTrabajados),
                    estado,
                    alerta));
            }

            var listaNovedades = susVacaciones
                .Select(v => new DetalleCorreoItem("Vacaciones", $"{FormatoCorreo.FechaCorta(v.FechaInicio)} – {FormatoCorreo.FechaCorta(v.FechaFin)}"))
                .Concat(susNovedades.Select(n => new DetalleCorreoItem(
                    n.Tipo, $"{FormatoCorreo.FechaHora(n.Inicio, zona)} – {FormatoCorreo.FechaHora(n.Fin, zona)}")))
                .ToList();

            var modelo = new ReporteSemanalModelo(
                empleado.Nombre,
                FormatoCorreo.Fecha(lunes),
                FormatoCorreo.Fecha(domingo),
                susTurnos.Count,
                susTurnos.Count(t => t.HoraEntrada is not null),
                faltas,
                faltasJustificadas,
                llegadasTarde,
                FormatoCorreo.Duracion(susTurnos.Sum(t => t.MinutosProgramados)),
                FormatoCorreo.Duracion(susTurnos.Where(t => t.HoraEntrada is not null).Sum(t => t.MinutosTrabajados)),
                FormatoCorreo.Duracion(minutosTarde),
                dias,
                susHoras.Select(h => new DetalleCorreoItem(h.Nombre, FormatoCorreo.Duracion(h.Minutos))).ToList(),
                listaNovedades);

            bool ok = await _notificador.EncolarAsync(
                PlantillasCorreo.ReporteSemanal,
                new DestinatarioCorreo(empleado.Correo, empleado.Nombre),
                $"Su resumen semanal: {lunes:dd/MM} al {domingo:dd/MM/yyyy}",
                modelo,
                cancellationToken);

            if (ok) encolados++; else fallidos++;
        }

        _logger.LogInformation(
            "Reporte semanal {Desde}–{Hasta}: {Encolados} encolados, {SinCorreo} sin correo, {SinActividad} sin actividad, {Fallidos} fallidos.",
            lunes, domingo, encolados, sinCorreo, sinActividad, fallidos);

        return new ResultadoReporteSemanal(lunes, domingo, encolados, sinCorreo, sinActividad, fallidos);
    }

    private (DateTimeOffset Desde, DateTimeOffset HastaExclusivo) AInstantes(DateOnly desde, DateOnly hastaExclusivo) =>
        (new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeZoneHelper.GetOffsetForDate(_configuration, desde)),
         new DateTimeOffset(hastaExclusivo.ToDateTime(TimeOnly.MinValue), TimeZoneHelper.GetOffsetForDate(_configuration, hastaExclusivo)));

    /// <summary>
    /// Misma regla que el resumen de asistencia: es tardanza si supera TOLERANCIA_ENTRADA_MIN vigente en la fecha.
    /// Si el parámetro no está configurado, cualquier minuto de retraso se reporta (el reporte no debe fallar por eso).
    /// </summary>
    private async Task<int> ToleranciaAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        try
        {
            return await _parametroService.ObtenerEnteroAsync(ParametrosCodigos.ToleranciaEntradaMin, fecha, cancellationToken);
        }
        catch (ParametroNoConfiguradoException ex)
        {
            _logger.LogWarning(ex, "TOLERANCIA_ENTRADA_MIN no configurado para {Fecha}; el reporte semanal usa 0 minutos.", fecha);
            return 0;
        }
    }
}
