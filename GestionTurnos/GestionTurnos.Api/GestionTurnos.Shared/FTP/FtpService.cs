using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using FluentFTP;
using FluentFTP.Exceptions;
using GestionTurnos.Shared.CRYPTOGRAPHY;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Implementación empresarial del servicio de transferencia y almacenamiento FTP/FTPS.
    /// Incorpora Connection Pooling, reintentos con backoff exponencial, creación automática
    /// de estructuras de carpetas, atomicidad con GUIDs y transaccionalidad con compensación (Rollback).
    /// </summary>
    public class FtpService : IFtpService, IAsyncDisposable, IDisposable
    {
        private readonly FtpSettings _settings;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FtpService>? _logger;
        private readonly SemaphoreSlim _poolSemaphore;
        private readonly ConcurrentBag<AsyncFtpClient> _clientPool;
        private bool _isDisposed;

        // Patrón para detectar nombres de archivo con prefijo GUID estándar (ej. 32 caracteres hexadecimales o con guiones)
        private static readonly Regex GuidPrefixRegex = new(@"^([0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})_(.*)$", RegexOptions.Compiled);

        public FtpService(
            IOptions<FtpSettings> options,
            IServiceProvider serviceProvider,
            ILogger<FtpService>? logger = null)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _settings.Validate();
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;

            _poolSemaphore = new SemaphoreSlim(_settings.MaxConnections, _settings.MaxConnections);
            _clientPool = new ConcurrentBag<AsyncFtpClient>();
        }

        #region 1. Creación y Carga de Archivos (Create / Upload)

        public async Task<FtpFileResult> UploadFileAsync(
            string remoteDirectory,
            string fileName,
            Stream contentStream,
            bool useGuid = true,
            bool overwrite = false,
            CancellationToken ct = default)
        {
            var request = new FtpUploadRequest
            {
                RemoteDirectory = remoteDirectory,
                FileName = fileName,
                ContentStream = contentStream,
                UseGuidPrefix = useGuid,
                Overwrite = overwrite
            };

            return await UploadFileAsync(request, ct);
        }

        public async Task<FtpFileResult> UploadFileAsync(
            string remoteDirectory,
            string fileName,
            byte[] contentBytes,
            bool useGuid = true,
            bool overwrite = false,
            CancellationToken ct = default)
        {
            var request = new FtpUploadRequest
            {
                RemoteDirectory = remoteDirectory,
                FileName = fileName,
                ContentBytes = contentBytes,
                UseGuidPrefix = useGuid,
                Overwrite = overwrite
            };

            return await UploadFileAsync(request, ct);
        }

        public async Task<FtpFileResult> UploadFileAsync(
            FtpUploadRequest request,
            CancellationToken ct = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.FileName))
                throw new ArgumentException("El nombre del archivo no puede estar vacío.", nameof(request.FileName));

            var normalizedDir = NormalizeDirectoryPath(request.RemoteDirectory);
            var sanitizedOriginalName = SanitizeFileName(request.FileName);

            Guid? fileId = null;
            string finalFileName;

            if (request.UseGuidPrefix)
            {
                fileId = request.CustomGuid ?? Guid.NewGuid();
                finalFileName = $"{fileId.Value:N}_{sanitizedOriginalName}";
            }
            else
            {
                finalFileName = sanitizedOriginalName;
            }

            var fullRemotePath = CombinePaths(normalizedDir, finalFileName);

            return await ExecuteWithRetryAsync(async client =>
            {
                // Asegurar que toda la estructura de carpetas exista recursivamente en el FTP
                await EnsureDirectoryExistsInternalAsync(client, normalizedDir, ct);

                using var stream = request.GetReadableStream();
                var existsMode = request.Overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip;

                var status = await client.UploadStream(
                    stream,
                    fullRemotePath,
                    existsMode,
                    createRemoteDir: true,
                    token: ct);

                if (status == FtpStatus.Failed)
                {
                    _logger?.LogError("Error al cargar el archivo '{FileName}' en la ruta '{Path}'.", finalFileName, fullRemotePath);
                    return FtpFileResult.Failed($"Error del servidor FTP al cargar el archivo en '{fullRemotePath}'.", fullRemotePath);
                }

                if (status == FtpStatus.Skipped && !request.Overwrite)
                {
                    _logger?.LogWarning("El archivo '{Path}' ya existe en el servidor FTP y la opción de sobrescritura está desactivada.", fullRemotePath);
                    return FtpFileResult.Failed($"El archivo ya existe en '{fullRemotePath}' y no se permitió sobrescritura.", fullRemotePath);
                }

                var size = stream.CanSeek ? stream.Length : 0;

                _logger?.LogInformation("Archivo cargado exitosamente en FTP: '{Path}' (Tamaño: {Size} bytes).", fullRemotePath, size);

                return FtpFileResult.Succeeded(
                    remoteFullPath: fullRemotePath,
                    fileName: finalFileName,
                    originalFileName: sanitizedOriginalName,
                    remoteDirectory: normalizedDir,
                    sizeBytes: size,
                    fileId: fileId,
                    contentType: request.ContentType);

            }, ct);
        }

        #endregion

        #region 2. Lectura y Descarga de Archivos (Read / Download)

        public async Task<FtpFileResult> DownloadFileAsync(
            string remoteFilePath,
            CancellationToken ct = default)
        {
            var normalizedPath = NormalizeFilePath(remoteFilePath);

            return await ExecuteWithRetryAsync(async client =>
            {
                var exists = await client.FileExists(normalizedPath, ct);
                if (!exists)
                {
                    _logger?.LogWarning("No se encontró el archivo remoto en '{Path}'.", normalizedPath);
                    return FtpFileResult.Failed($"El archivo no existe en la ruta '{normalizedPath}'.", normalizedPath);
                }

                var memoryStream = new MemoryStream();
                var success = await client.DownloadStream(memoryStream, normalizedPath, token: ct);

                if (!success)
                {
                    _logger?.LogError("Fallo al descargar el archivo desde '{Path}'.", normalizedPath);
                    return FtpFileResult.Failed($"No se pudo descargar el archivo de '{normalizedPath}'.", normalizedPath);
                }

                memoryStream.Position = 0;
                var bytes = memoryStream.ToArray();
                var (fileId, originalName, fileName) = ParseFileNameAndGuid(Path.GetFileName(normalizedPath));
                var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? "/";

                DateTime? modifiedUtc = null;
                try
                {
                    modifiedUtc = await client.GetModifiedTime(normalizedPath, ct);
                }
                catch
                {
                    // Algunos servidores FTP no implementan MDTM
                }

                return FtpFileResult.Succeeded(
                    remoteFullPath: normalizedPath,
                    fileName: fileName,
                    originalFileName: originalName,
                    remoteDirectory: directory,
                    sizeBytes: bytes.Length,
                    fileId: fileId,
                    contentStream: new MemoryStream(bytes, writable: false),
                    contentBytes: bytes,
                    modifiedAtUtc: modifiedUtc);

            }, ct);
        }

        public async Task<byte[]> DownloadBytesAsync(
            string remoteFilePath,
            CancellationToken ct = default)
        {
            var result = await DownloadFileAsync(remoteFilePath, ct);
            if (!result.Success)
            {
                throw new FileNotFoundException(result.ErrorMessage ?? $"No se pudo obtener el archivo '{remoteFilePath}'.");
            }

            return await result.GetBytesAsync(ct);
        }

        public async Task<Stream> OpenReadStreamAsync(
            string remoteFilePath,
            CancellationToken ct = default)
        {
            var result = await DownloadFileAsync(remoteFilePath, ct);
            if (!result.Success || result.ContentStream == null)
            {
                throw new FileNotFoundException(result.ErrorMessage ?? $"No se pudo abrir el archivo '{remoteFilePath}'.");
            }

            return result.ContentStream;
        }

        #endregion

        #region 3. Actualización y Sobrescritura (Update)

        public async Task<FtpFileResult> UpdateFileAsync(
            string remoteFilePath,
            Stream contentStream,
            CancellationToken ct = default)
        {
            if (contentStream == null)
                throw new ArgumentNullException(nameof(contentStream));

            var normalizedPath = NormalizeFilePath(remoteFilePath);
            var directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? "/";
            var fileName = Path.GetFileName(normalizedPath);
            var (fileId, originalName, _) = ParseFileNameAndGuid(fileName);

            return await ExecuteWithRetryAsync(async client =>
            {
                if (contentStream.CanSeek && contentStream.Position != 0)
                {
                    contentStream.Position = 0;
                }

                var status = await client.UploadStream(
                    contentStream,
                    normalizedPath,
                    FtpRemoteExists.Overwrite,
                    createRemoteDir: true,
                    token: ct);

                if (status == FtpStatus.Failed)
                {
                    return FtpFileResult.Failed($"Fallo al actualizar el archivo en '{normalizedPath}'.", normalizedPath);
                }

                var size = contentStream.CanSeek ? contentStream.Length : 0;
                _logger?.LogInformation("Archivo actualizado exitosamente en FTP: '{Path}'.", normalizedPath);

                return FtpFileResult.Succeeded(
                    remoteFullPath: normalizedPath,
                    fileName: fileName,
                    originalFileName: originalName,
                    remoteDirectory: directory,
                    sizeBytes: size,
                    fileId: fileId);

            }, ct);
        }

        public async Task<FtpFileResult> UpdateFileAsync(
            string remoteFilePath,
            byte[] contentBytes,
            CancellationToken ct = default)
        {
            if (contentBytes == null)
                throw new ArgumentNullException(nameof(contentBytes));

            using var ms = new MemoryStream(contentBytes, writable: false);
            return await UpdateFileAsync(remoteFilePath, ms, ct);
        }

        #endregion

        #region 4. Eliminación de Archivos y Directorios (Delete)

        public async Task<bool> DeleteFileAsync(
            string remoteFilePath,
            CancellationToken ct = default)
        {
            var normalizedPath = NormalizeFilePath(remoteFilePath);

            return await ExecuteWithRetryAsync(async client =>
            {
                var exists = await client.FileExists(normalizedPath, ct);
                if (!exists)
                {
                    _logger?.LogWarning("Intento de eliminar un archivo inexistente en FTP: '{Path}'.", normalizedPath);
                    return false;
                }

                await client.DeleteFile(normalizedPath, ct);
                _logger?.LogInformation("Archivo eliminado exitosamente de FTP: '{Path}'.", normalizedPath);
                return true;

            }, ct);
        }

        public async Task<bool> DeleteDirectoryAsync(
            string remoteDirectoryPath,
            bool recursive = true,
            CancellationToken ct = default)
        {
            var normalizedDir = NormalizeDirectoryPath(remoteDirectoryPath);

            return await ExecuteWithRetryAsync(async client =>
            {
                var exists = await client.DirectoryExists(normalizedDir, ct);
                if (!exists)
                {
                    _logger?.LogWarning("Intento de eliminar un directorio inexistente en FTP: '{Path}'.", normalizedDir);
                    return false;
                }

                if (recursive)
                {
                    await client.DeleteDirectory(normalizedDir, FtpListOption.AllFiles, ct);
                }
                else
                {
                    await client.DeleteDirectory(normalizedDir, ct);
                }

                _logger?.LogInformation("Directorio eliminado exitosamente de FTP: '{Path}' (Recursivo: {Recursive}).", normalizedDir, recursive);
                return true;

            }, ct);
        }

        #endregion

        #region 5. Consultas, Listado y Navegación (Query / Navigation)

        public async Task<bool> FileExistsAsync(
            string remoteFilePath,
            CancellationToken ct = default)
        {
            var normalizedPath = NormalizeFilePath(remoteFilePath);

            return await ExecuteWithRetryAsync(async client =>
            {
                return await client.FileExists(normalizedPath, ct);
            }, ct);
        }

        public async Task<bool> DirectoryExistsAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default)
        {
            var normalizedDir = NormalizeDirectoryPath(remoteDirectoryPath);

            return await ExecuteWithRetryAsync(async client =>
            {
                return await client.DirectoryExists(normalizedDir, ct);
            }, ct);
        }

        public async Task<IReadOnlyList<FtpItemInfo>> ListFilesAsync(
            string remoteDirectoryPath,
            bool recursive = false,
            CancellationToken ct = default)
        {
            var normalizedDir = NormalizeDirectoryPath(remoteDirectoryPath);

            return await ExecuteWithRetryAsync(async client =>
            {
                var exists = await client.DirectoryExists(normalizedDir, ct);
                if (!exists)
                {
                    return (IReadOnlyList<FtpItemInfo>)Array.Empty<FtpItemInfo>();
                }

                var options = recursive ? FtpListOption.Recursive : FtpListOption.Auto;
                var items = await client.GetListing(normalizedDir, options, ct);

                var resultList = new List<FtpItemInfo>();
                foreach (var item in items.Where(x => x.Type == FtpObjectType.File))
                {
                    resultList.Add(new FtpItemInfo
                    {
                        Name = item.Name,
                        FullPath = item.FullName.Replace('\\', '/'),
                        IsDirectory = false,
                        Size = item.Size,
                        ModifiedAtUtc = item.Modified,
                        Extension = Path.GetExtension(item.Name)
                    });
                }

                return (IReadOnlyList<FtpItemInfo>)resultList;

            }, ct);
        }

        public async Task<IReadOnlyList<FtpItemInfo>> ListDirectoriesAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default)
        {
            var normalizedDir = NormalizeDirectoryPath(remoteDirectoryPath);

            return await ExecuteWithRetryAsync(async client =>
            {
                var exists = await client.DirectoryExists(normalizedDir, ct);
                if (!exists)
                {
                    return (IReadOnlyList<FtpItemInfo>)Array.Empty<FtpItemInfo>();
                }

                var items = await client.GetListing(normalizedDir, FtpListOption.Auto, ct);

                var resultList = new List<FtpItemInfo>();
                foreach (var item in items.Where(x => x.Type == FtpObjectType.Directory))
                {
                    resultList.Add(new FtpItemInfo
                    {
                        Name = item.Name,
                        FullPath = item.FullName.Replace('\\', '/'),
                        IsDirectory = true,
                        Size = 0,
                        ModifiedAtUtc = item.Modified,
                        Extension = string.Empty
                    });
                }

                return (IReadOnlyList<FtpItemInfo>)resultList;

            }, ct);
        }

        public async Task<bool> CreateDirectoryRecursivelyAsync(
            string remoteDirectoryPath,
            CancellationToken ct = default)
        {
            var normalizedDir = NormalizeDirectoryPath(remoteDirectoryPath);

            return await ExecuteWithRetryAsync(async client =>
            {
                return await EnsureDirectoryExistsInternalAsync(client, normalizedDir, ct);
            }, ct);
        }

        #endregion

        #region 6. Procesos Masivos y Transaccionalidad (Batch / Concurrency / Rollback)

        public async Task<FtpBatchResult> UploadBatchAsync(
            IEnumerable<FtpUploadRequest> requests,
            bool transactional = false,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default)
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));

            var requestList = requests.ToList();
            var batchResult = new FtpBatchResult
            {
                TotalCount = requestList.Count
            };

            if (requestList.Count == 0)
            {
                return batchResult;
            }

            var stopwatch = Stopwatch.StartNew();
            var concurrency = Math.Clamp(maxDegreeOfParallelism, 1, _settings.MaxConnections);
            using var throttler = new SemaphoreSlim(concurrency, concurrency);

            var uploadedPathsForRollback = new ConcurrentBag<string>();
            var itemResults = new ConcurrentBag<FtpBatchItemResult>();
            var errors = new ConcurrentBag<string>();
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _logger?.LogInformation("Iniciando carga masiva de {Count} archivos en FTP (Transaccional: {Transactional}, Concurrencia: {Concurrency}).",
                requestList.Count, transactional, concurrency);

            var tasks = requestList.Select(async (req, index) =>
            {
                await throttler.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        itemResults.Add(new FtpBatchItemResult
                        {
                            Index = index,
                            FileName = req.FileName,
                            Success = false,
                            ErrorMessage = "Operación cancelada debido a fallo previo en lote transaccional."
                        });
                        return;
                    }

                    var uploadResult = await UploadFileAsync(req, cancellationTokenSource.Token);

                    if (uploadResult.Success)
                    {
                        uploadedPathsForRollback.Add(uploadResult.RemoteFullPath);

                        itemResults.Add(new FtpBatchItemResult
                        {
                            Index = index,
                            FileName = uploadResult.FileName,
                            RemoteFullPath = uploadResult.RemoteFullPath,
                            FileId = uploadResult.FileId,
                            Success = true,
                            SizeBytes = uploadResult.SizeBytes
                        });
                    }
                    else
                    {
                        var errorMsg = $"Fallo al cargar archivo [{index}] '{req.FileName}': {uploadResult.ErrorMessage}";
                        errors.Add(errorMsg);

                        itemResults.Add(new FtpBatchItemResult
                        {
                            Index = index,
                            FileName = req.FileName,
                            RemoteFullPath = uploadResult.RemoteFullPath,
                            Success = false,
                            ErrorMessage = uploadResult.ErrorMessage
                        });

                        if (transactional)
                        {
                            // Detener el resto de tareas de inmediato
                            cancellationTokenSource.Cancel();
                        }
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Excepción al cargar archivo [{index}] '{req.FileName}': {ex.Message}";
                    errors.Add(errorMsg);

                    itemResults.Add(new FtpBatchItemResult
                    {
                        Index = index,
                        FileName = req.FileName,
                        Success = false,
                        ErrorMessage = ex.Message
                    });

                    if (transactional)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
                finally
                {
                    throttler.Release();
                }
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (transactional)
            {
                _logger?.LogWarning("Cancelación detectada durante la carga por lote debido a un error transaccional.");
            }

            stopwatch.Stop();
            batchResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            batchResult.Items = itemResults.OrderBy(x => x.Index).ToList();
            batchResult.SuccessCount = batchResult.Items.Count(x => x.Success);
            batchResult.FailedCount = batchResult.TotalCount - batchResult.SuccessCount;
            batchResult.Errors = errors.ToList();

            // Ejecución de Rollback compensatorio si fue transaccional y hubo algún error
            if (transactional && batchResult.FailedCount > 0 && !uploadedPathsForRollback.IsEmpty)
            {
                _logger?.LogWarning("Ejecutando ROLLBACK de lote transaccional: Eliminando {Count} archivos ya subidos al FTP...", uploadedPathsForRollback.Count);

                var rollbackTasks = uploadedPathsForRollback.Select(async path =>
                {
                    try
                    {
                        await DeleteFileAsync(path, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error al revertir (eliminar) archivo '{Path}' durante Rollback.", path);
                    }
                });

                await Task.WhenAll(rollbackTasks);
                batchResult.IsRolledBack = true;
                _logger?.LogInformation("Rollback compensatorio completado con éxito.");
            }

            return batchResult;
        }

        public async Task<FtpBatchResult> DeleteBatchAsync(
            IEnumerable<string> remoteFilePaths,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default)
        {
            if (remoteFilePaths == null)
                throw new ArgumentNullException(nameof(remoteFilePaths));

            var pathList = remoteFilePaths.ToList();
            var batchResult = new FtpBatchResult
            {
                TotalCount = pathList.Count
            };

            if (pathList.Count == 0) return batchResult;

            var stopwatch = Stopwatch.StartNew();
            var concurrency = Math.Clamp(maxDegreeOfParallelism, 1, _settings.MaxConnections);
            using var throttler = new SemaphoreSlim(concurrency, concurrency);

            var itemResults = new ConcurrentBag<FtpBatchItemResult>();
            var errors = new ConcurrentBag<string>();

            var tasks = pathList.Select(async (path, index) =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    var deleted = await DeleteFileAsync(path, ct);
                    itemResults.Add(new FtpBatchItemResult
                    {
                        Index = index,
                        FileName = Path.GetFileName(path),
                        RemoteFullPath = path,
                        Success = deleted,
                        ErrorMessage = deleted ? null : "El archivo no existía o no pudo eliminarse."
                    });

                    if (!deleted)
                    {
                        errors.Add($"No se pudo eliminar el archivo '{path}'.");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error al eliminar '{path}': {ex.Message}");
                    itemResults.Add(new FtpBatchItemResult
                    {
                        Index = index,
                        FileName = Path.GetFileName(path),
                        RemoteFullPath = path,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            batchResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            batchResult.Items = itemResults.OrderBy(x => x.Index).ToList();
            batchResult.SuccessCount = batchResult.Items.Count(x => x.Success);
            batchResult.FailedCount = batchResult.TotalCount - batchResult.SuccessCount;
            batchResult.Errors = errors.ToList();

            return batchResult;
        }

        public async Task<IReadOnlyList<FtpFileResult>> DownloadBatchAsync(
            IEnumerable<string> remoteFilePaths,
            int maxDegreeOfParallelism = 5,
            CancellationToken ct = default)
        {
            if (remoteFilePaths == null)
                throw new ArgumentNullException(nameof(remoteFilePaths));

            var pathList = remoteFilePaths.ToList();
            if (pathList.Count == 0) return Array.Empty<FtpFileResult>();

            var concurrency = Math.Clamp(maxDegreeOfParallelism, 1, _settings.MaxConnections);
            using var throttler = new SemaphoreSlim(concurrency, concurrency);
            var results = new ConcurrentBag<(int index, FtpFileResult result)>();

            var tasks = pathList.Select(async (path, index) =>
            {
                await throttler.WaitAsync(ct);
                try
                {
                    var downloadResult = await DownloadFileAsync(path, ct);
                    results.Add((index, downloadResult));
                }
                catch (Exception ex)
                {
                    results.Add((index, FtpFileResult.Failed(ex.Message, path)));
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.OrderBy(x => x.index).Select(x => x.result).ToList();
        }

        #endregion

        #region 7. Connection Pool & Resilient Client Management

        /// <summary>
        /// Ejecuta una operación contra un cliente FTP obtenido del pool con política de reintentos y backoff.
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<AsyncFtpClient, Task<T>> action, CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var attempt = 0;
            var maxAttempts = Math.Max(1, _settings.RetryCount);

            while (true)
            {
                attempt++;
                AsyncFtpClient? client = null;

                try
                {
                    client = await RentClientAsync(ct);
                    return await action(client);
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < maxAttempts && !ct.IsCancellationRequested)
                {
                    _logger?.LogWarning(ex, "Fallo transitorio en operación FTP (Intento {Attempt}/{Max}). Reintentando...", attempt, maxAttempts);

                    if (client != null)
                    {
                        await DiscardClientAsync(client);
                        client = null;
                    }

                    var delay = _settings.RetryDelayMilliseconds * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delay, ct);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error no recuperable en operación FTP tras {Attempt} intentos: {Message}", attempt, ex.Message);
                    throw;
                }
                finally
                {
                    if (client != null)
                    {
                        ReturnClient(client);
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene un cliente FTP activo y autenticado desde el pool de conexiones o crea uno nuevo.
        /// </summary>
        private async Task<AsyncFtpClient> RentClientAsync(CancellationToken ct)
        {
            await _poolSemaphore.WaitAsync(ct);

            while (_clientPool.TryTake(out var pooledClient))
            {
                if (pooledClient.IsConnected && pooledClient.IsAuthenticated)
                {
                    return pooledClient;
                }

                // El cliente perdió la conexión; intentar reconectar o descartar
                try
                {
                    await pooledClient.AutoConnect(ct);
                    if (pooledClient.IsConnected && pooledClient.IsAuthenticated)
                    {
                        return pooledClient;
                    }
                }
                catch
                {
                    // Descartar cliente defectuoso y continuar
                    pooledClient.Dispose();
                }
            }

            // Crear una nueva instancia de cliente
            var newClient = CreateConfiguredFtpClient();
            try
            {
                await newClient.AutoConnect(ct);
                return newClient;
            }
            catch (Exception ex)
            {
                newClient.Dispose();
                _poolSemaphore.Release();
                _logger?.LogError(ex, "No fue posible conectar al servidor FTP en '{Host}:{Port}'.", _settings.Host, _settings.Port);
                throw new InvalidOperationException($"Error al establecer conexión con el servidor FTP '{_settings.Host}:{_settings.Port}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Devuelve un cliente al pool de conexiones disponibles.
        /// </summary>
        private void ReturnClient(AsyncFtpClient client)
        {
            if (_isDisposed)
            {
                client.Dispose();
                return;
            }

            if (client.IsConnected && client.IsAuthenticated)
            {
                _clientPool.Add(client);
            }
            else
            {
                client.Dispose();
            }

            _poolSemaphore.Release();
        }

        /// <summary>
        /// Descarta y cierra un cliente dañado del pool.
        /// </summary>
        private async Task DiscardClientAsync(AsyncFtpClient client)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.Disconnect();
                }
            }
            catch
            {
                // Ignorar error al desconectar cliente dañado
            }
            finally
            {
                client.Dispose();
                _poolSemaphore.Release();
            }
        }

        /// <summary>
        /// Crea e inicializa una instancia de AsyncFtpClient con todos los parámetros configurados.
        /// </summary>
        private AsyncFtpClient CreateConfiguredFtpClient()
        {
            var effectivePassword = GetEffectivePassword();
            var client = new AsyncFtpClient(_settings.Host, _settings.Username, effectivePassword, _settings.Port);

            // Cifrado y TLS
            client.Config.EncryptionMode = _settings.EncryptionMode switch
            {
                FtpEncryptionMode.Explicit => FluentFTP.FtpEncryptionMode.Explicit,
                FtpEncryptionMode.Implicit => FluentFTP.FtpEncryptionMode.Implicit,
                _ => FluentFTP.FtpEncryptionMode.None
            };

            // Modo de datos
            client.Config.DataConnectionType = _settings.DataConnectionType switch
            {
                FtpDataConnectionType.PASV => FluentFTP.FtpDataConnectionType.PASV,
                FtpDataConnectionType.EPSV => FluentFTP.FtpDataConnectionType.EPSV,
                FtpDataConnectionType.PORT => FluentFTP.FtpDataConnectionType.PORT,
                _ => FluentFTP.FtpDataConnectionType.AutoPassive
            };

            // Timeouts y buffers
            client.Config.ConnectTimeout = _settings.ConnectTimeoutSeconds * 1000;
            client.Config.ReadTimeout = _settings.ReadTimeoutSeconds * 1000;
            client.Config.DataConnectionConnectTimeout = _settings.ConnectTimeoutSeconds * 1000;
            client.Config.DataConnectionReadTimeout = _settings.ReadTimeoutSeconds * 1000;
            client.Config.SocketKeepAlive = _settings.KeepAlive;
            client.Config.TransferChunkSize = _settings.BufferSize;
            client.Config.RetryAttempts = _settings.RetryCount;

            // Acepta certificados autofirmados si se configuran redes internas
            client.ValidateCertificate += (control, e) => { e.Accept = true; };

            return client;
        }

        /// <summary>
        /// Obtiene la contraseña efectiva, descifrándola mediante ICryptography si fue configurada como cifrada.
        /// </summary>
        private string GetEffectivePassword()
        {
            if (_settings.IsPasswordEncrypted && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                try
                {
                    var crypto = _serviceProvider.GetService<ICryptography>();
                    if (crypto != null)
                    {
                        return crypto.Desencriptar(_settings.Password);
                    }
                    _logger?.LogWarning("IsPasswordEncrypted está habilitado pero ICryptography no está registrado en el contenedor DI.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error al desencriptar la contraseña de FTP configurada.");
                    throw new InvalidOperationException("No se pudo desencriptar la contraseña de FTP.", ex);
                }
            }

            return _settings.Password;
        }

        private static bool IsTransientException(Exception ex)
        {
            return ex is SocketException
                || ex is TimeoutException
                || ex is IOException
                || ex is FtpException;
        }

        #endregion

        #region 8. Path Engine, Navigation & GUID Helpers

        /// <summary>
        /// Asegura que todos los segmentos de una ruta de directorios existan recursivamente en el servidor FTP.
        /// Cada elemento separado por '/' es verificado y creado si no existe.
        /// </summary>
        private static async Task<bool> EnsureDirectoryExistsInternalAsync(
            AsyncFtpClient client,
            string remoteDirectoryPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(remoteDirectoryPath) || remoteDirectoryPath == "/")
            {
                return true;
            }

            var segments = remoteDirectoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;

            foreach (var segment in segments)
            {
                currentPath = $"{currentPath}/{segment}";

                var exists = await client.DirectoryExists(currentPath, ct);
                if (!exists)
                {
                    var created = await client.CreateDirectory(currentPath, ct);
                    if (!created && !await client.DirectoryExists(currentPath, ct))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Normaliza una ruta de directorio asegurando separadores '/' y eliminando posibles ataques traversal.
        /// </summary>
        private string NormalizeDirectoryPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return NormalizeRoot(_settings.RemoteRootPath);
            }

            ValidateNoTraversal(path);

            var cleaned = path.Replace('\\', '/').Trim();
            if (!cleaned.StartsWith('/'))
            {
                cleaned = CombinePaths(_settings.RemoteRootPath, cleaned);
            }

            return cleaned.TrimEnd('/');
        }

        /// <summary>
        /// Normaliza una ruta completa hacia un archivo.
        /// </summary>
        private string NormalizeFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("La ruta del archivo no puede ser nula ni vacía.", nameof(path));

            ValidateNoTraversal(path);

            var cleaned = path.Replace('\\', '/').Trim();
            if (!cleaned.StartsWith('/'))
            {
                cleaned = CombinePaths(_settings.RemoteRootPath, cleaned);
            }

            return cleaned;
        }

        private static string NormalizeRoot(string? rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return "/";
            var cleaned = rootPath.Replace('\\', '/').Trim();
            if (!cleaned.StartsWith('/')) cleaned = "/" + cleaned;
            return cleaned.TrimEnd('/');
        }

        private static string CombinePaths(string part1, string part2)
        {
            var p1 = part1.Replace('\\', '/').TrimEnd('/');
            var p2 = part2.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(p1)) return "/" + p2;
            return $"{p1}/{p2}";
        }

        private static void ValidateNoTraversal(string path)
        {
            if (path.Contains(".."))
            {
                throw new ArgumentException($"La ruta '{path}' contiene secuencias inválidas de directorio (..).", nameof(path));
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var ch in invalidChars)
            {
                name = name.Replace(ch, '_');
            }
            return name.Trim();
        }

        /// <summary>
        /// Analiza el nombre de un archivo remoto para extraer el GUID atómico y el nombre original limpio.
        /// </summary>
        private static (Guid? FileId, string OriginalName, string FullName) ParseFileNameAndGuid(string rawFileName)
        {
            if (string.IsNullOrWhiteSpace(rawFileName))
            {
                return (null, string.Empty, string.Empty);
            }

            var match = GuidPrefixRegex.Match(rawFileName);
            if (match.Success)
            {
                var guidPart = match.Groups[1].Value;
                var originalPart = match.Groups[2].Value;

                if (Guid.TryParse(guidPart, out var parsedGuid))
                {
                    return (parsedGuid, originalPart, rawFileName);
                }
            }

            return (null, rawFileName, rawFileName);
        }

        #endregion

        #region 9. IDisposable & IAsyncDisposable

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            while (_clientPool.TryTake(out var client))
            {
                try
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect().GetAwaiter().GetResult();
                    }
                }
                catch
                {
                    // Ignorar durante Dispose
                }
                finally
                {
                    client.Dispose();
                }
            }

            _poolSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            while (_clientPool.TryTake(out var client))
            {
                try
                {
                    if (client.IsConnected)
                    {
                        await client.Disconnect();
                    }
                }
                catch
                {
                    // Ignorar durante DisposeAsync
                }
                finally
                {
                    client.Dispose();
                }
            }

            _poolSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
