namespace GestionTurnos.Shared.EMAIL
{
    /// <summary>
    /// Configuración fuertemente tipada del servidor SMTP de salida.
    /// Las credenciales NUNCA deben versionarse en appsettings.json: use user-secrets en desarrollo
    /// y variables de entorno (p. ej. <c>EmailSettings__Password</c>) o un almacén de secretos en producción.
    /// </summary>
    public class EmailSettings
    {
        public const string SectionName = "EmailSettings";

        /// <summary>
        /// Interruptor general. Si es <c>false</c> los mensajes no salen por SMTP: se registran en el log
        /// (o se escriben en <see cref="PickupDirectory"/> si está configurado). Útil en desarrollo y pruebas.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Nombre de host del servidor SMTP (p. ej. smtp.office365.com, smtp.gmail.com).</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>Puerto SMTP. 587 para STARTTLS (recomendado), 465 para TLS implícito.</summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Modo de seguridad del canal: <c>StartTls</c> (recomendado), <c>SslOnConnect</c>, <c>StartTlsWhenAvailable</c>,
        /// <c>Auto</c> o <c>None</c>. <c>None</c> solo debe usarse contra un relay interno de confianza.
        /// </summary>
        public string SecureSocket { get; set; } = "StartTls";

        /// <summary>Usuario de autenticación SMTP. Vacío = relay sin autenticación.</summary>
        public string? Username { get; set; }

        /// <summary>Contraseña o "app password" del usuario SMTP. Proviene de un secreto, nunca del repositorio.</summary>
        public string? Password { get; set; }

        /// <summary>Dirección del remitente (debe estar autorizada por SPF/DKIM del dominio).</summary>
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>Nombre visible del remitente.</summary>
        public string FromName { get; set; } = "Gestión de Turnos";

        /// <summary>Dirección opcional para respuestas (Reply-To). Útil si el remitente es un buzón no monitoreado.</summary>
        public string? ReplyToAddress { get; set; }

        /// <summary>Tiempo máximo de cada operación SMTP, en segundos.</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Directorio opcional donde se guardan los mensajes como archivos .eml cuando <see cref="Enabled"/> es <c>false</c>.
        /// Permite revisar el HTML real de los correos en desarrollo sin enviar nada.
        /// </summary>
        public string? PickupDirectory { get; set; }
    }
}
