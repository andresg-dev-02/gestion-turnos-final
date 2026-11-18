namespace GestionTurnos.Api.Presentation.Contracts.Responses;

/// <summary>
/// Respuesta estándar de la API cuando no hay un payload de datos.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static ApiResponse Ok(
        string message = "Operación realizada correctamente.")
    {
        return new ApiResponse
        {
            Success = true,
            Message = message
        };
    }
}

/// <summary>
/// Respuesta estándar de la API con un payload de datos.
/// </summary>
/// <typeparam name="T">El tipo del dato devuelto.</typeparam>
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(
        T? data,
        string message = "Operación realizada correctamente.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }
}

