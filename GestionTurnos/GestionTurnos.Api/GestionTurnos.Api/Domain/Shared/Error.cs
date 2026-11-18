namespace GestionTurnos.Api.Domain.Shared;

public class Error
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("Error.NullValue", "El valor del resultado especificado es nulo.", ErrorType.Failure);

    public Error(string code, string message, ErrorType type = ErrorType.Failure)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    public string Code { get; }
    public string Message { get; }

    /// <summary>
    /// Tipo de error utilizado para mapear a códigos de respuesta HTTP de forma tipada.
    /// </summary>
    public ErrorType Type { get; }

    public static implicit operator string(Error error) => error.Code;

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        if (obj is not Error error)
        {
            return false;
        }

        return error.Code == Code && error.Message == Message;
    }

    public override int GetHashCode() => HashCode.Combine(Code, Message);
}

