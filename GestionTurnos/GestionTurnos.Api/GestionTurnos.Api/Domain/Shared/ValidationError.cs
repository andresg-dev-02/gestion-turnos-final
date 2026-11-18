namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Extiende la clase Error base para soportar el diccionario de errores de FluentValidation.
/// </summary>
public sealed class ValidationError : Error
{
    public ValidationError(IDictionary<string, string[]> errors)
        : base("Validation.Error", "Uno o más errores de validación han ocurrido.", ErrorType.Validation)
    {
        Errors = errors;
    }

    /// <summary>
    /// Diccionario donde la llave es la propiedad que falló y el valor es la lista de mensajes.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }
}

