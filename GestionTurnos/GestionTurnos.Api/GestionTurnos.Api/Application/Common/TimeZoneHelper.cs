using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.Common;

public static class TimeZoneHelper
{
    public static TimeZoneInfo ResolveEmpresaTimeZone(IConfiguration configuration)
    {
        var tzId = configuration["AppSettings:TimeZone"] ?? "America/Bogota";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            try
            {
                // Fallback en Windows para America/Bogota (-05:00)
                return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "America/Bogota",
                    TimeSpan.FromHours(-5),
                    "Hora Estándar de Colombia",
                    "COT");
            }
        }
    }

    public static TimeSpan GetOffsetForDate(IConfiguration configuration, DateOnly fecha)
    {
        var tz = ResolveEmpresaTimeZone(configuration);
        var dt = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return tz.GetUtcOffset(dt);
    }

    public static DateOnly GetCurrentDateInEmpresaTimeZone(IConfiguration configuration)
    {
        var tz = ResolveEmpresaTimeZone(configuration);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(localNow.DateTime);
    }
}

