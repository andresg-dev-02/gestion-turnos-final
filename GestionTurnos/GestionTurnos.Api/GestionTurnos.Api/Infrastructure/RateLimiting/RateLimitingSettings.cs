namespace GestionTurnos.Api.Infrastructure.RateLimiting;

public class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";
    public bool EnableRateLimiting { get; set; } = true;
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 2;
}

