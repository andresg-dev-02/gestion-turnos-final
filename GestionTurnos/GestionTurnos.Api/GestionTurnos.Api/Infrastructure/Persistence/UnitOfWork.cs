using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestionTurnos.Api.Infrastructure.Persistence;

/// <summary>
/// Implementación del patrón Unit of Work.
/// Garantiza que toda operación de escritura se ejecute en una transacción con las variables de sesión
/// 'app.id_usuario' y 'app.ip' configuradas para el disparador de auditoría (Sección 1 y 7),
/// y evita el borrado físico en tablas de negocio (RG-06).
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly GestionTurnosContext _context;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private IDbContextTransaction? _transaction;
    private bool _auditSessionConfigured;

    public UnitOfWork(
        GestionTurnosContext context,
        IMediator mediator,
        ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateNoPhysicalDeletesOnBusinessTables();

        // Toda operación de escritura va en una transacción con app.id_usuario y app.ip configurados
        bool startedLocalTransaction = false;
        if (_transaction is null && _context.Database.CurrentTransaction is null)
        {
            await BeginTransactionAsync(cancellationToken);
            startedLocalTransaction = true;
        }
        else if (!_auditSessionConfigured)
        {
            await ConfigureAuditSessionAsync(_currentUserService.UserId, cancellationToken);
        }

        try
        {
            // 1. Recolectar todas las entidades modificadas que tengan eventos de dominio
            var domainEntities = _context.ChangeTracker
                .Entries<BaseEntity>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
                .ToList();

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            // 2. Limpiar los eventos de las entidades para evitar que se disparen múltiples veces
            domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());

            // 3. Despachar los eventos usando MediatR (Antes de guardar en BD)
            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }

            // 4. Guardar cambios finales
            var result = await _context.SaveChangesAsync(cancellationToken);

            if (startedLocalTransaction && _transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken);
                await _transaction.DisposeAsync();
                _transaction = null;
                _auditSessionConfigured = false;
            }

            return result;
        }
        catch
        {
            if (startedLocalTransaction && _transaction is not null)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => BeginTransactionCoreAsync(_currentUserService.UserId, cancellationToken);

    /// <inheritdoc />
    public Task BeginTransactionAsync(long idUsuarioAuditoria, CancellationToken cancellationToken = default)
        => BeginTransactionCoreAsync(idUsuarioAuditoria, cancellationToken);

    private async Task BeginTransactionCoreAsync(long? idUsuarioAuditoria, CancellationToken cancellationToken)
    {
        if (_transaction != null)
        {
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await ConfigureAuditSessionAsync(idUsuarioAuditoria, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);

            if (_transaction != null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
                _auditSessionConfigured = false;
            }
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
                _auditSessionConfigured = false;
            }
        }
    }

    /// <summary>
    /// Fija 'app.id_usuario' y 'app.ip' al inicio de la transacción de escritura (Sección 1).
    /// </summary>
    private async Task ConfigureAuditSessionAsync(long? idUsuario, CancellationToken cancellationToken)
    {
        var userId = idUsuario?.ToString() ?? string.Empty;
        var ip = _currentUserService.IpAddress ?? string.Empty;
        var providerName = _context.Database.ProviderName ?? string.Empty;

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.id_usuario', {0}, true), set_config('app.ip', {1}, true);",
                new object[] { userId, ip },
                cancellationToken);
        }
        else if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sys.sp_set_session_context @key = N'app.id_usuario', @value = {0}; EXEC sys.sp_set_session_context @key = N'app.ip', @value = {1};",
                new object[] { userId, ip },
                cancellationToken);
        }

        _auditSessionConfigured = true;
    }

    /// <summary>
    /// Regla Sección 1 y RG-06: No usar DELETE en tablas de negocio.
    /// Excepciones permitidas: asistencia.detalle_horas al reliquidar (Sección 6.3), seguridad.rol_permiso
    /// y parametrizacion.dia_festivo (los festivos se crean, editan y borran - Sección 13).
    /// </summary>
    private void ValidateNoPhysicalDeletesOnBusinessTables()
    {
        var forbiddenDeletes = _context.ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Deleted &&
                        e.Entity is not DetalleHoras &&
                        e.Entity is not RolPermiso &&
                        e.Entity is not DiaFestivo)
            .ToList();

        if (forbiddenDeletes.Count > 0)
        {
            var entityNames = string.Join(", ", forbiddenDeletes.Select(e => e.Entity.GetType().Name).Distinct());
            throw new InvalidOperationException(
                $"No está permitido el borrado físico (DELETE) en tablas de negocio ({entityNames}). Desactive o cambie el estado del registro (RG-06).");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }

        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
