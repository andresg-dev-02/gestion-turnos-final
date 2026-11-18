namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Resultado individual para cada archivo procesado dentro de una operación masiva / por lote.
    /// </summary>
    public class FtpBatchItemResult
    {
        /// <summary>
        /// Posición u orden del elemento en la solicitud por lote.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Nombre del archivo procesado.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Ruta remota absoluta de destino en el servidor FTP.
        /// </summary>
        public string RemoteFullPath { get; set; } = string.Empty;

        /// <summary>
        /// Identificador único GUID asignado al archivo (si aplicó atomicidad).
        /// </summary>
        public Guid? FileId { get; set; }

        /// <summary>
        /// Indica si el archivo fue transferido exitosamente.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Mensaje de error en caso de que este archivo en particular haya fallado.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Tamaño del archivo transferido en bytes.
        /// </summary>
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Resultado consolidado de una operación masiva o por lote (ej. 500 liquidaciones simultáneas).
    /// Provee estadísticas, trazabilidad por archivo y estado de rollback transaccional.
    /// </summary>
    public class FtpBatchResult
    {
        /// <summary>
        /// Total de archivos recibidos para procesamiento en el lote.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Cantidad de archivos transferidos satisfactoriamente.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Cantidad de archivos que presentaron errores en la transferencia.
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Indica si todo el lote se procesó al 100% con éxito sin fallos.
        /// </summary>
        public bool IsSuccess => FailedCount == 0 && TotalCount > 0;

        /// <summary>
        /// Indica si se ejecutó un Rollback compensatorio automático (eliminación de archivos subidos)
        /// tras detectarse un error en modo transaccional.
        /// </summary>
        public bool IsRolledBack { get; set; }

        /// <summary>
        /// Tiempo total transcurrido en el procesamiento del lote en milisegundos.
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// Lista detallada del resultado de cada elemento del lote.
        /// </summary>
        public List<FtpBatchItemResult> Items { get; set; } = new();

        /// <summary>
        /// Lista consolidada de mensajes de error ocurridos durante la operación.
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}
