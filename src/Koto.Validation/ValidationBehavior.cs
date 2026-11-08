using FluentValidation;
using FluentValidation.Results;
using Koto.Application;
using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{T}"/>s for the request —
/// sequentially, so validators can share scoped services such as a DbContext — before the handler
/// executes. On failure it returns a failed result carrying one <see cref="EnrichedError"/> per
/// validation failure, bound to the offending field: a domain error produced through
/// <see cref="KotoValidators.MustBeValueObject{T,TSource,TValueObject}"/> keeps its code; any other
/// rule failure becomes <see cref="ValidationRuleFailed"/>.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type (<c>AppResult&lt;T&gt;</c>).</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResultBase, IAppResultFactory<TResponse>
{
    private readonly IReadOnlyList<IValidator<TRequest>> _validators;

    /// <summary>Initializes the behavior with all validators registered for <typeparamref name="TRequest"/>.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators.ToArray();

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (_validators.Count == 0)
            return await next().ConfigureAwait(false);

        var context = new ValidationContext<TRequest>(request);
        var errors = new List<EnrichedError>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, ct).ConfigureAwait(false);
            errors.AddRange(result.Errors.Select(ToError));
        }

        return errors.Count == 0
            ? await next().ConfigureAwait(false)
            : TResponse.FromErrors(errors);
    }

    private static EnrichedError ToError(ValidationFailure failure)
    {
        var field = string.IsNullOrEmpty(failure.PropertyName) ? null : failure.PropertyName;
        var error = failure.CustomState is Error domainError
            ? domainError.Enrich()
            : new ValidationRuleFailed(
                string.IsNullOrEmpty(failure.ErrorCode) ? "unknown" : failure.ErrorCode,
                failure.ErrorMessage).Enrich();
        return error.Field is null && field is not null ? error with { Field = field } : error;
    }
}
