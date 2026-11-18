namespace GestionTurnos.Shared.EMAIL
{
    /// <summary>
    /// Envío de correo electrónico transaccional. Implementación thread-safe registrada como singleton.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Envía el mensaje. Lanza <see cref="EmailPermanentException"/> si el error no se resuelve reintentando
        /// (dirección inválida, buzón inexistente, autenticación rechazada); cualquier otra excepción se considera transitoria.
        /// </summary>
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Recurso enlazado para imágenes inline (p. ej. logo corporativo referenciado por CID).
    /// </summary>
    public sealed record EmailLinkedResource(
        string ContentId,
        string ContentType,
        byte[] Data);

    /// <summary>
    /// Mensaje de correo a enviar. Conviene incluir siempre una versión en texto plano además del HTML
    /// (mejora la entregabilidad y la accesibilidad).
    /// </summary>
    public sealed record EmailMessage(
        string ToAddress,
        string? ToName,
        string Subject,
        string HtmlBody,
        string? TextBody = null,
        IReadOnlyList<EmailLinkedResource>? LinkedResources = null)
    {
        /// <summary>Etiqueta libre para trazabilidad en logs (p. ej. "registro_aprobado").</summary>
        public string? Category { get; init; }
    }

    /// <summary>
    /// Error de envío que no tiene sentido reintentar.
    /// </summary>
    public sealed class EmailPermanentException : Exception
    {
        public EmailPermanentException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Utilidades para no exponer direcciones de correo completas en logs ni en respuestas (datos personales).
    /// </summary>
    public static class EmailMask
    {
        /// <summary>"juan.perez@dominio.com" → "j***@dominio.com".</summary>
        public static string Mask(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return "(vacío)";
            }

            var at = address.IndexOf('@');
            if (at <= 0)
            {
                return "***";
            }

            return $"{address[0]}***{address[at..]}";
        }
    }
}
