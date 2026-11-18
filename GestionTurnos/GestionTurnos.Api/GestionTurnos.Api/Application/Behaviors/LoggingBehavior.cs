using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using GestionTurnos.Api.Domain.Shared;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Application.Behaviors;

/// <summary>
/// Pipeline Behavior de MediatR que intercepta todas las peticiones
/// para registrar su ejecución, parámetros y tiempo de respuesta.
/// Los parámetros sensibles (contraseñas, hashes, tokens) se enmascaran antes de escribirse en el log.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const string Enmascarado = "***MASKED***";

    private static readonly string[] FragmentosSensibles =
    {
        "contrasena", "contraseña", "password", "hash", "token", "secret", "clave"
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propiedadesCache = new();

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Iniciando solicitud: {RequestName} - {@Request}", requestName, Sanitizar(request));

        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (response is Result result && result.IsFailure)
        {
            _logger.LogWarning("La solicitud {RequestName} falló en {ElapsedMilliseconds} ms. Error: {@Error}",
                requestName, stopwatch.ElapsedMilliseconds, result.Error);
        }
        else
        {
            _logger.LogInformation("Solicitud {RequestName} completada exitosamente en {ElapsedMilliseconds} ms.",
                requestName, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }

    /// <summary>
    /// Copia las propiedades públicas de primer nivel de la petición, reemplazando las sensibles.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> Sanitizar(TRequest request)
    {
        var propiedades = _propiedadesCache.GetOrAdd(
            request.GetType(),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                  .ToArray());

        var resultado = new Dictionary<string, object?>(propiedades.Length);
        foreach (var propiedad in propiedades)
        {
            bool esSensible = FragmentosSensibles.Any(f =>
                propiedad.Name.Contains(f, StringComparison.OrdinalIgnoreCase));

            resultado[propiedad.Name] = esSensible ? Enmascarado : propiedad.GetValue(request);
        }

        return resultado;
    }
}
