using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Seguridad;

/// <summary>
/// Foto de perfil del usuario (seguridad.usuario_foto_perfil). La imagen se almacena en Cloudinary;
/// aquí solo se guarda su URL y su identificador. Tabla aparte para no alterar seguridad.usuario.
/// </summary>
public class UsuarioFotoPerfil : BaseEntity
{
    public long IdUsuario { get; set; }
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
