using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.Services.ReporteSemanal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Integrations.Email;

/// <summary>
/// Dispara el reporte semanal el día y la hora de <see cref="ReporteSemanalSettings"/> (zona horaria de la empresa),
/// reportando la semana anterior completa (lunes a domingo).
///
/// - Despierta como máximo cada hora para recalcular: un cambio de configuración en caliente se respeta.
/// - Si la API está apagada a la hora programada, esa semana NO se reenvía al arrancar (evita duplicados tras reinicios).
/// - Con varias instancias de la API, cada una enviaría el reporte: en ese caso dejar <c>Habilitado</c> en una sola.
/// </summary>
public sealed class ReporteSemanalBackgroundService : BackgroundService
{
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<NotificacionesSettings> _settings;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReporteSemanalBackgroundService> _logger;

    public ReporteSemanalBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<NotificacionesSettings> settings,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<ReporteSemanalBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var objetivo = ProximaEjecucion(_timeProvider.GetUtcNow());
        _logger.LogInformation("Reporte semanal programado para {Objetivo:u}.", objetivo);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ahora = _timeProvider.GetUtcNow();
            var espera = objetivo - ahora;

            if (espera > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(espera < EsperaMaxima ? espera : EsperaMaxima, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                ahora = _timeProvider.GetUtcNow();
                if (ahora < objetivo)
                {
                    // Aún no es la hora: recalcular por si cambió la configuración.
                    objetivo = ProximaEjecucion(ahora);
                    continue;
                }
            }

            await EjecutarAsync(stoppingToken);
            objetivo = ProximaEjecucion(_timeProvider.GetUtcNow());
            _logger.LogInformation("Próximo reporte semanal: {Objetivo:u}.", objetivo);
        }
    }

    private async Task EjecutarAsync(CancellationToken stoppingToken)
    {
        var settings = _settings.CurrentValue;
        if (!settings.Habilitado || !settings.ReporteSemanal.Habilitado)
        {
            _logger.LogInformation("Reporte semanal omitido: está deshabilitado en la configuración.");
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IServicioReporteSemanal>();
            await servicio.EnviarAsync(
                servicio.LunesSemanaAnterior(),
                idEmpleado: null,
                settings.ReporteSemanal.EnviarSinActividad,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la aplicación.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la generación del reporte semanal.");
        }
    }

    /// <summary>Siguiente día/hora configurados, estrictamente posterior a <paramref name="ahoraUtc"/>.</summary>
    private DateTimeOffset ProximaEjecucion(DateTimeOffset ahoraUtc)
    {
        var config = _settings.CurrentValue.ReporteSemanal;
        var zona = TimeZoneHelper.ResolveEmpresaTimeZone(_configuration);
        var ahoraLocal = TimeZoneInfo.ConvertTime(ahoraUtc, zona);
        var hoy = DateOnly.FromDateTime(ahoraLocal.DateTime);

        for (int i = 0; i <= 7; i++)
        {
            var fecha = hoy.AddDays(i);
            if (fecha.DayOfWeek != config.DiaEnvio)
            {
                continue;
            }

            var local = fecha.ToDateTime(config.HoraEnvio, DateTimeKind.Unspecified);
            var candidato = new DateTimeOffset(local, zona.GetUtcOffset(local));
            if (candidato > ahoraUtc)
            {
                return candidato;
            }
        }

        // Inalcanzable: en 8 días siempre hay una ocurrencia posterior.
        return ahoraUtc.AddDays(7);
    }
}
