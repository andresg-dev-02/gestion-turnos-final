using Serilog.Core;
using Serilog.Events;

namespace GestionTurnos.Api.Infrastructure.Logging;

public class SensitiveDataEnricher : ILogEventEnricher
{
    private readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "contrasena",
        "hash",
        "token",
        "secret",
        "clave",
        "credencial",
        "authorization",
        "bearer"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (_sensitiveKeys.Any(key => property.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(property.Key, "***MASKED***"));
            }
        }
    }
}

