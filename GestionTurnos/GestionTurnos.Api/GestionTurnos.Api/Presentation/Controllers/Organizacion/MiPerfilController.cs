using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Organizacion;
using GestionTurnos.Api.Presentation.Contracts.Requests.Organizacion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Organizacion;

/// <summary>
/// Autogestión del perfil y contraseña del empleado en sesión (Secciones 3.4 y 3.5).
/// El id_empleado nunca se recibe por parámetro: se resuelve siempre desde el usuario autenticado (Sección 2).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mi-perfil")]
public sealed class MiPerfilController : BaseApiController
{
    public MiPerfilController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Obtiene los datos fijos y de contacto del perfil del empleado autenticado (Sección 3.4).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerMiPerfil(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMiPerfilQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Actualiza únicamente correo_contacto, telefono y direccion (lista blanca estricta - Sección 3.5).
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> EditarMiPerfil(
        [FromBody] EditarMiPerfilRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EditarMiPerfilCommand(request.CorreoContacto, request.Telefono, request.Direccion);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cambia la contraseña del usuario en sesión y registra el evento en auditoria.bitacora_negocio (Sección 3.5).
    /// </summary>
    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> CambiarMiContrasena(
        [FromBody] CambiarMiContrasenaRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CambiarMiContrasenaCommand(request.ContrasenaActual, request.NuevaContrasena);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

