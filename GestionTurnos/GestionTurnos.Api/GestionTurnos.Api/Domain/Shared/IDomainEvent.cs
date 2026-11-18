using MediatR;

namespace GestionTurnos.Api.Domain.Shared;

/// <summary>
/// Interfaz base para todos los eventos de dominio.
/// Hereda de INotification de MediatR para permitir el despacho en memoria.
/// </summary>
public interface IDomainEvent : INotification
{
}

