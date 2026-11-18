using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Reportes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Reportes;

/// <summary>
/// Módulo de reportes operativos y consolidados (Sección 5 - RF-10 - permiso: reportes.consultar).
/// Todas las consultas son de solo lectura y están acotadas por rango de fechas.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reportes")]
public sealed class ReportesController : BaseApiController
{
    public ReportesController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Turnos programados y asistencia por turno (detalle paginado). Filtro de estado: estado de la asignación.
    /// </summary>
    [HttpGet("cumplimiento-turnos")]
    public async Task<IActionResult> ReporteCumplimientoTurnos(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] int? idTurno,
        [FromQuery] bool? soloSinAsistencia,
        [FromQuery] bool? soloConRetardo,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteCumplimientoTurnosQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            idTurno,
            soloSinAsistencia,
            soloConRetardo,
            pageNumber,
            pageSize,
            estado);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Horas trabajadas por empleado: programadas, trabajadas, diferencia y turnos sin asistencia.
    /// </summary>
    [HttpGet("consolidado-empleados")]
    public async Task<IActionResult> ReporteConsolidadoPorEmpleado(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idTurno,
        [FromQuery] string? estado,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteConsolidadoPorEmpleadoQuery(
            fechaDesde,
            fechaHasta,
            idArea,
            idCargo,
            idEmpleado,
            idTurno,
            estado);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Horas por categoría (ordinaria, extra, recargos) por empleado (detalle paginado).
    /// </summary>
    [HttpGet("horas-categoria")]
    public async Task<IActionResult> ReporteHorasPorCategoria(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] int? idTurno,
        [FromQuery] string? codCategoriaHora,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteHorasPorCategoriaQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            idTurno,
            codCategoriaHora,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Saldo de vacaciones por empleado y días aprobados o pendientes dentro del rango. Filtro de estado: estado del empleado.
    /// </summary>
    [HttpGet("vacaciones/saldos")]
    public async Task<IActionResult> ReporteSaldosVacaciones(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteSaldosVacacionesQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            estado,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicitudes de vacaciones que se cruzan con el rango. Filtro de estado: estado de la solicitud.
    /// </summary>
    [HttpGet("vacaciones/solicitudes")]
    public async Task<IActionResult> ReporteSolicitudesVacaciones(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReporteSolicitudesVacacionesQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            estado,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Permisos, incapacidades y ausencias que se cruzan con el rango. Filtro de estado: estado de la solicitud.
    /// </summary>
    [HttpGet("permisos-ausencias")]
    public async Task<IActionResult> ReportePermisosAusencias(
        [FromQuery] DateOnly fechaDesde,
        [FromQuery] DateOnly fechaHasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] int? idCargo,
        [FromQuery] string? codTipoNovedad,
        [FromQuery] string? estado,
        [FromQuery] bool? remunerado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ReportePermisosAusenciasQuery(
            fechaDesde,
            fechaHasta,
            idEmpleado,
            idArea,
            idCargo,
            codTipoNovedad,
            estado,
            remunerado,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }
}

