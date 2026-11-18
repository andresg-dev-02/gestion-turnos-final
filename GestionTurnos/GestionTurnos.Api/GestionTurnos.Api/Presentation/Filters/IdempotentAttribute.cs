using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Api.Presentation.Filters;

/// <summary>
/// Filtro de acción para asegurar la idempotencia de las peticiones POST.
/// Evita la creación de registros duplicados cuando el cliente reintenta la misma
/// petición enviando el mismo X-Idempotency-Key.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    private const string IdempotencyHeader = "X-Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey))
        {
            await next();
            return;
        }

        var keyStr = idempotencyKey.ToString();
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = $"Idempotency_{keyStr}";

        if (cache.TryGetValue(cacheKey, out ObjectResult? cachedResult) && cachedResult != null)
        {
            context.Result = cachedResult;
            return;
        }

        var executedContext = await next();

        if (executedContext.Exception == null && executedContext.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? 200;
            if (statusCode >= 200 && statusCode < 300)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };

                cache.Set(cacheKey, objectResult, cacheOptions);
            }
        }
    }
}

