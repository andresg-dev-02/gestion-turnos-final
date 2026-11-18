namespace GestionTurnos.Api.Presentation.Contracts.Requests.Organizacion;

public sealed record GuardarSucursalRequest(
    string Nombre,
    string? Direccion,
    string? Ciudad,
    bool Activo = true);

public sealed record GuardarAreaRequest(
    int IdSucursal,
    string Nombre,
    bool Activo = true);

public sealed record GuardarCargoRequest(
    string Nombre,
    bool Activo = true);

public sealed record CrearEmpleadoRequest(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso,
    long? IdUsuarioExistente = null,
    string? CorreoAccesoNuevoUsuario = null,
    string? ContrasenaNuevoUsuario = null);

public sealed record ActualizarEmpleadoRequest(
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    int IdCargo,
    string? CodTipoContrato,
    DateOnly FechaIngreso);

public sealed record DesactivarEmpleadoRequest(
    DateOnly FechaRetiro);

/// <summary>
/// Lista blanca estricta para edición del propio perfil (Sección 3.5).
/// </summary>
public sealed record EditarMiPerfilRequest(
    string? CorreoContacto,
    string? Telefono,
    string? Direccion);

public sealed record CambiarMiContrasenaRequest(
    string ContrasenaActual,
    string NuevaContrasena);

