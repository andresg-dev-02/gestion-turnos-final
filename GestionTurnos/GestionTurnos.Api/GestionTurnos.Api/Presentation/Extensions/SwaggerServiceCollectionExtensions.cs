using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GestionTurnos.Api.Presentation.Extensions;

public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddCustomSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Ingrese el token JWT Bearer en el formato: Bearer {token}"
            };

            options.AddSecurityDefinition("Bearer", securityScheme);
            options.OperationFilter<AuthorizeCheckOperationFilter>();
        });

        return services;
    }
}

/// <summary>
/// Filtro de operación de Swagger que agrega el requerimiento de seguridad y el candado
/// exclusivamente a los endpoints que requieren [Authorize], omitiendo [AllowAnonymous].
/// </summary>
public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = (context.MethodInfo.DeclaringType != null &&
                            context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()) ||
                           context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        var hasAllowAnonymous = (context.MethodInfo.DeclaringType != null &&
                                context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()) ||
                                context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        if (hasAuthorize && !hasAllowAnonymous)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "No autorizado - Token JWT ausente o inválido" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Prohibido - No cuenta con permisos suficientes" });

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", context.Document),
                    new List<string>()
                }
            });
        }
    }
}

