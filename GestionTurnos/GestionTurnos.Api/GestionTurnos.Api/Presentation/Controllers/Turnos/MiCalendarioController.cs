using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Turnos;
using GestionTurnos.Api.Presentation.Contracts.Requests.Turnos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Turnos;

/// <summary>
/// Consultas de calendario, historial de turnos y solicitudes de cambio de turno para el empleado autenticado (Secciones 3.4 y 3.9).
/// Filtra obligatoriamente por el id_empleado de la sesión.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mi-calendario")]
public sealed class MiCalendarioController : BaseApiController
{
    public MiCalendarioController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Consulta el calendario unificado (turnos, descansos, vacaciones, novedades) sobre turnos.v_calendario (Sección 3.4).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerMiCalendario(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiCalendarioQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consulta el historial de turnos pasados (fecha &lt; current_date) del empleado en sesión (Sección 3.4).
    /// </summary>
    [HttpGet("historial-turnos")]
    public async Task<IActionResult> ObtenerMiHistorialTurnos(
        [FromQuery] DateOnly? desde,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiHistorialTurnosQuery(desde, pageNumber, pageSize), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Turnos futuros (no anulados) del empleado en sesión, con su id_asignacion para solicitar cambios (Sección 3.9).
    /// </summary>
    [HttpGet("proximos-turnos")]
    public async Task<IActionResult> ObtenerMisProximosTurnos(
        [FromQuery] int dias = 60,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMisProximosTurnosQuery(dias), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Turnos futuros de los compañeros activos de la misma área, candidatos a intercambio (Sección 3.9).
    /// </summary>
    [HttpGet("turnos-companeros")]
    public async Task<IActionResult> ObtenerTurnosCompaneros(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerTurnosCompanerosQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lista las solicitudes de cambio de turno del empleado en sesión (Sección 3.9 - OPC-03).
    /// </summary>
    [HttpGet("cambios-turno")]
    public async Task<IActionResult> ObtenerMisCambiosTurno(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMisCambiosTurnoQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita un cambio de turno con un compañero validando que ambas asignaciones sean futuras y activas (Sección 3.9 - OPC-03).
    /// </summary>
    [HttpPost("cambios-turno")]
    public async Task<IActionResult> SolicitarMiCambioTurno(
        [FromBody] SolicitarCambioTurnoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarMiCambioTurnoCommand(
            request.IdAsignacionSolicitante,
            request.IdAsignacionCompanero,
            request.Motivo);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancela una solicitud de cambio de turno propia que siga en estado PENDIENTE (Sección 3.9).
    /// </summary>
    [HttpPost("cambios-turno/{idSolicitudCambio:long}/cancelar")]
    public async Task<IActionResult> CancelarMiCambioTurno(
        long idSolicitudCambio,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CancelarMiCambioTurnoCommand(idSolicitudCambio), cancellationToken);
        return HandleResult(result);
    }
}

