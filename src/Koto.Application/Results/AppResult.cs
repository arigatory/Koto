using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Outcome of an application use case (command or query handler): either a value of type
/// <typeparamref name="T"/> or one or more <see cref="EnrichedError"/>s. This is the type the API
/// layer accepts; a domain <see cref="Result{T}"/> never reaches it without an explicit
/// <c>ForField</c>, <c>Enrich()</c> or <c>Enrich(map)</c> — there is no implicit conversion from
/// <see cref="Result{T}"/> or from a plain <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed class AppResult<T> : IResultBase, IAppResultFactory<AppResult<T>>
{
    private readonly T? _value;
    private readonly ErrorList<EnrichedError> _errors;

    private AppResult(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private AppResult(ErrorList<EnrichedError> errors)
    {
        IsSuccess = false;
        _errors = errors;
    }

    /// <summary><c>true</c> when the use case succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><c>true</c> when the use case failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws <see cref="InvalidOperationException"/> on failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    /// <summary>The first error — the headline of the failure. Throws <see cref="InvalidOperationException"/> on success.</summary>
    public EnrichedError FirstError => IsFailure
        ? _errors[0]
        : throw new InvalidOperationException("Cannot access FirstError on a successful result.");

    /// <summary>All errors of a failed result; empty on success.</summary>
    public ErrorList<EnrichedError> Errors => _errors;

    IReadOnlyList<Error> IResultBase.Errors => _errors;

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public static AppResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value);
    }

    /// <summary>Creates a failed result wrapping <paramref name="error"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <c>null</c>.</exception>
    public static AppResult<T> Failure(EnrichedError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new((ErrorList<EnrichedError>)error);
    }

    /// <summary>Creates a failed result carrying all of <paramref name="errors"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> (or an element) is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static AppResult<T> Failure(IEnumerable<EnrichedError> errors) =>
        Failure(new ErrorList<EnrichedError>(errors));

    /// <summary>Creates a failed result carrying all of <paramref name="errors"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static AppResult<T> Failure(ErrorList<EnrichedError> errors)
    {
        if (errors.Count == 0)
            throw new ArgumentException(
                "A failed result requires at least one error. Did you return the Errors of a successful result?",
                nameof(errors));
        return new(errors);
    }

    static AppResult<T> IAppResultFactory<AppResult<T>>.FromErrors(IReadOnlyList<EnrichedError> errors) =>
        Failure(errors);

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator AppResult<T>(T value) => Success(value);

    /// <summary>Implicitly wraps an enriched error in a failed result.</summary>
    public static implicit operator AppResult<T>(EnrichedError error) => Failure(error);

    /// <summary>Implicitly wraps a non-empty enriched error list in a failed result (<c>return x.Errors.Enrich();</c>).</summary>
    public static implicit operator AppResult<T>(ErrorList<EnrichedError> errors) => Failure(errors);

    /// <summary>Transforms the success value; propagates all errors unchanged on failure.</summary>
    public AppResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? AppResult<TNew>.Success(mapper(Value)) : AppResult<TNew>.Failure(_errors);
    }

    /// <summary>Projects the result to a single value by providing handlers for both cases.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ErrorList<EnrichedError>, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(_errors);

    /// <summary>Async version of <see cref="Match{TResult}"/> where both handlers are asynchronous.</summary>
    public Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess, Func<ErrorList<EnrichedError>, Task<TResult>> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(_errors);

    /// <summary>Async version of <see cref="Match{TResult}"/> where only the success handler is asynchronous.</summary>
    public Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess, Func<ErrorList<EnrichedError>, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : Task.FromResult(onFailure(_errors));
}
