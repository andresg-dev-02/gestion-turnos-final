namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Modo de cifrado y seguridad SSL/TLS para el protocolo FTP.
    /// </summary>
    public enum FtpEncryptionMode
    {
        /// <summary>FTP estándar sin cifrado (puerto 21 por defecto).</summary>
        None = 0,
        /// <summary>FTPS explícito: Inicia en plano y negocia TLS mediante comando AUTH TLS (puerto 21).</summary>
        Explicit = 1,
        /// <summary>FTPS implícito: Requiere TLS desde el inicio de la conexión (puerto 990 por defecto).</summary>
        Implicit = 2
    }

    /// <summary>
    /// Tipo de conexión de datos para transferencia FTP.
    /// </summary>
    public enum FtpDataConnectionType
    {
        /// <summary>Detección automática del mejor modo pasivo (recomendado).</summary>
        AutoPassive = 0,
        /// <summary>Modo pasivo estándar (PASV).</summary>
        PASV = 1,
        /// <summary>Modo pasivo extendido IPv6/IPv4 (EPSV).</summary>
        EPSV = 2,
        /// <summary>Modo activo estándar (PORT).</summary>
        PORT = 3
    }

    /// <summary>
    /// Configuración fuertemente tipada para el servicio FTP de la Gobernación del Cauca.
    /// </summary>
    public class FtpSettings
    {
        public const string SectionName = "FtpSettings";

        /// <summary>
        /// Dirección IP o nombre de host del servidor FTP.
        /// </summary>
        public string Host { get; set; } = "192.168.20.23";

        /// <summary>
        /// Puerto del servidor FTP (Por defecto: 21 para FTP/FTPS Explícito, 990 para FTPS Implícito).
        /// </summary>
        public int Port { get; set; } = 21;

        /// <summary>
        /// Nombre de usuario para la autenticación FTP.
        /// </summary>
        public string Username { get; set; } = "SICCO@@123";

        /// <summary>
        /// Contraseña de autenticación (en texto plano o cifrada mediante ICryptography).
        /// </summary>
        public string Password { get; set; } = "SICCO123@@";

        /// <summary>
        /// Indica si la contraseña almacenada en la configuración se encuentra cifrada con DataProtection/ICryptography.
        /// Si es true, el servicio la descifrará automáticamente en tiempo de ejecución.
        /// </summary>
        public bool IsPasswordEncrypted { get; set; } = false;

        /// <summary>
        /// Directorio raíz base en el servidor FTP donde se estructurarán las carpetas. Por defecto "/".
        /// </summary>
        public string RemoteRootPath { get; set; } = "/mnt/array1/GOBERNACION/Caqueta";

        /// <summary>
        /// Modo de cifrado FTPS (None, Explicit, Implicit). Por defecto: None.
        /// </summary>
        public FtpEncryptionMode EncryptionMode { get; set; } = FtpEncryptionMode.None;

        /// <summary>
        /// Tipo de conexión de datos (AutoPassive, PASV, EPSV, PORT). Por defecto: AutoPassive.
        /// </summary>
        public FtpDataConnectionType DataConnectionType { get; set; } = FtpDataConnectionType.AutoPassive;

        /// <summary>
        /// Tiempo de espera en segundos para comandos y transferencias FTP. Por defecto: 30 segundos.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Tiempo de espera en segundos para el establecimiento de la conexión TCP. Por defecto: 15 segundos.
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Tiempo de espera en segundos para operaciones de lectura/descarga. Por defecto: 30 segundos.
        /// </summary>
        public int ReadTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Número máximo de conexiones simultáneas en el pool para operaciones masivas. Por defecto: 10.
        /// </summary>
        public int MaxConnections { get; set; } = 10;

        /// <summary>
        /// Número de reintentos automáticos ante errores transitorios de red. Por defecto: 3.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Tiempo de espera inicial entre reintentos en milisegundos (con backoff exponencial). Por defecto: 1000 ms.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Habilita paquetes TCP KeepAlive en la conexión. Por defecto: true.
        /// </summary>
        public bool KeepAlive { get; set; } = true;

        /// <summary>
        /// Tamaño del búfer de transferencia de datos en bytes. Por defecto: 64 KB (65536 bytes).
        /// </summary>
        public int BufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// Valida la consistencia de los parámetros mínimos requeridos.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Host))
                throw new InvalidOperationException("La configuración FTP requiere especificar el parámetro 'Host'.");

            if (Port <= 0 || Port > 65535)
                throw new InvalidOperationException($"El puerto FTP '{Port}' es inválido. Debe estar entre 1 y 65535.");

            if (string.IsNullOrWhiteSpace(Username))
                throw new InvalidOperationException("La configuración FTP requiere especificar el parámetro 'Username'.");

            if (TimeoutSeconds <= 0)
                TimeoutSeconds = 30;

            if (MaxConnections <= 0)
                MaxConnections = 10;

            if (RetryCount < 0)
                RetryCount = 3;
        }
    }
}

