using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GestionTurnos.Api.Presentation.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Api.Presentation.Extensions;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // Errores de lectura del cuerpo o de la URL (JSON mal formado, fecha "hola", cuerpo vacío...)
                // con el mismo formato y en español, como los errores de validación de los handlers.
                // Sin esto ASP.NET responde "One or more validation errors occurred." y el frontend lo muestra tal cual.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problem = new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Error de validación",
                        Detail = "La solicitud tiene datos con formato inválido. Revise los campos indicados.",
                        Instance = context.HttpContext.Request.Path
                    };
                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                    return new BadRequestObjectResult(problem);
                };
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddCustomRateLimiting(configuration);

        // Solo los orígenes de Cors:AllowedOrigins. Antes se aceptaba cualquier origen con credenciales,
        // y la lista configurada no se usaba.
        var origenesPermitidos = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy("AngularPolicy", builder =>
            {
                builder
                    .WithOrigins(origenesPermitidos)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.AddMemoryCache();

        return services;
    }
}

