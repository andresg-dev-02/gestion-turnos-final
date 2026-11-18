namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Destinatario de un correo.
/// </summary>
public sealed record DestinatarioCorreo(string Correo, string? Nombre);

/// <summary>
/// Renderiza una plantilla de correo y la deja en la cola de envío en segundo plano.
/// El envío real ocurre fuera de la petición HTTP: la latencia o caída del SMTP nunca bloquea ni revierte
/// una operación de negocio.
/// </summary>
public interface INotificadorCorreo
{
    /// <summary>
    /// Encola el correo. No lanza excepciones: devuelve <c>false</c> si no fue posible renderizarlo o encolarlo
    /// (el detalle queda en el log).
    /// </summary>
    /// <param name="plantilla">Nombre de la plantilla (ver <see cref="Notificaciones.PlantillasCorreo"/>).</param>
    /// <param name="destinatario">Correo y nombre del destinatario.</param>
    /// <param name="asunto">Asunto del correo.</param>
    /// <param name="modelo">Datos de la plantilla; en Scriban se accede como <c>modelo.nombre_en_snake_case</c>.</param>
    Task<bool> EncolarAsync(
        string plantilla,
        DestinatarioCorreo destinatario,
        string asunto,
        object modelo,
        CancellationToken cancellationToken = default);
}
