namespace GestionTurnos.Api.Application.DTOs.Seguridad;

/// <summary>
/// Respuesta de inicio de sesión (Sección 3.3). Nunca incluye hash_contrasena.
/// </summary>
public sealed record LoginResponseDto(
    string AccessToken,
    DateTimeOffset ExpiraEn,
    long IdUsuario,
    string Correo,
    string Rol,
    long? IdEmpleado,
    string? NombreCompleto,
    IReadOnlyList<string> Permisos,
    string? RefreshToken = null,
    DateTimeOffset? RefreshExpiraEn = null);

/// <summary>
/// DTO de solicitud de registro (Secciones 3.1 y 3.2). Nunca incluye hash_contrasena (Sección 3.1 punto 4 y Sección 9).
/// </summary>
public sealed record SolicitudRegistroDto(
    long IdSolicitudRegistro,
    string CodTipoDocumento,
    string NombreTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string Correo,
    string? Telefono,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    DateTimeOffset SolicitadoEn,
    long? RevisadoPor,
    DateTimeOffset? RevisadoEn,
    string? MotivoRechazo,
    long? IdUsuarioCreado);

public sealed record AprobacionRegistroResultDto(
    long IdSolicitudRegistro,
    long IdUsuarioCreado,
    long IdEmpleadoCreado);

/// <summary>
/// DTO de usuario (Sección 5 - RF-01). Nunca incluye hash_contrasena.
/// </summary>
public sealed record UsuarioDto(
    long IdUsuario,
    string Correo,
    int IdRol,
    string NombreRol,
    string CodEstadoUsuario,
    string NombreEstadoUsuario,
    DateTimeOffset? UltimoAcceso,
    DateTimeOffset CreadoEn,
    long? IdEmpleado,
    string? NombreEmpleado);

public sealed record RolDto(
    int IdRol,
    string Nombre,
    string? Descripcion,
    bool Activo,
    IReadOnlyList<string> Permisos);

public sealed record PermisoDto(
    int IdPermiso,
    string Codigo,
    string Descripcion);

