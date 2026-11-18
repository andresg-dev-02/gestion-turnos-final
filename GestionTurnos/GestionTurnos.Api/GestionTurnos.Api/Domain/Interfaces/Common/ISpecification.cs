using System.Linq.Expressions;

namespace GestionTurnos.Api.Domain.Interfaces;

/// <summary>
/// Define la estructura de una Especificación para consultas de base de datos.
/// Permite encapsular la lógica de las consultas (Where, Includes, Order, Paging) 
/// de forma tipada, reusable y agnóstica a Entity Framework.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>Criterios de búsqueda combinados (Where). Soportan múltiples filtros encadenados (AND).</summary>
    List<Expression<Func<T, bool>>> Criterias { get; }

    /// <summary>Lista de relaciones fuertemente tipadas que se deben incluir (Join).</summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Nombres de relaciones que se deben incluir como cadenas (Join anidados).</summary>
    List<string> IncludeStrings { get; }

    /// <summary>Criterio de ordenamiento ascendente.</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>Criterio de ordenamiento descendente.</summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>Indica si la paginación está habilitada para esta especificación.</summary>
    bool IsPagingEnabled { get; }

    /// <summary>Cantidad de registros a saltar para la paginación.</summary>
    int Skip { get; }

    /// <summary>Cantidad de registros a tomar para la paginación.</summary>
    int Take { get; }

    /// <summary>Indica si se deshabilita el rastreo de entidades (mejora rendimiento).</summary>
    bool IsAsNoTracking { get; }
}

