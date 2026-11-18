using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Shared.EMAIL;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Integrations.Email;

/// <summary>
/// Servicio en segundo plano que consume <see cref="ColaCorreos"/> y envía por SMTP con reintentos
/// exponenciales (con jitter) ante errores transitorios. Los errores permanentes no se reintentan.
/// </summary>
public sealed class DespachadorCorreosService : BackgroundService
{
    private readonly ColaCorreos _cola;
    private readonly IEmailService _emailService;
    private readonly IOptionsMonitor<NotificacionesSettings> _settings;
    private readonly ILogger<DespachadorCorreosService> _logger;

    public DespachadorCorreosService(
        ColaCorreos cola,
        IEmailService emailService,
        IOptionsMonitor<NotificacionesSettings> settings,
        ILogger<DespachadorCorreosService> logger)
    {
        _cola = cola;
        _emailService = emailService;
        _settings = settings;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumidores = Math.Clamp(_settings.CurrentValue.GradoParalelismo, 1, 8);
        return Task.WhenAll(Enumerable.Range(0, consumidores).Select(_ => ConsumirAsync(stoppingToken)));
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mensaje in _cola.Lector.ReadAllAsync(stoppingToken))
            {
                await EnviarConReintentosAsync(mensaje, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (_cola.Lector.CanCount && _cola.Lector.Count > 0)
            {
                _logger.LogWarning("Apagado de la aplicación con {Pendientes} correos sin enviar en la cola.", _cola.Lector.Count);
            }
        }
    }

    private async Task EnviarConReintentosAsync(EmailMessage mensaje, CancellationToken stoppingToken)
    {
        var maximoReintentos = Math.Max(_settings.CurrentValue.MaximoReintentos, 0);

        for (var intento = 0; ; intento++)
        {
            try
            {
                await _emailService.SendAsync(mensaje, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (EmailPermanentException ex)
            {
                _logger.LogError(ex, "Correo [{Category}] para {Recipient} descartado por error permanente.",
                    mensaje.Category, EmailMask.Mask(mensaje.ToAddress));
                return;
            }
            catch (Exception ex) when (intento < maximoReintentos)
            {
                var espera = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, intento + 1), 60))
                             + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

                _logger.LogWarning(ex, "Fallo transitorio enviando correo [{Category}] a {Recipient}. Reintento {Intento}/{Maximo} en {Espera}.",
                    mensaje.Category, EmailMask.Mask(mensaje.ToAddress), intento + 1, maximoReintentos, espera);

                await Task.Delay(espera, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Correo [{Category}] para {Recipient} descartado tras {Intentos} intentos.",
                    mensaje.Category, EmailMask.Mask(mensaje.ToAddress), intento + 1);
                return;
            }
        }
    }
}
