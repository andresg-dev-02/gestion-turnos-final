namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Clase base para entidades del dominio. Proporciona la capacidad de almacenar
/// eventos de dominio que serán despachados justo antes de guardar en la base de datos.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Colección de eventos de dominio pendientes por despachar.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Agrega un nuevo evento de dominio a la entidad.
    /// </summary>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Remueve un evento de dominio específico.
    /// </summary>
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Limpia todos los eventos de dominio una vez han sido despachados.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

