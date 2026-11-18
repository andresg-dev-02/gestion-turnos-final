using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Errors.Seguridad;

/// <summary>Errores del módulo de foto de perfil.</summary>
public static class FotoPerfilErrors
{
    public static readonly Error SinFoto = new(
        "FotoPerfil.SinFoto",
        "No tiene una foto de perfil registrada.",
        ErrorType.NotFound);

    public static readonly Error ErrorAlmacenamiento = new(
        "FotoPerfil.ErrorAlmacenamiento",
        "No fue posible guardar la imagen. Verifique que sea una imagen JPG, PNG o WEBP válida e inténtelo de nuevo.",
        ErrorType.Failure);
}
