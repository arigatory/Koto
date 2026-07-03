namespace Koto.Domain;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value of
/// type <typeparamref name="T"/> or fail with one or more <see cref="Domain.Error"/>s.
/// Use <see cref="Success"/> / <see cref="Failure(Domain.Error)"/> to construct, or rely on the
/// implicit conversions from <typeparamref name="T"/> and <see cref="Domain.Error"/>.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed class Result<T> : IResultBase, IResultFactory<Result<T>>
{
    private static readonly Error[] NoErrors = [];

    private readonly T? _value;
    private readonly Error[] _errors;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _errors = NoErrors;
    }

    private Result(Error[] errors)
    {
        IsSuccess = false;
        _errors = errors;
    }

    /// <summary><c>true</c> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><c>true</c> when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws <see cref="InvalidOperationException"/> on failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    /// <summary>
    /// The first error. Throws <see cref="InvalidOperationException"/> on success.
    /// A failed result carries at least one error; see <see cref="Errors"/> for all of them.
    /// </summary>
    public Error Error => IsFailure
        ? _errors[0]
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    /// <summary>All errors carried by a failed result; empty on success.</summary>
    public IReadOnlyList<Error> Errors => _errors;

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value);
    }

    /// <summary>Creates a failed result wrapping <paramref name="error"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <c>null</c>.</exception>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new([error]);
    }

    /// <summary>Creates a failed result carrying all of <paramref name="errors"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> (or an element) is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static Result<T> Failure(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var array = errors.ToArray();
        if (array.Length == 0)
            throw new ArgumentException("A failed result requires at least one error.", nameof(errors));
        if (Array.IndexOf(array, null) >= 0)
            throw new ArgumentNullException(nameof(errors), "Error collection must not contain null.");
        return new(array);
    }

    static Result<T> IResultFactory<Result<T>>.FromErrors(IReadOnlyList<Error> errors) => Failure(errors);

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicitly wraps an error in a failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Transforms the success value; propagates all errors unchanged on failure.</summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(_errors);

    /// <summary>Chains another operation on success; propagates all errors on failure.</summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(Value) : Result<TNew>.Failure(_errors);

    /// <summary>Runs <paramref name="action"/> on the success value; returns itself unchanged.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess) action(Value);
        return this;
    }

    /// <summary>Runs <paramref name="action"/> on the first error; returns itself unchanged.</summary>
    public Result<T> TapError(Action<Error> action)
    {
        if (IsFailure) action(Error);
        return this;
    }

    /// <summary>Runs <paramref name="action"/> on all errors; returns itself unchanged.</summary>
    public Result<T> TapErrors(Action<IReadOnlyList<Error>> action)
    {
        if (IsFailure) action(_errors);
        return this;
    }

    /// <summary>Projects the result to a single value by providing handlers for both cases.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);

    /// <summary>Async version of <see cref="Match{TResult}"/> where both handlers are asynchronous.</summary>
    public Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);

    /// <summary>Async version of <see cref="Match{TResult}"/> where only the success handler is asynchronous.</summary>
    public Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : Task.FromResult(onFailure(Error));

    /// <summary>
    /// Returns a failure with <paramref name="error"/> if the success value does not satisfy
    /// <paramref name="predicate"/>; otherwise returns itself unchanged.
    /// </summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsSuccess && !predicate(Value) ? Failure(error) : this;

    /// <summary>Async version of <see cref="Map{TNew}"/>.</summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper) =>
        IsSuccess ? Result<TNew>.Success(await mapper(Value).ConfigureAwait(false)) : Result<TNew>.Failure(_errors);

    /// <summary>Async version of <see cref="Bind{TNew}"/>.</summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder) =>
        IsSuccess ? await binder(Value).ConfigureAwait(false) : Result<TNew>.Failure(_errors);

    /// <summary>Async version of <see cref="Tap"/>.</summary>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        if (IsSuccess) await action(Value).ConfigureAwait(false);
        return this;
    }

    /// <summary>Async version of <see cref="Ensure"/>.</summary>
    public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Error error)
    {
        if (!IsSuccess) return this;
        return await predicate(Value).ConfigureAwait(false) ? this : Failure(error);
    }
}
