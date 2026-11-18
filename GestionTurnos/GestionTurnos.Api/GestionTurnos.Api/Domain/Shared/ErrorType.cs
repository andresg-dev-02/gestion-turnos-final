namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Enumera los tipos de error posibles para mapear a códigos HTTP de forma tipada.
/// Esto evita la comparación por cadenas de texto (string matching) y centraliza la lógica.
/// </summary>
public enum ErrorType
{
    /// <summary>Error genérico de negocio (400 Bad Request).</summary>
    Failure = 0,

    /// <summary>Error de validación de datos de entrada (400 Bad Request).</summary>
    Validation = 1,

    /// <summary>El recurso solicitado no fue encontrado (404 Not Found).</summary>
    NotFound = 2,

    /// <summary>Conflicto con el estado actual del recurso (409 Conflict).</summary>
    Conflict = 3,

    /// <summary>El usuario no está autenticado (401 Unauthorized).</summary>
    Unauthorized = 4,

    /// <summary>El usuario no tiene permisos suficientes (403 Forbidden).</summary>
    Forbidden = 5
}

