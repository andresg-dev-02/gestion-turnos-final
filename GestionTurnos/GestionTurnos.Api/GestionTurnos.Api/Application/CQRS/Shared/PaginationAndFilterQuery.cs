namespace GestionTurnos.Api.Application.CQRS.Shared;

/// <summary>
/// Clase base para consultas paginadas que incluyen filtros dinámicos estandarizados.
/// </summary>
public abstract record PaginationAndFilterQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public string? SortColumn { get; init; }
    public bool SortDescending { get; init; }
}

