namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>Imagen ya almacenada en el proveedor externo.</summary>
public sealed record ImagenAlmacenada(string Url, string PublicId);

/// <summary>
/// Almacenamiento externo de imágenes (implementado con Cloudinary).
/// </summary>
public interface IAlmacenImagenes
{
    /// <summary>
    /// Sube (o reemplaza) la foto de perfil de un usuario. El identificador es fijo por usuario,
    /// así cada usuario ocupa una sola imagen en el proveedor.
    /// </summary>
    Task<ImagenAlmacenada> SubirFotoPerfilAsync(long idUsuario, Stream contenido, string nombreArchivo, CancellationToken cancellationToken);

    /// <summary>Elimina una imagen del proveedor. No falla si ya no existe.</summary>
    Task EliminarAsync(string publicId, CancellationToken cancellationToken);
}
