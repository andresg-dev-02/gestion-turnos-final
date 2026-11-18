using System.Threading.Channels;
using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Shared.EMAIL;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Integrations.Email;

/// <summary>
/// Cola acotada en memoria entre la petición HTTP (productor) y <see cref="DespachadorCorreosService"/> (consumidor).
/// Acotada a propósito: ante un SMTP caído no crece sin límite ni agota la memoria del proceso.
/// </summary>
public sealed class ColaCorreos
{
    private readonly Channel<EmailMessage> _canal;

    public ColaCorreos(IOptions<NotificacionesSettings> settings)
    {
        _canal = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(Math.Max(settings.Value.CapacidadCola, 10))
        {
            FullMode = BoundedChannelFullMode.Wait, // con TryWrite: devuelve false en lugar de descartar en silencio
            SingleReader = false,
            SingleWriter = false
        });
    }

    public bool TryEncolar(EmailMessage mensaje) => _canal.Writer.TryWrite(mensaje);

    public ChannelReader<EmailMessage> Lector => _canal.Reader;
}
