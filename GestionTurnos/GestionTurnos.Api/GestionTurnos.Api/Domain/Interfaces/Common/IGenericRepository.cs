using System.Linq.Expressions;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Interfaces;

/// <summary>
/// Define las operaciones de acceso a datos comunes para las entidades de la aplicación.
/// </summary>
/// <typeparam name="T">Tipo de entidad gestionada por el repositorio.</typeparam>
public interface IGenericRepository<T> where T : class
{
    /// <summary>
    /// Obtiene una entidad por su identificador entero (32 bits).
    /// </summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una entidad por su identificador largo (64 bits / bigint).
    /// </summary>
    Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una entidad de catálogo por su código cadena (cod_*).
    /// </summary>
    Task<T?> GetByIdAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la primera entidad que cumple con el criterio o nulo.
    /// </summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression, bool asNoTracking = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todas las entidades. 
    /// <param name="asNoTracking">Poner en true si solo se va a leer (mejora rendimiento).</param>
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(bool asNoTracking = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las entidades que cumplen con un criterio (sin paginación).
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> expression, bool asNoTracking = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si existe al menos una entidad que cumpla con el criterio.
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta el número de entidades que cumplen con el criterio.
    /// </summary>
    Task<int> CountAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las entidades de forma paginada para alto rendimiento.
    /// </summary>
    Task<PagedResult<T>> GetPagedAsync(int pageNumber, int pageSize, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las entidades que cumplen con el criterio, pero de forma paginada.
    /// </summary>
    Task<PagedResult<T>> FindPagedAsync(Expression<Func<T, bool>> expression, int pageNumber, int pageSize, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega una entidad al contexto de persistencia.
    /// </summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca una entidad existente para ser actualizada.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Marca una entidad para ser eliminada.
    /// </summary>
    void Remove(T entity);

    /// <summary>
    /// Obtiene una entidad aplicando una Especificación.
    /// </summary>
    Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una lista de entidades aplicando una Especificación.
    /// </summary>
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta el número total de entidades que cumplen con la Especificación.
    /// </summary>
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las entidades de forma paginada basándose en una Especificación.
    /// </summary>
    Task<PagedResult<T>> GetPagedWithSpecAsync(ISpecification<T> spec, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

