using System.Reflection;
using FluentValidation;
using Koto.Application;
using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// Pipeline behavior that runs all registered <see cref="IValidator{T}"/> for the request
/// before the handler executes. Returns a <c>Result.Failure</c> with a combined error
/// message when any rule fails. <typeparamref name="TResponse"/> must be <c>Result&lt;T&gt;</c>
/// for some T.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type (<c>Result&lt;T&gt;</c>).</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>Initializes the behavior with all validators registered for <typeparamref name="TRequest"/>.</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var message = string.Join("; ", failures.Select(f => f.ErrorMessage));
        var error = new Error("validation.failed", message);

        // TResponse is Result<T> — create Failure via reflection (once per type, effectively cached by JIT)
        var failureMethod = typeof(TResponse)
            .GetMethod(nameof(Result<Unit>.Failure), BindingFlags.Public | BindingFlags.Static)!;

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
