namespace GestionTurnos.Api.Presentation.Contracts.Requests.Seguridad;

/// <param name="Recordar">"Mantener la sesión iniciada en este dispositivo" (sesión renovable de larga duración).</param>
public sealed record LoginRequest(
    string Correo,
    string Contrasena,
    bool Recordar = false);

/// <summary>Refresh token emitido en el inicio de sesión.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record SolicitarRegistroRequest(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string Correo,
    string? Telefono,
    string Contrasena);

public sealed record AprobarSolicitudRegistroRequest(
    int IdArea,
    int IdCargo,
    DateOnly FechaIngreso,
    string? CodTipoContrato);

public sealed record RechazarSolicitudRegistroRequest(
    string MotivoRechazo);

public sealed record CrearUsuarioRequest(
    string Correo,
    string Contrasena,
    int IdRol);

public sealed record ActualizarUsuarioRequest(
    string Correo,
    int IdRol,
    string CodEstadoUsuario);

public sealed record GuardarRolRequest(
    string Nombre,
    string? Descripcion,
    bool Activo,
    IReadOnlyList<string> CodigosPermisos);


public sealed record CambiarEstadoUsuarioRequest(
    string CodEstadoUsuario);

public sealed record CambiarRolUsuarioRequest(
    int IdRol);

public sealed record RestablecerContrasenaRequest(
    string NuevaContrasena);
