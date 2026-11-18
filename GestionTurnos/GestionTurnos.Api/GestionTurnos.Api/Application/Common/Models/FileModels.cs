namespace GestionTurnos.Api.Application.Common.Models;

/// <summary>
/// Representa un archivo en memoria o stream independiente de dependencias web (IFormFile).
/// </summary>
public record FileAttachment(
    Stream ContentStream,
    string FileName,
    string ContentType,
    long Length) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        ContentStream?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (ContentStream != null)
        {
            await ContentStream.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Metadatos devueltos tras almacenar un archivo físicamente en el servidor FTP.
/// </summary>
public record StoredFileResult(
    string RemotePath,
    string PhysicalFileName,
    string OriginalFileName,
    long SizeBytes,
    Guid? FileId);

/// <summary>
/// Objeto devuelto por queries de descarga para streaming seguro directo al cliente.
/// </summary>
public record DownloadedFile(
    Stream ContentStream,
    string FileName,
    string ContentType,
    long SizeBytes) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        ContentStream?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (ContentStream != null)
        {
            await ContentStream.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}

