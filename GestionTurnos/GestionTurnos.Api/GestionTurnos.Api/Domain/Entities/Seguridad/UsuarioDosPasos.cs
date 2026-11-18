using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Seguridad;

/// <summary>
/// Preferencia de autenticación en dos pasos del usuario (seguridad.usuario_dos_pasos).
/// Tabla aparte para no alterar seguridad.usuario. Nunca se borra: se desactiva (RG-06).
/// </summary>
public class UsuarioDosPasos : BaseEntity
{
    public long IdUsuario { get; set; }
    public bool Habilitado { get; set; }
    public DateTimeOffset? HabilitadoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Usuario que hizo el último cambio (el propio usuario o un administrador que la restableció).</summary>
    public long? ActualizadoPor { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
