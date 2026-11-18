namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Interfaz marcadora que indica que un Command o Query
/// requiere que exista un usuario autenticado para su ejecución.
/// El <see cref="Behaviors.AuthorizationBehavior{TRequest, TResponse}"/>
/// interceptará automáticamente las peticiones que implementen esta interfaz.
/// </summary>
public interface IRequireAuthentication
{
}

