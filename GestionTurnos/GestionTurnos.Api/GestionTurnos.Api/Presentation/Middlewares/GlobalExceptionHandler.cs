using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Presentation.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // 1. Verificar si es un error de base de datos traducible (Sección 1 - RNF-01)
        if (DatabaseErrorTranslator.TryTranslate(exception, out var translatedError) && translatedError is not null)
        {
            _logger.LogWarning(
                exception,
                "Restricción de base de datos traducida [{ErrorCode}]: {Message}",
                translatedError.Code,
                translatedError.Message);

            var statusCode = translatedError.Type switch
            {
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

            var title = translatedError.Type switch
            {
                ErrorType.Conflict => "Conflicto",
                ErrorType.NotFound => "Recurso no encontrado",
                _ => "Solicitud inválida"
            };

            var dbProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = translatedError.Message,
                Instance = httpContext.Request.Path
            };
            dbProblemDetails.Extensions["code"] = translatedError.Code;
            dbProblemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(dbProblemDetails, cancellationToken);
            return true;
        }

        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var (unhandledStatusCode, unhandledTitle, type) = exception switch
        {
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "El registro fue modificado por otro usuario mientras intentabas actualizarlo. Por favor, recarga la información e intenta de nuevo.",
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
            )
        };

        var env = httpContext.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
        var problemDetails = new ProblemDetails
        {
            Status = unhandledStatusCode,
            Title = unhandledTitle,
            Type = type,
            Detail = env?.IsDevelopment() == true ? exception.Message : null,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
