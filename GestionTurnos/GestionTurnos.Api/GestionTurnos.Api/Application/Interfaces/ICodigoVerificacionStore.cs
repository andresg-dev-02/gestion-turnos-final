namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Almacén de códigos de verificación de un solo uso (OTP) con vigencia, límite de intentos y de reenvíos.
/// Nunca guarda el código en claro: solo su HMAC, y todo el registro va cifrado con Data Protection.
/// </summary>
public interface ICodigoVerificacionStore
{
    /// <summary>
    /// Genera un código nuevo para (<paramref name="proposito"/>, <paramref name="clave"/>), reemplazando cualquier
    /// código anterior. <paramref name="carga"/> es un dato opaco que se devuelve al verificar con éxito.
    /// </summary>
    Task<CodigoEmitido> EmitirAsync(string proposito, string clave, string? carga, CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera un código nuevo conservando la carga. Devuelve <c>null</c> si no hay desafío vigente, si no ha pasado
    /// la espera mínima o si se agotaron los reenvíos.
    /// </summary>
    Task<CodigoReemitido?> ReemitirAsync(string proposito, string clave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica el código. Si es válido el desafío se elimina (un solo uso); si se agotan los intentos también.
    /// </summary>
    Task<VerificacionCodigo> VerificarAsync(string proposito, string clave, string codigo, CancellationToken cancellationToken = default);

    /// <summary>Elimina el desafío, si existe.</summary>
    Task DescartarAsync(string proposito, string clave, CancellationToken cancellationToken = default);
}

public sealed record CodigoEmitido(string Codigo, DateTimeOffset ExpiraEn);

public sealed record CodigoReemitido(string Codigo, DateTimeOffset ExpiraEn, string? Carga);

public enum EstadoVerificacionCodigo
{
    Valido,
    Invalido,
    NoExisteOExpirado,
    IntentosAgotados
}

public sealed record VerificacionCodigo(EstadoVerificacionCodigo Estado, string? Carga, int IntentosRestantes);
