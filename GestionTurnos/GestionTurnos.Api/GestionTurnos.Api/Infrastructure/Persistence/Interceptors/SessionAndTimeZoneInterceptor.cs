using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de conexión que fija la zona horaria de la empresa al abrir cada conexión (Sección 1).
/// </summary>
public sealed class SessionAndTimeZoneInterceptor : DbConnectionInterceptor
{
    private readonly string _timeZone;

    public SessionAndTimeZoneInterceptor(IConfiguration configuration)
    {
        _timeZone = configuration["AppSettings:TimeZone"] ?? "America/Bogota";
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateTimeZoneCommand(connection);
        if (command is not null)
        {
            command.ExecuteNonQuery();
        }
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateTimeZoneCommand(connection);
        if (command is not null)
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private DbCommand? CreateTimeZoneCommand(DbConnection connection)
    {
        var providerType = connection.GetType().FullName ?? string.Empty;
        var command = connection.CreateCommand();

        if (providerType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var param = command.CreateParameter();
            param.ParameterName = "tz";
            param.Value = _timeZone;
            command.Parameters.Add(param);
            command.CommandText = "SELECT set_config('TimeZone', @tz, false);";
            return command;
        }

        if (providerType.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            var param = command.CreateParameter();
            param.ParameterName = "@tz";
            param.Value = _timeZone;
            command.Parameters.Add(param);
            command.CommandText = "EXEC sys.sp_set_session_context @key = N'TimeZone', @value = @tz;";
            return command;
        }

        command.Dispose();
        return null;
    }
}

