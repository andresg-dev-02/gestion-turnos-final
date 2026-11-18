using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Gestión administrativa de solicitudes de registro (Sección 3.2 - permiso: registros.aprobar).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/solicitudes-registro")]
public sealed class SolicitudesRegistroController : BaseApiController
{
    public SolicitudesRegistroController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Bandeja de solicitudes de registro por estado (por defecto PENDIENTE), paginada y sin hash_contrasena.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado,
        [FromQuery] string? busqueda,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarSolicitudesRegistroQuery(estado, busqueda, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Aprueba una solicitud de registro pendiente creando usuario y empleado en una sola transacción (Sección 3.2).
    /// </summary>
    [HttpPost("{idSolicitudRegistro:long}/aprobar")]
    public async Task<IActionResult> Aprobar(
        long idSolicitudRegistro,
        [FromBody] AprobarSolicitudRegistroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AprobarSolicitudRegistroCommand(
            idSolicitudRegistro,
            request.IdArea,
            request.IdCargo,
            request.FechaIngreso,
            request.CodTipoContrato);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Rechaza una solicitud de registro pendiente indicando el motivo obligatorio (Sección 3.2).
    /// </summary>
    [HttpPost("{idSolicitudRegistro:long}/rechazar")]
    public async Task<IActionResult> Rechazar(
        long idSolicitudRegistro,
        [FromBody] RechazarSolicitudRegistroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RechazarSolicitudRegistroCommand(idSolicitudRegistro, request.MotivoRechazo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

