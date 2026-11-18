using System.Collections.Concurrent;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Errors.Seguridad;
using GestionTurnos.Api.Domain.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace GestionTurnos.Api.Application.Behaviors;

/// <summary>
/// Pipeline Behavior de MediatR que intercepta peticiones decoradas con <see cref="AuthorizeAttribute"/>,
/// <see cref="IRequireAuthentication"/> o <see cref="RequirePermissionAttribute"/> para verificar
/// autenticación y permisos granulares (Sección 2 - RF-01, RG-08) antes de ejecutar el Handler.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUserService;

    private static readonly ConcurrentDictionary<Type, AuthorizeAttribute[]> _attributeCache = new();
    private static readonly ConcurrentDictionary<Type, RequirePermissionAttribute[]> _permissionCache = new();

    public AuthorizationBehavior(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attributes = _attributeCache.GetOrAdd(
            typeof(TRequest),
            t => (AuthorizeAttribute[])t.GetCustomAttributes(typeof(AuthorizeAttribute), true));

        var permissionAttributes = _permissionCache.GetOrAdd(
            typeof(TRequest),
            t => (RequirePermissionAttribute[])t.GetCustomAttributes(typeof(RequirePermissionAttribute), true));

        bool hasAuthorizeAttribute = attributes.Length > 0;
        bool implementsRequireAuth = request is IRequireAuthentication;
        bool hasPermissionAttribute = permissionAttributes.Length > 0;

        if (!hasAuthorizeAttribute && !implementsRequireAuth && !hasPermissionAttribute)
        {
            return await next();
        }

        // 1. Verificación obligatoria de Autenticación (401 Unauthorized)
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return CreateFailureResult<TResponse>(AuthErrors.Unauthorized);
        }

        // 2. Verificación de Permisos granulares declarados en [Authorize(Policy = "...")]
        foreach (var attribute in attributes)
        {
            if (!string.IsNullOrEmpty(attribute.Policy) &&
                !string.Equals(attribute.Policy, PoliticasSeguridad.Autenticado, StringComparison.OrdinalIgnoreCase))
            {
                if (!_currentUserService.TienePermiso(attribute.Policy))
                {
                    return CreateFailureResult<TResponse>(AuthErrors.Forbidden);
                }
            }
        }

        // 3. Verificación de Permisos granulares declarados con [RequirePermission("...")] (403 Forbidden)
        foreach (var permAttr in permissionAttributes)
        {
            if (!_currentUserService.TienePermiso(permAttr.Permiso))
            {
                return CreateFailureResult<TResponse>(AuthErrors.Forbidden);
            }
        }

        return await next();
    }

    private static TResponse CreateFailureResult<TResult>(Error error)
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var resultType = typeof(TResult).GetGenericArguments()[0];

        var method = typeof(Result)
            .GetMethods()
            .First(m => m.Name == "Failure" && m.IsGenericMethod)
            .MakeGenericMethod(resultType);

        var response = method.Invoke(null, new object[] { error });

        return (TResponse)response!;
    }
}
