using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Integrations.Imagenes;

/// <summary>
/// Almacén de imágenes sobre Cloudinary. Las fotos de perfil se recortan en el servidor de Cloudinary
/// a un cuadrado centrado en el rostro, así el cliente siempre recibe un tamaño uniforme.
/// </summary>
public sealed class CloudinaryAlmacenImagenes : IAlmacenImagenes
{
    private static readonly string[] FormatosPermitidos = ["jpg", "jpeg", "png", "webp"];

    private readonly Cloudinary _cloudinary;
    private readonly CloudinarySettings _settings;
    private readonly ILogger<CloudinaryAlmacenImagenes> _logger;

    public CloudinaryAlmacenImagenes(IOptions<CloudinarySettings> settings, ILogger<CloudinaryAlmacenImagenes> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _cloudinary = new Cloudinary(new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret))
        {
            Api = { Secure = true }
        };
    }

    public async Task<ImagenAlmacenada> SubirFotoPerfilAsync(
        long idUsuario,
        Stream contenido,
        string nombreArchivo,
        CancellationToken cancellationToken)
    {
        var parametros = new ImageUploadParams
        {
            File = new FileDescription(nombreArchivo, contenido),
            Folder = _settings.CarpetaFotosPerfil,
            PublicId = $"usuario-{idUsuario}",
            Overwrite = true,
            Invalidate = true,
            UniqueFilename = false,
            UseFilename = false,
            AllowedFormats = FormatosPermitidos,
            Transformation = new Transformation()
                .Width(_settings.TamanoFotoPerfil)
                .Height(_settings.TamanoFotoPerfil)
                .Crop("fill")
                .Gravity("face")
                .Quality("auto")
        };

        var resultado = await _cloudinary.UploadAsync(parametros, cancellationToken);

        if (resultado.Error is not null || resultado.SecureUrl is null)
        {
            throw new AlmacenImagenesException(resultado.Error?.Message ?? "Cloudinary no devolvió la URL de la imagen.");
        }

        return new ImagenAlmacenada(resultado.SecureUrl.ToString(), resultado.PublicId);
    }

    public async Task EliminarAsync(string publicId, CancellationToken cancellationToken)
    {
        // Nunca lanza: una imagen que no se pudo borrar solo queda sin referencia en Cloudinary.
        try
        {
            var resultado = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image,
                Invalidate = true
            });

            // "not found" no es un error: la imagen ya no existe.
            if (resultado.Error is not null)
            {
                _logger.LogWarning("No se pudo eliminar la imagen {PublicId} de Cloudinary: {Error}", publicId, resultado.Error.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la imagen {PublicId} de Cloudinary", publicId);
        }
    }
}

/// <summary>Fallo al comunicarse con el proveedor de imágenes.</summary>
public sealed class AlmacenImagenesException(string mensaje) : Exception(mensaje);
