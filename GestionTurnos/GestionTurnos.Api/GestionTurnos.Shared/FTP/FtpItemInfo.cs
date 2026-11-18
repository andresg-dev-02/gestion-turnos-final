namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Información sobre un elemento (archivo o directorio) almacenado en el servidor FTP.
    /// </summary>
    public class FtpItemInfo
    {
        /// <summary>
        /// Nombre del archivo o directorio (ej. "liquidacion_01.pdf" o "2026").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Ruta remota absoluta completa en el servidor FTP.
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Indica si el elemento es un directorio.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Tamaño del archivo en bytes (0 para directorios).
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Fecha y hora de última modificación en UTC reportada por el servidor FTP.
        /// </summary>
        public DateTime? ModifiedAtUtc { get; set; }

        /// <summary>
        /// Extensión del archivo (ej. ".pdf", ".zip"), o vacía si es un directorio.
        /// </summary>
        public string Extension { get; set; } = string.Empty;
    }
}
