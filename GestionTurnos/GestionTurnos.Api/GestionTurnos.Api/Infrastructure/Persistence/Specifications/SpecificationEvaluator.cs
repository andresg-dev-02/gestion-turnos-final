using GestionTurnos.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence.Specifications;

/// <summary>
/// Evaluador que traduce una ISpecification (agnóstica) en una consulta LINQ de Entity Framework Core.
/// Centraliza toda la construcción del IQueryable.
/// </summary>
public class SpecificationEvaluator<TEntity> where TEntity : class
{
    public static IQueryable<TEntity> GetQuery(IQueryable<TEntity> inputQuery, ISpecification<TEntity> spec)
    {
        var query = inputQuery;

        if (spec.IsAsNoTracking)
        {
            query = query.AsNoTracking();
        }

        foreach (var criteria in spec.Criterias)
        {
            query = query.Where(criteria);
        }

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip).Take(spec.Take);
        }

        return query;
    }
}

