namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Contrato para el servicio de gestión de almacenamiento y transferencia de archivos FTP/FTPS
    /// de la Gobernación del Cauca.
    /// Soporta navegación y creación automática de rutas jerárquicas, atomicidad con GUIDs,
    /// pooling de conexiones de alta velocidad para procesos masivos y transaccionalidad con rollback.
    /// </summary>
    public interface IFtpService
    {
        #region 1. Creación y Carga de Archivos (Create / Upload)

        /// <summary>
        /// Sube un archivo a partir de un Stream en el directorio remoto especificado.
        /// Si alguna carpeta en la ruta no existe, se creará recursivamente de forma automática.
        /// </summary>
        /// <param name="remoteDirectory">Ruta del directorio destino (ej. "/Liquidaciones/2026/09/Automotores").</param>
        /// <param name="fileName">Nombre del archivo (ej. "liquidacion_001.pdf").</param>
        /// <param name="contentStream">Flujo con los datos del documento.</param>
        /// <param name="useGuid">Indica si se antepone un GUID al nombre para asegurar atomicidad y evitar colisiones.</param>
        /// <param name="overwrite">Indica si debe sobrescribir el archivo en caso de existir con el mismo nombre.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpFileResult> UploadFileAsync(
            string remoteDirectory,
            string fileName,
            Stream contentStream,
            bool useGuid = true,
            bool overwrite = false,
            CancellationToken ct = default);

        /// <summary>
        /// Sube un archivo a partir de un arreglo de bytes en el directorio remoto especificado.
        /// Si alguna carpeta en la ruta no existe, se creará recursivamente de forma automática.
        /// </summary>
        /// <param name="remoteDirectory">Ruta del directorio destino (ej. "/Liquidaciones/2026/09/Automotores").</param>
        /// <param name="fileName">Nombre del archivo (ej. "liquidacion_001.pdf").</param>
        /// <param name="contentBytes">Arreglo de bytes del documento.</param>
        /// <param name="useGuid">Indica si se antepone un GUID al nombre para asegurar atomicidad y evitar colisiones.</param>
        /// <param name="overwrite">Indica si debe sobrescribir el archivo en caso de existir con el mismo nombre.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpFileResult> UploadFileAsync(
            string remoteDirectory,
            string fileName,
            byte[] contentBytes,
            bool useGuid = true,
            bool overwrite = false,
            CancellationToken ct = default);

        /// <summary>
        /// Sube un archivo a partir de un objeto <see cref="FtpUploadRequest"/> detallado.
        /// </summary>
        Task<FtpFileResult> UploadFileAsync(
            FtpUploadRequest request,
            CancellationToken ct = default);

        #endregion

        #region 2. Lectura y Descarga de Archivos (Read / Download)

        /// <summary>
        /// Descarga un archivo desde su ruta remota completa o relativa, devolviendo su contenido y metadatos.
        /// Extrae automáticamente el GUID del documento si fue nombrado con el prefijo atómico.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo en el servidor (ej. "/Liquidaciones/2026/09/Automotores/guid_liquidacion.pdf").</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpFileResult> DownloadFileAsync(
            string remoteFilePath,
            CancellationToken ct = default);

        /// <summary>
        /// Descarga únicamente el contenido binario (arreglo de bytes) de un archivo en el servidor FTP.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo en el servidor.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<byte[]> DownloadBytesAsync(
            string remoteFilePath,
            CancellationToken ct = default);

        /// <summary>
        /// Abre un Stream de lectura directo hacia el archivo en el servidor FTP.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo en el servidor.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<Stream> OpenReadStreamAsync(
            string remoteFilePath,
            CancellationToken ct = default);

        #endregion

        #region 3. Actualización y Sobrescritura (Update)

        /// <summary>
        /// Actualiza o sobrescribe el contenido de un archivo existente en la ruta remota especificada.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo a actualizar.</param>
        /// <param name="contentStream">Nuevo flujo de datos.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpFileResult> UpdateFileAsync(
            string remoteFilePath,
            Stream contentStream,
            CancellationToken ct = default);

        /// <summary>
        /// Actualiza o sobrescribe el contenido de un archivo existente en la ruta remota especificada con un arreglo de bytes.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo a actualizar.</param>
        /// <param name="contentBytes">Nuevos bytes del documento.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpFileResult> UpdateFileAsync(
            string remoteFilePath,
            byte[] contentBytes,
            CancellationToken ct = default);

        #endregion

        #region 4. Eliminación de Archivos y Directorios (Delete)

        /// <summary>
        /// Elimina un archivo específico ubicado en la ruta remota.
        /// </summary>
        /// <param name="remoteFilePath">Ruta completa del archivo a eliminar.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<bool> DeleteFileAsync(
            string remoteFilePath,
            CancellationToken ct = default);

        /// <summary>
        /// Elimina un directorio remoto.
        /// </summary>
        /// <param name="remoteDirectoryPath">Ruta del directorio a eliminar.</param>
        /// <param name="recursive">Indica si se deben eliminar todos los archivos y subcarpetas contenidas recursivamente.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<bool> DeleteDirectoryAsync(
            string remoteDirectoryPath,
            bool recursive = true,
            CancellationToken ct = default);

        #endregion

        #region 5. Consultas, Listado y Navegación (Query / Navigation)

        /// <summary>
        /// Verifica si un archivo existe en la ruta remota indicada.
        /// </summary>
        Task<bool> FileExistsAsync(
            string remoteFilePath,
            CancellationToken ct = default);

        /// <summary>
        /// Verifica si un directorio existe en la ruta remota indicada.
        /// </summary>
        Task<bool> DirectoryExistsAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default);

        /// <summary>
        /// Lista todos los archivos contenidos en el directorio remoto especificado.
        /// </summary>
        /// <param name="remoteDirectoryPath">Ruta del directorio a inspeccionar.</param>
        /// <param name="recursive">Si es true, busca también en todas las subcarpetas de manera recursiva.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<IReadOnlyList<FtpItemInfo>> ListFilesAsync(
            string remoteDirectoryPath,
            bool recursive = false,
            CancellationToken ct = default);

        /// <summary>
        /// Lista los subdirectorios contenidos en el directorio remoto especificado.
        /// </summary>
        Task<IReadOnlyList<FtpItemInfo>> ListDirectoriesAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default);

        /// <summary>
        /// Crea recursivamente toda la estructura de carpetas de una ruta remota si no existen.
        /// </summary>
        /// <param name="remoteDirectoryPath">Ruta con los niveles de carpetas separados por '/' (ej. "/Liquidaciones/2026/09/Automotores").</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<bool> CreateDirectoryRecursivelyAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default);

        #endregion

        #region 6. Procesos Masivos y Transaccionalidad (Batch / Concurrency / Rollback)

        /// <summary>
        /// Carga masiva de múltiples archivos en el servidor FTP utilizando el pool de conexiones reutilizables.
        /// Soporta modo transaccional: Si ocurre un error en cualquier archivo y 'transactional' es true,
        /// se ejecuta un Rollback automático eliminando del servidor todos los archivos que ya habían sido subidos en este lote.
        /// </summary>
        /// <param name="requests">Colección de solicitudes de carga de archivos.</param>
        /// <param name="transactional">Si es true, activa la garantía ACID/Rollback ante cualquier fallo en el lote.</param>
        /// <param name="maxDegreeOfParallelism">Grado máximo de concurrencia paralela sobre el pool de conexiones.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpBatchResult> UploadBatchAsync(
            IEnumerable<FtpUploadRequest> requests,
            bool transactional = false,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default);

        /// <summary>
        /// Elimina masivamente un listado de archivos en el servidor FTP reutilizando conexiones del pool.
        /// </summary>
        /// <param name="remoteFilePaths">Listado de rutas completas de archivos a eliminar.</param>
        /// <param name="maxDegreeOfParallelism">Grado máximo de concurrencia paralela.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<FtpBatchResult> DeleteBatchAsync(
            IEnumerable<string> remoteFilePaths,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default);

        /// <summary>
        /// Descarga masiva de un conjunto de archivos en memoria de manera paralela y segura.
        /// </summary>
        /// <param name="remoteFilePaths">Listado de rutas completas a descargar.</param>
        /// <param name="maxDegreeOfParallelism">Grado máximo de concurrencia paralela.</param>
        /// <param name="ct">Token de cancelación.</param>
        Task<IReadOnlyList<FtpFileResult>> DownloadBatchAsync(
            IEnumerable<string> remoteFilePaths,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default);

        #endregion
    }
}

