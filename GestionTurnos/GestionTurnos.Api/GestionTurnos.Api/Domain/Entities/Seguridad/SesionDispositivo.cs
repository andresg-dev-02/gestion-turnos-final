using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Seguridad;

/// <summary>
/// Sesión renovable de un dispositivo (seguridad.sesion_dispositivo). Guarda solo el hash SHA-256 del
/// refresh token. Cada renovación revoca la fila usada y crea otra de la misma familia; si una fila ya
/// rotada se vuelve a presentar (token robado), se revoca toda la familia.
/// </summary>
public class SesionDispositivo : BaseEntity
{
    public long IdSesion { get; set; }
    public long IdUsuario { get; set; }
    public string HashToken { get; set; } = string.Empty;
    public Guid Familia { get; set; }

    /// <summary>"Mantener la sesión iniciada": vigencia deslizante larga en lugar de la de una jornada.</summary>
    public bool Recordar { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadaEn { get; set; }
    public string? MotivoRevocacion { get; set; }
    public string? UserAgent { get; set; }
    public string? IpOrigen { get; set; }

    public Usuario Usuario { get; set; } = null!;
}

public static class MotivosRevocacionSesion
{
    public const string Rotada = "ROTADA";
    public const string CierreSesion = "CIERRE_SESION";
    public const string Reutilizada = "REUTILIZADA";
    public const string CambioContrasena = "CAMBIO_CONTRASENA";
    public const string UsuarioNoActivo = "USUARIO_NO_ACTIVO";
}
