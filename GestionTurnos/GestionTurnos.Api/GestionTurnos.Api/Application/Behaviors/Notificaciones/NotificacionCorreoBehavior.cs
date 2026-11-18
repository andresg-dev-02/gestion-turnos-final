using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Domain.Shared;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Application.Behaviors.Notificaciones;

/// <summary>
/// Base de los behaviors que notifican por correo DESPUÉS de que un comando existente termina con éxito.
///
/// Por qué un behavior y no un evento de dominio: <c>UnitOfWork</c> publica los eventos ANTES de SaveChanges y dentro
/// de la transacción, así que un correo enviado desde un handler de evento saldría aunque la transacción se revirtiera.
/// Aquí el correo solo se encola cuando el handler ya confirmó y devolvió <c>Result.IsSuccess</c>, sin tocar el handler.
///
/// Garantías:
///  - Nunca altera la respuesta del comando ni lanza excepciones hacia él (fallos solo al log).
///  - La captura previa (<see cref="CapturarAntesAsync"/>) permite comparar el estado anterior y el nuevo.
///  - Usa <see cref="CancellationToken.None"/> tras el commit: si el cliente cancela, el cambio ya está hecho y debe avisarse.
/// </summary>
public abstract class NotificacionCorreoBehavior<TRequest, TResponse, TCaptura> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
    where TCaptura : class
{
    private readonly IOptionsMonitor<NotificacionesSettings> _settings;
    protected ILogger Logger { get; }

    protected NotificacionCorreoBehavior(IOptionsMonitor<NotificacionesSettings> settings, ILogger logger)
    {
        _settings = settings;
        Logger = logger;
    }

    /// <summary>Indica si el evento concreto está activado en <see cref="NotificacionesEventosSettings"/>.</summary>
    protected abstract bool EventoHabilitado(NotificacionesEventosSettings eventos);

    /// <summary>Lee el estado previo necesario para la notificación. <c>null</c> = no notificar.</summary>
    protected virtual Task<TCaptura?> CapturarAntesAsync(TRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<TCaptura?>(null);

    /// <summary>Indica si la notificación necesita una captura previa no nula.</summary>
    protected virtual bool RequiereCaptura => false;

    protected abstract Task NotificarAsync(TRequest request, TResponse response, TCaptura? captura, CancellationToken cancellationToken);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var settings = _settings.CurrentValue;
        bool activo = settings.Habilitado && EventoHabilitado(settings.Eventos);

        TCaptura? captura = null;
        if (activo && RequiereCaptura)
        {
            try
            {
                captura = await CapturarAntesAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex, "No se pudo leer el estado previo para notificar {Request}; la operación continúa sin correo.", typeof(TRequest).Name);
            }
        }

        var response = await next();

        if (!activo || response.IsFailure || (RequiereCaptura && captura is null))
        {
            return response;
        }

        try
        {
            await NotificarAsync(request, response, captura, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Falló la preparación del correo de {Request}. La operación de negocio ya fue confirmada.", typeof(TRequest).Name);
        }

        return response;
    }
}
