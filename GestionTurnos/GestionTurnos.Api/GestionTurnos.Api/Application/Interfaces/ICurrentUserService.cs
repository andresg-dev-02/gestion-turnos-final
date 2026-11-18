namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Proporciona acceso al contexto del usuario actualmente autenticado en la solicitud HTTP.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// ID del usuario autenticado (id_usuario en seguridad.usuario).
    /// </summary>
    long? UserId { get; }

    /// <summary>
    /// Correo electrónico del usuario autenticado.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Nombre completo del usuario autenticado (si aplica).
    /// </summary>
    string? Nombre { get; }

    /// <summary>
    /// Nombre del rol del usuario autenticado (seguridad.rol.nombre).
    /// </summary>
    string? RolCodigo { get; }

    /// <summary>
    /// Dirección IP del cliente que realiza la solicitud HTTP (usada en app.ip para auditoría).
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Indica si la solicitud actual pertenece a un usuario autenticado.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Lista de códigos de permisos activos asignados al rol del usuario (seguridad.permiso.codigo).
    /// </summary>
    IReadOnlyList<string> Permisos { get; }

    /// <summary>
    /// Determina si el usuario autenticado cuenta con un permiso específico.
    /// </summary>
    bool TienePermiso(string permisoCodigo);
}
