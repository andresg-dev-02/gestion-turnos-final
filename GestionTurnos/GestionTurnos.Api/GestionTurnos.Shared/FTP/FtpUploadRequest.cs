namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Representa una solicitud para cargar o almacenar un archivo en el servidor FTP.
    /// </summary>
    public class FtpUploadRequest
    {
        /// <summary>
        /// Ruta del directorio remoto de destino (ej. "/Liquidaciones/2026/09/Automotores").
        /// Si alguna de las subcarpetas separadas por '/' no existe, se creará recursivamente de forma automática.
        /// </summary>
        public string RemoteDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Nombre deseado para el archivo (ej. "liquidacion_98765.pdf").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Flujo de datos del contenido del archivo.
        /// </summary>
        public Stream? ContentStream { get; set; }

        /// <summary>
        /// Arreglo de bytes del contenido del archivo (si no se proporciona un ContentStream).
        /// </summary>
        public byte[]? ContentBytes { get; set; }

        /// <summary>
        /// Tipo MIME del contenido (ej. "application/pdf", "image/png"). Opcional.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Si es true, el servicio antepondrá un GUID único al nombre del archivo para garantizar atomicidad y prevenir colisiones.
        /// Formato resultante: "{Guid}_{FileName}". Por defecto: true.
        /// </summary>
        public bool UseGuidPrefix { get; set; } = true;

        /// <summary>
        /// GUID personalizado a utilizar si UseGuidPrefix es true. Si es null, se generará un nuevo Guid.NewGuid().
        /// </summary>
        public Guid? CustomGuid { get; set; }

        /// <summary>
        /// Si es true y ya existe un archivo con el mismo nombre en la ruta, se sobrescribirá.
        /// Si es false y el archivo existe, generará un error o nombre alternativo. Por defecto: false.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// Obtiene un Stream legible para la transferencia FTP a partir de ContentStream o ContentBytes.
        /// </summary>
        public Stream GetReadableStream()
        {
            if (ContentStream != null)
            {
                if (ContentStream.CanSeek && ContentStream.Position != 0)
                {
                    ContentStream.Position = 0;
                }
                return ContentStream;
            }

            if (ContentBytes != null && ContentBytes.Length > 0)
            {
                return new MemoryStream(ContentBytes, writable: false);
            }

            throw new InvalidOperationException($"La solicitud para '{FileName}' no contiene datos válidos en ContentStream ni ContentBytes.");
        }

        /// <summary>
        /// Crea una solicitud de carga a partir de un Stream.
        /// </summary>
        public static FtpUploadRequest FromStream(string remoteDirectory, string fileName, Stream stream, bool useGuidPrefix = true, bool overwrite = false, string? contentType = null)
        {
            return new FtpUploadRequest
            {
                RemoteDirectory = remoteDirectory,
                FileName = fileName,
                ContentStream = stream,
                UseGuidPrefix = useGuidPrefix,
                Overwrite = overwrite,
                ContentType = contentType
            };
        }

        /// <summary>
        /// Crea una solicitud de carga a partir de un arreglo de bytes.
        /// </summary>
        public static FtpUploadRequest FromBytes(string remoteDirectory, string fileName, byte[] bytes, bool useGuidPrefix = true, bool overwrite = false, string? contentType = null)
        {
            return new FtpUploadRequest
            {
                RemoteDirectory = remoteDirectory,
                FileName = fileName,
                ContentBytes = bytes,
                UseGuidPrefix = useGuidPrefix,
                Overwrite = overwrite,
                ContentType = contentType
            };
        }
    }
}
