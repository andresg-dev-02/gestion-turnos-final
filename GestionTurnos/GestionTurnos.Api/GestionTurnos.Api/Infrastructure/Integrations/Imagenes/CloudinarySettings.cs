namespace GestionTurnos.Api.Infrastructure.Integrations.Imagenes;

/// <summary>Credenciales y opciones de Cloudinary (sección "Cloudinary" de la configuración).</summary>
public sealed class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Carpeta de Cloudinary donde se guardan las fotos de perfil.</summary>
    public string CarpetaFotosPerfil { get; set; } = "gestion-turnos/perfiles";

    /// <summary>Lado (en píxeles) del recorte cuadrado que se guarda.</summary>
    public int TamanoFotoPerfil { get; set; } = 512;
}
