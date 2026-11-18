using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GestionTurnos.Api.Infrastructure.Persistence;

/// <summary>
/// Bloqueo pesimista de una fila dentro de la transacción en curso
/// (<c>SELECT ... FOR UPDATE</c> en PostgreSQL, <c>WITH (UPDLOCK, ROWLOCK)</c> en SQL Server).
/// Debe llamarse después de <c>IUnitOfWork.BeginTransactionAsync</c>; el bloqueo se libera al confirmar o revertir.
/// </summary>
public static class BloqueoFilasExtensions
{
    public static async Task<TEntity?> BloquearPorIdAsync<TEntity>(
        this DbContext context,
        object id,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"La entidad {typeof(TEntity).Name} no está mapeada.");

        var tabla = entityType.GetTableName()
            ?? throw new InvalidOperationException($"La entidad {typeof(TEntity).Name} no está mapeada a una tabla.");
        var esquema = entityType.GetSchema();

        var clave = entityType.FindPrimaryKey()?.Properties;
        if (clave is null || clave.Count != 1)
        {
            throw new InvalidOperationException($"La entidad {typeof(TEntity).Name} debe tener una clave primaria simple.");
        }

        var storeObject = StoreObjectIdentifier.Table(tabla, esquema);
        var columnaClave = clave[0].GetColumnName(storeObject)
            ?? throw new InvalidOperationException($"No se pudo resolver la columna clave de {typeof(TEntity).Name}.");

        var proveedor = context.Database.ProviderName ?? string.Empty;
        var nombreTabla = string.IsNullOrEmpty(esquema) ? tabla : $"{esquema}.{tabla}";

        // Los identificadores vienen del modelo de EF (no del cliente); el valor de la clave va parametrizado.
        string sql = proveedor.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? $"SELECT * FROM {nombreTabla} WITH (UPDLOCK, ROWLOCK) WHERE {columnaClave} = {{0}}"
            : $"SELECT * FROM {nombreTabla} WHERE {columnaClave} = {{0}} FOR UPDATE";

        // Sin composición LINQ para que el proveedor no envuelva la sentencia de bloqueo en una subconsulta.
        var filas = await context.Set<TEntity>()
            .FromSqlRaw(sql, id)
            .AsTracking()
            .ToListAsync(cancellationToken);

        return filas.FirstOrDefault();
    }
}
