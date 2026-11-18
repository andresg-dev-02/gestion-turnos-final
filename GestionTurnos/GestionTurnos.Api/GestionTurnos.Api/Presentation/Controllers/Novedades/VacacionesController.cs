using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Novedades;
using GestionTurnos.Api.Presentation.Contracts.Requests.Novedades;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Novedades;

/// <summary>
/// Saldo, solicitudes, períodos vacacionales y resolución de vacaciones (Secciones 3.4, 3.7, 4 y 5 - RF-06, CU-03).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vacaciones")]
public sealed class VacacionesController : BaseApiController
{
    public VacacionesController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Consulta el saldo de vacaciones del empleado en sesión desde novedades.v_saldo_vacaciones (Sección 3.4).
    /// </summary>
    [HttpGet("mi-saldo")]
    public async Task<IActionResult> ObtenerMiSaldo(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMiSaldoVacacionesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lista las solicitudes de vacaciones propias del empleado en sesión (Sección 3.4).
    /// </summary>
    [HttpGet("mis-solicitudes")]
    public async Task<IActionResult> ObtenerMisSolicitudes(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMisSolicitudesVacacionesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita vacaciones calculando los días hábiles en el backend y validando el saldo disponible (Sección 3.7 - CU-03).
    /// </summary>
    [HttpPost("mis-solicitudes")]
    public async Task<IActionResult> SolicitarMisVacaciones(
        [FromBody] SolicitarMisVacacionesRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarMisVacacionesCommand(request.FechaInicio, request.FechaFin, request.Observacion);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancela una solicitud de vacaciones propia que siga en estado PENDIENTE (Sección 3.7).
    /// </summary>
    [HttpPost("mis-solicitudes/{idSolicitudVacaciones:long}/cancelar")]
    public async Task<IActionResult> CancelarMiSolicitud(
        long idSolicitudVacaciones,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CancelarMiSolicitudVacacionesCommand(idSolicitudVacaciones), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Bandeja de solicitudes con el saldo de cada empleado (sección 11). Sin estado muestra las PENDIENTE; estado=TODOS devuelve todas.
    /// </summary>
    [HttpGet("solicitudes")]
    public async Task<IActionResult> ListarSolicitudesAdmin(
        [FromQuery] long? idEmpleado,
        [FromQuery] string? estado,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarSolicitudesVacacionesAdminQuery(idEmpleado, estado, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Crea una solicitud de vacaciones a nombre del empleado; nace PENDIENTE (sección 11).
    /// </summary>
    [HttpPost("solicitudes")]
    public async Task<IActionResult> RegistrarSolicitudAdmin(
        [FromBody] RegistrarSolicitudVacacionesAdminRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarSolicitudVacacionesAdminCommand(
            request.IdEmpleado,
            request.FechaInicio,
            request.FechaFin,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Turnos que chocan con la solicitud, el valor de CONFLICTO_BLOQUEA y cuáles tienen asistencia (sección 11).
    /// </summary>
    [HttpGet("solicitudes/{idSolicitudVacaciones:long}/turnos-en-conflicto")]
    public async Task<IActionResult> ConsultarTurnosEnConflicto(
        long idSolicitudVacaciones,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ConsultarTurnosEnConflictoVacacionesQuery(idSolicitudVacaciones), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Anula unas vacaciones aprobadas que todavía no inician; el saldo vuelve (sección 11).
    /// </summary>
    [HttpPost("solicitudes/{idSolicitudVacaciones:long}/anular")]
    public async Task<IActionResult> AnularVacaciones(
        long idSolicitudVacaciones,
        [FromBody] AnularVacacionesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new AnularVacacionesAprobadasCommand(idSolicitudVacaciones, request.Motivo), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Aprueba o rechaza una solicitud de vacaciones revalidando el saldo y aplicando CONFLICTO_BLOQUEA (Sección 4).
    /// </summary>
    [HttpPost("solicitudes/{idSolicitudVacaciones:long}/resolver")]
    public async Task<IActionResult> ResolverSolicitud(
        long idSolicitudVacaciones,
        [FromBody] ResolverSolicitudRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolverSolicitudVacacionesCommand(
            idSolicitudVacaciones,
            request.Aprobar,
            request.MotivoRechazo,
            request.ConfirmarAnulacionTurnos);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra los días causados por período vacacional que alimentan el saldo (Sección 5 - RF-06).
    /// </summary>
    [HttpPost("periodos")]
    public async Task<IActionResult> RegistrarPeriodoVacacional(
        [FromBody] RegistrarPeriodoVacacionalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarPeriodoVacacionalCommand(
            request.IdEmpleado,
            request.FechaInicio,
            request.FechaFin,
            request.DiasOtorgados);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Lista los períodos vacacionales registrados (permiso: vacaciones.aprobar).
    /// </summary>
    [HttpGet("periodos")]
    public async Task<IActionResult> ListarPeriodosVacacionales(
        [FromQuery] long? idEmpleado,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarPeriodosVacacionalesAdminQuery(idEmpleado), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consulta los saldos de vacaciones desde novedades.v_saldo_vacaciones (permiso: vacaciones.aprobar).
    /// </summary>
    [HttpGet("saldos")]
    public async Task<IActionResult> ListarSaldosAdmin(
        [FromQuery] long? idEmpleado,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarSaldosVacacionesAdminQuery(idEmpleado), cancellationToken);
        return HandleResult(result);
    }
}

