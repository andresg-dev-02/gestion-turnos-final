using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using GestionTurnos.Api.Application.Services.Sesiones;
using GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Seguridad;

/// <summary>
/// Endpoints públicos de autenticación (Sección 3.3) y solicitud de registro (Sección 3.1).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : BaseApiController
{
    private readonly SesionesService _sesiones;

    public AuthController(ISender sender, SesionesService sesiones) : base(sender)
    {
        _sesiones = sesiones;
    }

    /// <summary>
    /// Inicia sesión validando estado en seguridad.usuario y seguridad.solicitud_registro (Sección 3.3).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new IniciarSesionCommand(request.Correo, request.Contrasena);
        var result = await Sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return HandleResult(result);
        }

        // Sesión renovable del dispositivo (refresh token). Con "Recordar" dura semanas; sin él, una jornada.
        var sesion = await _sesiones.EmitirAsync(result.Value, request.Recordar, Request.Headers.UserAgent.ToString(), cancellationToken);
        return HandleResult(Domain.Shared.Result.Success(sesion));
    }

    /// <summary>
    /// Renueva el access token con el refresh token. El refresh token usado queda inválido y se entrega uno nuevo:
    /// el cliente debe reemplazarlo. 401 si la sesión expiró, fue cerrada o el usuario ya no está activo.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("renovar")]
    public async Task<IActionResult> Renovar(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RenovarSesionCommand(request.RefreshToken, Request.Headers.UserAgent.ToString()), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Cierra la sesión de este dispositivo (revoca su refresh token). Siempre responde 200.</summary>
    [AllowAnonymous]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> CerrarSesion(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CerrarSesionCommand(request.RefreshToken), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Solicita el registro de un nuevo empleado, quedando en estado PENDIENTE (Sección 3.1).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("solicitar-registro")]
    public async Task<IActionResult> SolicitarRegistro(
        [FromBody] SolicitarRegistroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SolicitarRegistroCommand(
            request.CodTipoDocumento,
            request.NumeroDocumento,
            request.Nombres,
            request.Apellidos,
            request.Correo,
            request.Telefono,
            request.Contrasena);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

