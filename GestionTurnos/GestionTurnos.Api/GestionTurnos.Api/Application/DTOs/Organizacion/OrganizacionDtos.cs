namespace GestionTurnos.Api.Application.DTOs.Organizacion;

public sealed record SucursalDto(
    int IdSucursal,
    string Nombre,
    string? Direccion,
    string? Ciudad,
    bool Activo);

public sealed record AreaDto(
    int IdArea,
    int IdSucursal,
    string NombreSucursal,
    string Nombre,
    bool Activo);

public sealed record CargoDto(
    int IdCargo,
    string Nombre,
    bool Activo);

public sealed record EmpleadoDto(
    long IdEmpleado,
    long? IdUsuario,
    string? CorreoUsuario,
    string CodTipoDocumento,
    string NombreTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    int IdArea,
    string NombreArea,
    int IdSucursal,
    string NombreSucursal,
    int IdCargo,
    string NombreCargo,
    string? CodTipoContrato,
    string? NombreTipoContrato,
    DateOnly FechaIngreso,
    DateOnly? FechaRetiro,
    string CodEstadoEmpleado,
    string NombreEstadoEmpleado,
    DateTimeOffset CreadoEn);

/// <summary>
/// Perfil del empleado en sesión (Sección 3.4 - solo lectura para los datos fijos).
/// </summary>
public sealed record PerfilEmpleadoDto(
    long IdEmpleado,
    string CodTipoDocumento,
    string NumeroDocumento,
    string Nombres,
    string Apellidos,
    string? CorreoContacto,
    string? Telefono,
    string? Direccion,
    DateOnly FechaIngreso,
    string Cargo,
    string Area,
    string Sucursal,
    string? TipoContrato,
    string Estado);

