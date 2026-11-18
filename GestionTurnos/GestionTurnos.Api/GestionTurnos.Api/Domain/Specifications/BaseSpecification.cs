using System.Linq.Expressions;
using GestionTurnos.Api.Domain.Interfaces;

namespace GestionTurnos.Api.Domain.Specifications;

/// <summary>
/// Implementación base de ISpecification. 
/// Se hereda para crear especificaciones concretas con filtros, joins y ordenamiento.
/// </summary>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    public List<Expression<Func<T, bool>>> Criterias { get; } = new();
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public List<string> IncludeStrings { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int Take { get; private set; }
    public int Skip { get; private set; }
    public bool IsPagingEnabled { get; private set; } = false;
    public bool IsAsNoTracking { get; private set; } = false;

    /// <summary>
    /// Constructor para crear una especificación que retorne todos los registros (sin filtros WHERE).
    /// </summary>
    protected BaseSpecification()
    {
    }

    /// <summary>
    /// Constructor para crear una especificación que aplica un filtro principal WHERE.
    /// </summary>
    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criterias.Add(criteria);
    }

    protected void AddCriteria(Expression<Func<T, bool>> criteria)
    {
        Criterias.Add(criteria);
    }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    protected void AddInclude(string includeString)
    {
        IncludeStrings.Add(includeString);
    }

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    {
        OrderByDescending = orderByDescendingExpression;
    }

    protected void ApplyAsNoTracking()
    {
        IsAsNoTracking = true;
    }
}

