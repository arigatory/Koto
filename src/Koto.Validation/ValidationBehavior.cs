using FluentValidation;
using FluentValidation.Results;
using Koto.Application;
using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{T}"/>s for the request
/// before the handler executes. On failure, returns a failed result carrying one structured
/// <see cref="Error"/> per validation failure (code, message, and the offending field) —
/// no information is collapsed into a single message string.
/// </summary>
/// <remarks>
/// The <typeparamref name="TResponse"/> constraint (<see cref="IResultFactory{TSelf}"/>)
/// makes failure construction compile-time safe; the dispatcher always closes this behavior
/// over <c>Result&lt;T&gt;</c>, which satisfies it.
/// </remarks>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type (<c>Result&lt;T&gt;</c>).</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResultBase, IResultFactory<TResponse>
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
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct))).ConfigureAwait(false);

        var errors = results
            .SelectMany(r => r.Errors)
            .Select(ToError)
            .ToArray();

        return errors.Length == 0
            ? await next().ConfigureAwait(false)
            : TResponse.FromErrors(errors);
    }

    private static Error ToError(ValidationFailure failure) =>
        failure.CustomState is Error domainError
            // Structured Error attached by KotoValidators (domain factory failure).
            ? domainError.Field is null ? domainError with { Field = failure.PropertyName } : domainError
            : new Error(
                string.IsNullOrEmpty(failure.ErrorCode) ? "validation.failed" : failure.ErrorCode,
                failure.ErrorMessage)
            { Field = string.IsNullOrEmpty(failure.PropertyName) ? null : failure.PropertyName };
}
