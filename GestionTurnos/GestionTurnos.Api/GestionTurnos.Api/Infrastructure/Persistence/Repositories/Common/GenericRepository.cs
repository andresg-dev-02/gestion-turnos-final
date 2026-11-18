using System.Linq.Expressions;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using GestionTurnos.Api.Infrastructure.Persistence.Specifications;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación genérica del patrón Repository utilizando Entity Framework Core.
/// Proporciona operaciones CRUD reutilizables, paginación y optimizaciones (AsNoTracking).
/// </summary>
/// <typeparam name="T">Tipo de entidad gestionada por el repositorio.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly GestionTurnosContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(GestionTurnosContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { code }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.Where(expression);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync(bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> expression, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.Where(expression);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(expression, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(expression, cancellationToken);
    }

    private const int MaxPageSize = 50;

    /// <inheritdoc />
    public async Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        pageNumber = Paginacion.NormalizarPagina(pageNumber);

        IQueryable<T> query = _dbSet;

        if (asNoTracking)
            query = query.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    /// <inheritdoc />
    public async Task<PagedResult<T>> FindPagedAsync(Expression<Func<T, bool>> expression, int pageNumber, int pageSize, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        pageNumber = Paginacion.NormalizarPagina(pageNumber);

        IQueryable<T> query = _dbSet.Where(expression);

        if (asNoTracking)
            query = query.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    /// <inheritdoc />
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    /// <inheritdoc />
    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    /// <inheritdoc />
    public async Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(spec).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(spec).CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<T>> GetPagedWithSpecAsync(ISpecification<T> spec, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        pageNumber = Paginacion.NormalizarPagina(pageNumber);

        var query = ApplySpecification(spec);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(_dbSet.AsQueryable(), spec);
    }
}

