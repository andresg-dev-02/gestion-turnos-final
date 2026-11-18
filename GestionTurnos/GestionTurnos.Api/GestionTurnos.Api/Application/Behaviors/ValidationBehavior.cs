using FluentValidation;
using GestionTurnos.Api.Domain.Shared;
using MediatR;

namespace GestionTurnos.Api.Application.Behaviors;

/// <summary>
/// Pipeline Behavior de MediatR que intercepta las peticiones (Comandos/Consultas)
/// para validarlas usando FluentValidation antes de que lleguen a su Handler.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationFailures = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = validationFailures
            .SelectMany(validationResult => validationResult.Errors)
            .Where(validationFailure => validationFailure != null)
            .GroupBy(
                x => x.PropertyName,
                x => x.ErrorMessage,
                (propertyName, errorMessages) => new
                {
                    Key = propertyName,
                    Values = errorMessages.Distinct().ToArray()
                })
            .ToDictionary(x => x.Key, x => x.Values);

        if (errors.Any())
        {
            return CreateValidationResult<TResponse>(errors);
        }

        return await next();
    }

    /// <summary>
    /// Utiliza Reflection para devolver un Result.Failure o Result&lt;T&gt;.Failure dinámicamente si la respuesta esperada 
    /// es un objeto Result o Result&lt;T&gt;, evitando así lanzar excepciones.
    /// </summary>
    private static TResponse CreateValidationResult<TResult>(IDictionary<string, string[]> errors)
    {
        var validationError = new ValidationError(errors);

        if (typeof(TResult) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(validationError);
        }

        var resultType = typeof(TResult).GetGenericArguments()[0];

        var method = typeof(Result)
            .GetMethods()
            .First(m => m.Name == "Failure" && m.IsGenericMethod)
            .MakeGenericMethod(resultType);

        var response = method.Invoke(null, new object[] { validationError });

        return (TResponse)response!;
    }
}

