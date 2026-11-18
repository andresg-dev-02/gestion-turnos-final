using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Shared.EMAIL
{
    /// <summary>
    /// Registro del servicio de correo en el contenedor de inyección de dependencias (mismo estilo que <c>AddSharedFtp</c>).
    /// </summary>
    public static class EmailConfigurationExtensions
    {
        public static IServiceCollection AddSharedEmail(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = EmailSettings.SectionName)
        {
            services.AddOptions<EmailSettings>()
                .Bind(configuration.GetSection(sectionName))
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsValidator>();
            services.AddSingleton<IEmailService, EmailService>();

            return services;
        }
    }

    /// <summary>
    /// Falla el arranque si el SMTP está habilitado pero mal configurado, en lugar de descubrirlo en el primer envío.
    /// </summary>
    internal sealed class EmailSettingsValidator : IValidateOptions<EmailSettings>
    {
        public ValidateOptionsResult Validate(string? name, EmailSettings options)
        {
            if (!options.Enabled)
            {
                return ValidateOptionsResult.Success;
            }

            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Host))
                errores.Add("EmailSettings:Host es obligatorio cuando Enabled=true.");

            if (options.Port is <= 0 or > 65535)
                errores.Add("EmailSettings:Port debe estar entre 1 y 65535.");

            if (string.IsNullOrWhiteSpace(options.FromAddress) || !MimeKit.MailboxAddress.TryParse(options.FromAddress, out _))
                errores.Add("EmailSettings:FromAddress debe ser una dirección de correo válida.");

            if (!Enum.TryParse<SecureSocketOptions>(options.SecureSocket, ignoreCase: true, out _))
                errores.Add("EmailSettings:SecureSocket debe ser None, Auto, SslOnConnect, StartTls o StartTlsWhenAvailable.");

            if (!string.IsNullOrWhiteSpace(options.Username) && string.IsNullOrEmpty(options.Password))
                errores.Add("EmailSettings:Password es obligatorio cuando se define Username (configúrelo como secreto).");

            return errores.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(errores);
        }
    }
}
