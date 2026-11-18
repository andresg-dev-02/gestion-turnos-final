using GestionTurnos.Api.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace GestionTurnos.Api.Presentation.Middlewares;

public class LogContextMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUserService)
    {
        var userId = currentUserService.UserId?.ToString() ?? "Anonymous";
        var correlationId = context.TraceIdentifier;

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

