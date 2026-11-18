namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Resultado de una operación de archivo FTP (carga, descarga, actualización o consulta).
    /// </summary>
    public class FtpFileResult : IDisposable, IAsyncDisposable
    {
        private Stream? _contentStream;

        /// <summary>
        /// Identificador único GUID del documento si fue asignado o detectado en el nombre.
        /// </summary>
        public Guid? FileId { get; set; }

        /// <summary>
        /// Nombre físico del archivo en el servidor FTP (ej. "a1b2c3d4-e5f6-7890-1234-56789abcdef0_liquidacion.pdf").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Nombre original del archivo sin el prefijo GUID (ej. "liquidacion.pdf").
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Directorio remoto donde se aloja el archivo (ej. "/Liquidaciones/2026/09/Automotores").
        /// </summary>
        public string RemoteDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Ruta absoluta completa en el servidor FTP (ej. "/Liquidaciones/2026/09/Automotores/a1b2c3d4..._liquidacion.pdf").
        /// </summary>
        public string RemoteFullPath { get; set; } = string.Empty;

        /// <summary>
        /// Extensión del archivo (ej. ".pdf").
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de contenido MIME inferido o asignado (ej. "application/pdf").
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Tamaño del archivo en bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Contenido en bytes del archivo si fue cargado en memoria.
        /// </summary>
        public byte[]? ContentBytes { get; set; }

        /// <summary>
        /// Flujo de lectura del contenido del archivo.
        /// </summary>
        public Stream? ContentStream
        {
            get => _contentStream;
            set => _contentStream = value;
        }

        /// <summary>
        /// Fecha y hora de creación o carga del documento (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha y hora de última modificación según el servidor FTP (UTC).
        /// </summary>
        public DateTime? ModifiedAtUtc { get; set; }

        /// <summary>
        /// Indica si la operación fue exitosa.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Mensaje de error descriptivo en caso de fallo.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Obtiene los bytes del archivo, leyéndolos desde el stream si es necesario.
        /// </summary>
        public async Task<byte[]> GetBytesAsync(CancellationToken ct = default)
        {
            if (ContentBytes != null && ContentBytes.Length > 0)
            {
                return ContentBytes;
            }

            if (_contentStream != null)
            {
                if (_contentStream is MemoryStream ms)
                {
                    ContentBytes = ms.ToArray();
                    return ContentBytes;
                }

                using var memoryStream = new MemoryStream();
                if (_contentStream.CanSeek)
                {
                    _contentStream.Position = 0;
                }
                await _contentStream.CopyToAsync(memoryStream, ct);
                ContentBytes = memoryStream.ToArray();
                return ContentBytes;
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Crea un resultado exitoso para una operación FTP.
        /// </summary>
        public static FtpFileResult Succeeded(
            string remoteFullPath,
            string fileName,
            string originalFileName,
            string remoteDirectory,
            long sizeBytes,
            Guid? fileId = null,
            Stream? contentStream = null,
            byte[]? contentBytes = null,
            string? contentType = null,
            DateTime? modifiedAtUtc = null)
        {
            var ext = Path.GetExtension(fileName);
            return new FtpFileResult
            {
                Success = true,
                RemoteFullPath = remoteFullPath,
                FileName = fileName,
                OriginalFileName = string.IsNullOrWhiteSpace(originalFileName) ? fileName : originalFileName,
                RemoteDirectory = remoteDirectory,
                SizeBytes = sizeBytes,
                FileId = fileId,
                ContentStream = contentStream,
                ContentBytes = contentBytes,
                Extension = ext,
                ContentType = contentType ?? GetMimeType(ext),
                ModifiedAtUtc = modifiedAtUtc,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Crea un resultado fallido para una operación FTP.
        /// </summary>
        public static FtpFileResult Failed(string errorMessage, string? remotePath = null)
        {
            return new FtpFileResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                RemoteFullPath = remotePath ?? string.Empty
            };
        }

        /// <summary>
        /// Mapeo estándar de tipos MIME frecuentes para documentos de la Gobernación.
        /// </summary>
        public static string GetMimeType(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return "application/octet-stream";

            var ext = extension.TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "pdf" => "application/pdf",
                "xml" => "application/xml",
                "json" => "application/json",
                "zip" => "application/zip",
                "txt" => "text/plain",
                "csv" => "text/csv",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xls" => "application/vnd.ms-excel",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "doc" => "application/msword",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
        }

        public void Dispose()
        {
            _contentStream?.Dispose();
            _contentStream = null;
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_contentStream != null)
            {
                await _contentStream.DisposeAsync();
                _contentStream = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}

