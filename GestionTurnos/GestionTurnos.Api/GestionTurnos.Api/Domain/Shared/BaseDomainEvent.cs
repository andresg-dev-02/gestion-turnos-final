namespace GestionTurnos.Api.Domain.Shared;

public abstract class BaseDomainEvent : IDomainEvent
{
    public DateTime DateOccurred { get; protected set; } = DateTime.UtcNow;
}

