using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Errors.Seguridad;

/// <summary>
/// Errores de dominio para el módulo de Seguridad, Autenticación, Registro y Autorización.
/// </summary>
public static class AuthErrors
{
    public static readonly Error SesionInvalida = new(
        "Auth.SesionInvalida", "Tu sesión expiró o fue cerrada. Ingresa nuevamente.", ErrorType.Unauthorized);

    public static readonly Error Unauthorized = new(
        "Auth.Unauthorized", "El usuario no está autenticado.", ErrorType.Unauthorized);

    public static readonly Error Forbidden = new(
        "Auth.Forbidden", "No tiene permisos para realizar esta acción.", ErrorType.Forbidden);

    public static readonly Error CredencialesInvalidas = new(
        "Auth.CredencialesInvalidas", "El correo electrónico o la contraseña son incorrectos.", ErrorType.Unauthorized);

    public static readonly Error RegistroPendiente = new(
        "Auth.RegistroPendiente", "Tu registro está pendiente de aprobación.", ErrorType.Forbidden);

    public static Error RegistroRechazado(string? motivo) => new(
        "Auth.RegistroRechazado",
        string.IsNullOrWhiteSpace(motivo)
            ? "Tu solicitud de registro fue rechazada."
            : $"Tu solicitud de registro fue rechazada. Motivo: {motivo}",
        ErrorType.Forbidden);

    public static Error UsuarioNoActivo(string estado) => new(
        "Auth.UsuarioNoActivo",
        $"La cuenta de usuario no se encuentra activa (estado actual: {estado}).",
        ErrorType.Forbidden);

    public static readonly Error EmpleadoNoActivo = new(
        "Auth.EmpleadoNoActivo",
        "No se encontró un perfil de empleado activo asociado al usuario de la sesión.",
        ErrorType.Forbidden);

    public static readonly Error CorreoYaRegistrado = new(
        "Seguridad.CorreoYaRegistrado",
        "Ya existe un usuario registrado con ese correo electrónico.",
        ErrorType.Conflict);

    public static readonly Error DocumentoYaRegistrado = new(
        "Seguridad.DocumentoYaRegistrado",
        "Ya existe un empleado registrado con ese número de documento.",
        ErrorType.Conflict);

    public static readonly Error SolicitudPendienteExistente = new(
        "Seguridad.SolicitudPendienteExistente",
        "Ya existe una solicitud de registro pendiente con ese correo electrónico.",
        ErrorType.Conflict);

    public static readonly Error SolicitudNoEncontradaOPendiente = new(
        "Seguridad.SolicitudNoEncontradaOPendiente",
        "La solicitud de registro no existe o ya fue procesada.",
        ErrorType.NotFound);

    public static readonly Error RolEmpleadoNoConfigurado = new(
        "Seguridad.RolEmpleadoNoConfigurado",
        "No se encontró el rol 'Empleado' activo en la base de datos.",
        ErrorType.Failure);

    public static readonly Error UsuarioNoEncontrado = new(
        "Seguridad.UsuarioNoEncontrado",
        "El usuario solicitado no fue encontrado.",
        ErrorType.NotFound);

    public static readonly Error RolNoEncontrado = new(
        "Seguridad.RolNoEncontrado",
        "El rol seleccionado no existe o está inactivo.",
        ErrorType.NotFound);

    public static readonly Error NoPuedeDesactivarseASiMismo = new(
        "Seguridad.NoPuedeDesactivarseASiMismo",
        "No puede desactivar ni bloquear su propio usuario.",
        ErrorType.Conflict);

    public static readonly Error NoPuedeCambiarsePropioRol = new(
        "Seguridad.NoPuedeCambiarsePropioRol",
        "No puede cambiar el rol de su propio usuario.",
        ErrorType.Conflict);

    public static readonly Error UltimoAdministrador = new(
        "Seguridad.UltimoAdministrador",
        "La operación dejaría el sistema sin ningún usuario activo con permiso para gestionar usuarios.",
        ErrorType.Conflict);

    public static readonly Error PermisoNoEncontrado = new(
        "Seguridad.PermisoNoEncontrado",
        "El permiso seleccionado no existe.",
        ErrorType.NotFound);

    public static readonly Error PermisoYaAsignado = new(
        "Seguridad.PermisoYaAsignado",
        "El rol ya tiene asignado ese permiso.",
        ErrorType.Conflict);

    public static readonly Error PermisoNoAsignado = new(
        "Seguridad.PermisoNoAsignado",
        "El rol no tiene asignado ese permiso.",
        ErrorType.NotFound);

    public static readonly Error RolNombreDuplicado = new(
        "Seguridad.RolNombreDuplicado",
        "Ya existe un rol con ese nombre.",
        ErrorType.Conflict);

    public static readonly Error AreaNoEncontrada = new(
        "Organizacion.AreaNoEncontrada",
        "El área seleccionada no existe o fue desactivada.",
        ErrorType.NotFound);

    public static readonly Error CargoNoEncontrado = new(
        "Organizacion.CargoNoEncontrado",
        "El cargo seleccionado no existe o fue desactivado.",
        ErrorType.NotFound);
}
