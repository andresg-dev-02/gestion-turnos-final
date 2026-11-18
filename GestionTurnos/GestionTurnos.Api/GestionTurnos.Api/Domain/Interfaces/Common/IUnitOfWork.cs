namespace GestionTurnos.Api.Domain.Interfaces;

/// <summary>
/// Define el contrato para el patrón Unit of Work Ligero.
/// Se encarga de manejar las transacciones de base de datos y asegurar 
/// el despacho de los Domain Events antes del guardado definitivo.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Guarda todos los cambios realizados en el contexto y despacha los eventos de dominio.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia una nueva transacción en la base de datos de forma explícita.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia una transacción fijando explícitamente el autor para la auditoría ('app.id_usuario').
    /// Se usa cuando aún no hay usuario autenticado en la solicitud (inicio de sesión).
    /// </summary>
    Task BeginTransactionAsync(long idUsuarioAuditoria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma la transacción actual, aplicando todos los cambios de forma permanente.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revierte la transacción actual, deshaciendo cualquier cambio no confirmado.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

