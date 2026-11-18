using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Errors.Organizacion;

/// <summary>
/// Errores de dominio del módulo de Organización y Empleados (FLUJO_BD_ADMIN secciones 6 y 7).
/// </summary>
public static class OrganizacionErrors
{
    public static readonly Error SucursalConAreasActivas = new(
        "Organizacion.SucursalConAreasActivas",
        "No se puede desactivar la sucursal porque tiene áreas activas. Desactive o traslade primero sus áreas.",
        ErrorType.Conflict);

    public static readonly Error AreaConEmpleadosActivos = new(
        "Organizacion.AreaConEmpleadosActivos",
        "No se puede desactivar el área porque tiene empleados activos. Trasládelos o desactívelos primero.",
        ErrorType.Conflict);

    public static readonly Error CargoConEmpleadosActivos = new(
        "Organizacion.CargoConEmpleadosActivos",
        "No se puede desactivar el cargo porque tiene empleados activos. Cámbielos de cargo o desactívelos primero.",
        ErrorType.Conflict);

    public static readonly Error EmpleadoNoEncontrado = new(
        "Organizacion.EmpleadoNoEncontrado",
        "El empleado no fue encontrado.",
        ErrorType.NotFound);

    public static readonly Error EmpleadoYaInactivo = new(
        "Organizacion.EmpleadoYaInactivo",
        "El empleado ya se encuentra inactivo.",
        ErrorType.Conflict);

    public static readonly Error EmpleadoYaActivo = new(
        "Organizacion.EmpleadoYaActivo",
        "El empleado ya se encuentra activo.",
        ErrorType.Conflict);

    public static readonly Error TipoDocumentoInvalido = new(
        "Parametrizacion.TipoDocumentoInvalido",
        "El tipo de documento seleccionado no existe o fue desactivado.",
        ErrorType.Failure);

    public static readonly Error TipoContratoInvalido = new(
        "Parametrizacion.TipoContratoInvalido",
        "El tipo de contrato seleccionado no existe o fue desactivado.",
        ErrorType.Failure);

    public static readonly Error UsuarioYaVinculado = new(
        "Organizacion.UsuarioYaVinculado",
        "El usuario seleccionado ya está vinculado a otro empleado.",
        ErrorType.Conflict);

    public static readonly Error AccesoIncompleto = new(
        "Organizacion.AccesoIncompleto",
        "Para crear el usuario de acceso indique el correo y la contraseña temporal.",
        ErrorType.Failure);
}
