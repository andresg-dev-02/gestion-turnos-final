using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GestionTurnos.Shared.EMAIL
{
    /// <summary>
    /// Envío SMTP con MailKit (System.Net.Mail.SmtpClient está desaconsejado por Microsoft para código nuevo).
    /// Abre una conexión por mensaje: es thread-safe sin bloqueos y adecuado para el volumen transaccional del sistema.
    /// </summary>
    public sealed class EmailService : IEmailService
    {
        private readonly IOptionsMonitor<EmailSettings> _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptionsMonitor<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            var settings = _settings.CurrentValue;
            var mime = BuildMimeMessage(message, settings);

            if (!settings.Enabled)
            {
                await WriteToPickupOrLogAsync(mime, message, settings, cancellationToken);
                return;
            }

            using var client = new SmtpClient
            {
                Timeout = (int)TimeSpan.FromSeconds(Math.Max(settings.TimeoutSeconds, 1)).TotalMilliseconds
            };

            try
            {
                await client.ConnectAsync(settings.Host, settings.Port, ParseSecureSocket(settings.SecureSocket), cancellationToken);

                if (!string.IsNullOrWhiteSpace(settings.Username))
                {
                    await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
                }

                await client.SendAsync(mime, cancellationToken);

                _logger.LogInformation(
                    "Correo [{Category}] enviado a {Recipient}. MessageId: {MessageId}",
                    message.Category, EmailMask.Mask(message.ToAddress), mime.MessageId);
            }
            catch (AuthenticationException ex)
            {
                throw new EmailPermanentException("El servidor SMTP rechazó las credenciales configuradas.", ex);
            }
            catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted
                                                   or SmtpErrorCode.SenderNotAccepted
                                                   || (int)ex.StatusCode >= 500)
            {
                // 5xx = error permanente según RFC 5321: reintentar no cambia el resultado.
                throw new EmailPermanentException(
                    $"El servidor SMTP rechazó el mensaje ({(int)ex.StatusCode}): {ex.Message}", ex);
            }
            finally
            {
                if (client.IsConnected)
                {
                    try
                    {
                        await client.DisconnectAsync(true, CancellationToken.None);
                    }
                    catch (Exception ex) when (ex is ServiceNotConnectedException or IOException)
                    {
                        // El mensaje ya se entregó o falló; un cierre fallido no debe ocultar el resultado.
                    }
                }
            }
        }

        private static MimeMessage BuildMimeMessage(EmailMessage message, EmailSettings settings)
        {
            if (!MailboxAddress.TryParse(message.ToAddress, out var to))
            {
                throw new EmailPermanentException("La dirección de destino no es válida.");
            }

            if (!string.IsNullOrWhiteSpace(message.ToName))
            {
                to.Name = message.ToName;
            }

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
            mime.To.Add(to);

            if (!string.IsNullOrWhiteSpace(settings.ReplyToAddress))
            {
                mime.ReplyTo.Add(MailboxAddress.Parse(settings.ReplyToAddress));
            }

            mime.Subject = message.Subject;

            // Correo automático: evita respuestas de "fuera de la oficina" y bucles (RFC 3834).
            mime.Headers.Add("Auto-Submitted", "auto-generated");

            var body = new BodyBuilder { HtmlBody = message.HtmlBody };
            if (!string.IsNullOrWhiteSpace(message.TextBody))
            {
                body.TextBody = message.TextBody;
            }

            if (message.LinkedResources != null)
            {
                foreach (var res in message.LinkedResources)
                {
                    var part = body.LinkedResources.Add(res.ContentId, res.Data, ContentType.Parse(res.ContentType));
                    part.ContentId = res.ContentId;
                }
            }

            mime.Body = body.ToMessageBody();
            return mime;
        }

        private async Task WriteToPickupOrLogAsync(
            MimeMessage mime,
            EmailMessage message,
            EmailSettings settings,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(settings.PickupDirectory))
            {
                Directory.CreateDirectory(settings.PickupDirectory);
                var path = Path.Combine(
                    settings.PickupDirectory,
                    $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{message.Category ?? "correo"}_{Guid.NewGuid():N}.eml");

                await mime.WriteToAsync(path, cancellationToken);
                _logger.LogInformation(
                    "EmailSettings:Enabled=false. Correo [{Category}] para {Recipient} guardado en {Path}.",
                    message.Category, EmailMask.Mask(message.ToAddress), path);
                return;
            }

            _logger.LogInformation(
                "EmailSettings:Enabled=false. Correo [{Category}] para {Recipient} NO enviado. Asunto: {Subject}",
                message.Category, EmailMask.Mask(message.ToAddress), message.Subject);
        }

        private static SecureSocketOptions ParseSecureSocket(string? value) =>
            Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var option)
                ? option
                : SecureSocketOptions.StartTls;
    }
}
