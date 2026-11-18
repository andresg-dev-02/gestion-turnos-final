using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Novedades;
using GestionTurnos.Api.Presentation.Contracts.Requests.Novedades;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Novedades;

/// <summary>
/// Solicitud de permisos, incapacidades, calamidades, reporte de ausencias y resolución (Secciones 3.8 y 4 - RF-07).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/novedades")]
public sealed class NovedadesController : BaseApiController
{
    public NovedadesController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Lista las novedades y permisos propios del empleado en sesión (Sección 3.4).
    /// </summary>
    [HttpGet("mis-solicitudes")]
    public async Task<IActionResult> ObtenerMisNovedades(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMisNovedadesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita un permiso, incapacidad o calamidad para el empleado en sesión (Sección 3.8 - RF-07).
    /// </summary>
    [HttpPost("mis-solicitudes")]
    public async Task<IActionResult> SolicitarMiNovedad(
        [FromBody] SolicitarMiNovedadRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarMiNovedadCommand(
            request.CodTipoNovedad,
            request.Inicio,
            request.Fin,
            request.Observaciones,
            request.UrlSoporte);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cancela una novedad propia que siga en estado PENDIENTE (Sección 3.8).
    /// </summary>
    [HttpPost("mis-solicitudes/{idNovedad:long}/cancelar")]
    public async Task<IActionResult> CancelarMiNovedad(
        long idNovedad,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CancelarMiNovedadCommand(idNovedad), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Bandeja de novedades (sección 12). Sin estado muestra las PENDIENTE; estado=TODOS devuelve todas (permiso: novedades.aprobar).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListarAdmin(
        [FromQuery] long? idEmpleado,
        [FromQuery] string? estado,
        [FromQuery] string? tipo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarNovedadesAdminQuery(idEmpleado, estado, tipo, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra directamente una novedad o ausencia por el administrador (Sección 3.8 - permiso: novedades.aprobar).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegistrarAdmin(
        [FromBody] RegistrarNovedadAdminRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarNovedadAdminCommand(
            request.IdEmpleado,
            request.CodTipoNovedad,
            request.Inicio,
            request.Fin,
            request.Remunerado,
            request.AfectaHoras,
            request.Observaciones,
            request.UrlSoporte,
            request.ConfirmarAnulacionTurnos);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Aprueba o rechaza una novedad pendiente aplicando CONFLICTO_BLOQUEA frente a turnos programados (Sección 4).
    /// </summary>
    [HttpPost("{idNovedad:long}/resolver")]
    public async Task<IActionResult> ResolverNovedad(
        long idNovedad,
        [FromBody] ResolverNovedadRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolverNovedadCommand(
            idNovedad,
            request.Aprobar,
            request.MotivoRechazo,
            request.ConfirmarAnulacionTurnos,
            request.Remunerado,
            request.AfectaHoras);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

