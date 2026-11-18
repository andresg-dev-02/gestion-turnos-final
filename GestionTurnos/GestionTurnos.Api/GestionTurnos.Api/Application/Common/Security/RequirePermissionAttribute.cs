namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Atributo declarativo para exigir uno o más permisos específicos en Commands o Queries de CQRS.
/// Evaluado automáticamente por <see cref="Behaviors.AuthorizationBehavior{TRequest, TResponse}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// Código del permiso requerido (ej: 'LIQUIDACION.GENERAR', 'TARIFAS.CREAR').
    /// </summary>
    public string Permiso { get; }

    public RequirePermissionAttribute(string permiso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permiso);
        Permiso = permiso;
    }
}

