namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// DTO con el token dinámico de la estación de marcaje y su temporización.
/// </summary>
public sealed record TokenQrEstacionDto(
    string Token,
    string CodigoEstacion,
    string NombreEstacion,
    int ExpiraEnSegundos,
    int IntervaloSegundos,
    long TimestampUnix);

/// <summary>
/// Resultado de validación del token TOTP escaneado.
/// </summary>
public sealed record ResultadoValidacionQr(
    bool EsValido,
    string CodigoEstacion,
    string? MensajeError = null);

/// <summary>
/// Servicio criptográfico de generación y validación de códigos QR rotativos (estilo TOTP / Authenticator)
/// para estaciones físicas o tótems de acceso.
/// </summary>
public interface IQrEstacionService
{
    /// <summary>
    /// Genera el token rotativo actual para una estación determinada.
    /// </summary>
    TokenQrEstacionDto GenerarTokenActual(string? codigoEstacion = null);

    /// <summary>
    /// Valida un token QR escaneado por un empleado.
    /// Acepta el paso actual y el anterior con ventana de tolerancia e implementa anti-reutilización.
    /// </summary>
    ResultadoValidacionQr ValidarToken(string token, long idEmpleado);
}
