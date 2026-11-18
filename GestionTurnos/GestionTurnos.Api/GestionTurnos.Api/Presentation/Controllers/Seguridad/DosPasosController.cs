using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.DosPasos;
using GestionTurnos.Api.Application.Services.Sesiones;
using GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Autenticación en dos pasos por correo (opcional).
///
/// Flujo de inicio de sesión con la opción activa:
///  1. POST auth/login → 401 con <c>code = "Auth.SegundoFactorRequerido"</c> y el correo enmascarado en <c>detail</c>.
///  2. POST auth/dos-pasos/verificar { correo, claveVerificacion } → misma respuesta que un login exitoso.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth/dos-pasos")]
public sealed class DosPasosController : BaseApiController
{
    private readonly SesionesService _sesiones;

    public DosPasosController(ISender sender, SesionesService sesiones) : base(sender)
    {
        _sesiones = sesiones;
    }

    /// <summary>Completa el inicio de sesión con el código enviado al correo.</summary>
    [AllowAnonymous]
    [HttpPost("verificar")]
    public async Task<IActionResult> Verificar([FromBody] VerificarDosPasosRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new VerificarInicioSesionDosPasosCommand(request.Correo, request.ClaveVerificacion), cancellationToken);
        if (result.IsFailure)
        {
            return HandleResult(result);
        }

        // El refresh token se emite recién aquí, cuando el segundo factor ya se verificó.
        var sesion = await _sesiones.EmitirAsync(result.Value, request.Recordar, Request.Headers.UserAgent.ToString(), cancellationToken);
        return HandleResult(Domain.Shared.Result.Success(sesion));
    }

    /// <summary>Reenvía un código nuevo. Siempre responde 200 (no revela si existe un inicio de sesión pendiente).</summary>
    [AllowAnonymous]
    [HttpPost("reenviar")]
    public async Task<IActionResult> Reenviar([FromBody] ReenviarCodigoDosPasosRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ReenviarCodigoInicioSesionCommand(request.Correo), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Estado de la verificación en dos pasos del usuario en sesión.</summary>
    [HttpGet("~/api/v{version:apiVersion}/mi-perfil/dos-pasos")]
    public async Task<IActionResult> ObtenerMiEstado(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerMiEstadoDosPasosQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Paso 1 de la activación: confirma la contraseña y envía un código al correo de la cuenta.</summary>
    [HttpPost("~/api/v{version:apiVersion}/mi-perfil/dos-pasos/activar")]
    public async Task<IActionResult> SolicitarActivacion([FromBody] ContrasenaActualRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SolicitarActivacionDosPasosCommand(request.Contrasena), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Paso 2 de la activación: valida el código recibido y activa la opción.</summary>
    [HttpPost("~/api/v{version:apiVersion}/mi-perfil/dos-pasos/confirmar")]
    public async Task<IActionResult> ConfirmarActivacion([FromBody] ConfirmarDosPasosRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ConfirmarActivacionDosPasosCommand(request.ClaveVerificacion), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Desactiva la verificación en dos pasos (requiere la contraseña actual).</summary>
    [HttpPost("~/api/v{version:apiVersion}/mi-perfil/dos-pasos/desactivar")]
    public async Task<IActionResult> Desactivar([FromBody] ContrasenaActualRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DesactivarMisDosPasosCommand(request.Contrasena), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Administración: desactiva la opción de un usuario que perdió acceso a su correo (permiso usuarios.gestionar).</summary>
    [HttpDelete("~/api/v{version:apiVersion}/usuarios/{idUsuario:long}/dos-pasos")]
    public async Task<IActionResult> Restablecer(long idUsuario, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RestablecerDosPasosUsuarioCommand(idUsuario), cancellationToken);
        return HandleResult(result);
    }
}
