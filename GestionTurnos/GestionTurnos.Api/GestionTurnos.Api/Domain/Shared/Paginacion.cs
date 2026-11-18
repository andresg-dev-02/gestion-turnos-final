namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Límites comunes de paginación.
/// Sin tope, (pageNumber - 1) * pageSize desborda int con páginas muy grandes
/// (p. ej. pageNumber=999999999) y PostgreSQL responde "OFFSET must not be negative" (error 500).
/// Con 100 000 páginas de hasta 100 registros el desplazamiento máximo es 10 000 000.
/// </summary>
public static class Paginacion
{
    public const int PaginaMaxima = 100_000;

    /// <summary>Número de página válido: entre 1 y <see cref="PaginaMaxima"/>.</summary>
    public static int NormalizarPagina(int pageNumber) => Math.Clamp(pageNumber, 1, PaginaMaxima);
}
