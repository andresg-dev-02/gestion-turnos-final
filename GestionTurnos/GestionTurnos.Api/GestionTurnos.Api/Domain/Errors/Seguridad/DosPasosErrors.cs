using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Errors.Seguridad;

/// <summary>
/// Errores de la autenticación en dos pasos por correo.
/// </summary>
public static class DosPasosErrors
{
    /// <summary>
    /// Respuesta del login cuando la contraseña es correcta pero falta el segundo factor.
    /// El cliente lo identifica por <c>code</c> en el ProblemDetails y pasa a la pantalla del código.
    /// </summary>
    public const string CodigoSegundoFactorRequerido = "Auth.SegundoFactorRequerido";

    public static Error SegundoFactorRequerido(string correoEnmascarado, int minutos) => new(
        CodigoSegundoFactorRequerido,
        $"Enviamos un código de verificación a {correoEnmascarado}. Ingréselo para completar el inicio de sesión (vigencia: {minutos} minutos).",
        ErrorType.Unauthorized);

    public static Error CodigoInvalido(int intentosRestantes) => new(
        "Auth.CodigoVerificacionInvalido",
        $"El código de verificación no es correcto. Intentos restantes: {intentosRestantes}.",
        ErrorType.Unauthorized);

    public static readonly Error CodigoExpirado = new(
        "Auth.CodigoVerificacionExpirado",
        "El código de verificación expiró o ya fue usado. Solicite uno nuevo.",
        ErrorType.Unauthorized);

    public static readonly Error IntentosAgotados = new(
        "Auth.CodigoVerificacionIntentosAgotados",
        "Superó el número de intentos permitidos. Solicite un código nuevo.",
        ErrorType.Unauthorized);

    public static readonly Error NoDisponible = new(
        "Seguridad.DosPasosNoDisponible",
        "La autenticación en dos pasos no está habilitada en el sistema.",
        ErrorType.Failure);

    public static readonly Error YaHabilitado = new(
        "Seguridad.DosPasosYaHabilitado",
        "La autenticación en dos pasos ya está activa para su cuenta.",
        ErrorType.Conflict);

    public static readonly Error NoHabilitado = new(
        "Seguridad.DosPasosNoHabilitado",
        "La autenticación en dos pasos no está activa para esta cuenta.",
        ErrorType.Conflict);

    public static readonly Error ContrasenaIncorrecta = new(
        "Seguridad.ContrasenaIncorrecta",
        "La contraseña actual no es correcta.",
        ErrorType.Unauthorized);

    public static readonly Error CorreoNoEnviado = new(
        "Seguridad.CorreoNoEnviado",
        "No fue posible enviar el código de verificación en este momento. Intente de nuevo en unos minutos.",
        ErrorType.Failure);
}
