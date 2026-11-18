namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Representa un resultado paginado para colecciones de datos grandes,
/// evitando problemas de memoria al retornar millones de registros.
/// </summary>
/// <typeparam name="T">Tipo de entidad contenida en la página.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Los elementos de la página actual, devueltos como colección de solo lectura.
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Número total de registros en la base de datos (sin paginación).
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// La página actual que se está consultando.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// La cantidad máxima de elementos por página.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Total de páginas disponibles según el TotalCount y el PageSize.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}

