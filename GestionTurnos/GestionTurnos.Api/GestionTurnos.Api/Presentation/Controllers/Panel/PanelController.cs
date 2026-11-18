using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Panel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Panel;

/// <summary>
/// Panel inicial del administrador (FLUJO_BD_ADMIN sección 3).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/panel")]
public sealed class PanelController : BaseApiController
{
    public PanelController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Contadores: registros, vacaciones y novedades pendientes, empleados activos, turnos de hoy y turnos sin asistencia.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerPanelInicialQuery(), cancellationToken);
        return HandleResult(result);
    }
}
