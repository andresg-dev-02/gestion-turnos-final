using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Presentation.Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers;

[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ISender Sender { get; }

    protected BaseApiController(ISender sender)
    {
        Sender = sender;
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.Ok(result.Value));
        }

        return MapErrorToResponse(result.Error);
    }

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse.Ok());
        }

        return MapErrorToResponse(result.Error);
    }

    protected IActionResult HandleCreatedResult<T>(
        Result<T> result,
        string actionName)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                actionName,
                new { id = result.Value },
                ApiResponse<T>.Ok(
                    result.Value,
                    "Recurso creado correctamente."));
        }

        return MapErrorToResponse(result.Error);
    }

    protected IActionResult HandleCreatedResult<T>(
        Result<T> result,
        string actionName,
        object routeValues)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                actionName,
                routeValues,
                ApiResponse<T>.Ok(
                    result.Value,
                    "Recurso creado correctamente."));
        }

        return MapErrorToResponse(result.Error);
    }

    private IActionResult MapErrorToResponse(Error error)
    {
        if (error is ValidationError validationError)
        {
            var problem = new ValidationProblemDetails(validationError.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = "Una o más validaciones fallaron.",
                Instance = HttpContext.Request.Path
            };

            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

            return BadRequest(problem);
        }

        return error.Type switch
        {
            ErrorType.NotFound =>
                CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Recurso no encontrado",
                    error.Message,
                    error.Code),

            ErrorType.Conflict =>
                CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Conflicto",
                    error.Message,
                    error.Code),

            // Con cuerpo ProblemDetails: Unauthorized()/Forbid() responden vacío y el cliente perdía
            // mensajes como "Tu registro está pendiente de aprobación" o el motivo de rechazo.
            ErrorType.Unauthorized =>
                CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "No autorizado",
                    error.Message,
                    error.Code),

            ErrorType.Forbidden =>
                CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Acceso denegado",
                    error.Message,
                    error.Code),

            _ =>
                CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Solicitud inválida",
                    error.Message,
                    error.Code)
        };
    }

    private IActionResult CreateProblem(
        int statusCode,
        string title,
        string detail,
        string? code = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        // Código estable del error (p. ej. "Auth.SegundoFactorRequerido") para que el cliente decida sin comparar textos.
        if (!string.IsNullOrEmpty(code))
        {
            problem.Extensions["code"] = code;
        }

        return StatusCode(statusCode, problem);
    }
}

