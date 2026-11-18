using System.Threading.RateLimiting;
using GestionTurnos.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Api.Presentation.Extensions;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitingSettings = new RateLimitingSettings();
        configuration.GetSection(RateLimitingSettings.SectionName).Bind(rateLimitingSettings);

        if (!rateLimitingSettings.EnableRateLimiting)
        {
            // Program.cs llama siempre a app.UseRateLimiter(): sin este registro vacío
            // la aplicación no arranca cuando el límite está desactivado.
            services.AddRateLimiter(_ => { });
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = rateLimitingSettings.PermitLimit,
                        QueueLimit = rateLimitingSettings.QueueLimit,
                        Window = TimeSpan.FromSeconds(rateLimitingSettings.WindowSeconds)
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "You have exceeded the rate limit. Please try again later."
                }, cancellationToken: token);
            };
        });

        return services;
    }
}

