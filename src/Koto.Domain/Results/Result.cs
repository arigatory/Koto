namespace Koto.Domain;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value of
/// type <typeparamref name="T"/> or fail with an <see cref="Domain.Error"/>.
/// Use <see cref="Success"/> / <see cref="Failure"/> to construct, or rely on the
/// implicit conversions from <typeparamref name="T"/> and <see cref="Domain.Error"/>.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed class Result<T> : IResultBase
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _error = error;
    }

    /// <summary><c>true</c> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><c>true</c> when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws <see cref="InvalidOperationException"/> on failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    /// <summary>The error. Throws <see cref="InvalidOperationException"/> on success.</summary>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result wrapping <paramref name="error"/>.</summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicitly wraps an error in a failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Transforms the success value; propagates the error unchanged on failure.</summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(Error);

    /// <summary>Chains another operation on success; propagates the error on failure.</summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(Value) : Result<TNew>.Failure(Error);

    /// <summary>Runs <paramref name="action"/> on the success value; returns itself unchanged.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess) action(Value);
        return this;
    }

    /// <summary>Runs <paramref name="action"/> on the error; returns itself unchanged.</summary>
    public Result<T> TapError(Action<Error> action)
    {
        if (IsFailure) action(Error);
        return this;
    }

    /// <summary>Projects the result to a single value by providing handlers for both cases.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);

    /// <summary>
    /// Returns a failure with <paramref name="error"/> if the success value does not satisfy
    /// <paramref name="predicate"/>; otherwise returns itself unchanged.
    /// </summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        IsSuccess && !predicate(Value) ? Failure(error) : this;

    /// <summary>Async version of <see cref="Map{TNew}"/>.</summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper) =>
        IsSuccess ? Result<TNew>.Success(await mapper(Value)) : Result<TNew>.Failure(Error);

    /// <summary>Async version of <see cref="Bind{TNew}"/>.</summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder) =>
        IsSuccess ? await binder(Value) : Result<TNew>.Failure(Error);

    /// <summary>Async version of <see cref="Tap"/>.</summary>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        if (IsSuccess) await action(Value);
        return this;
    }

    /// <summary>Async version of <see cref="Ensure"/>.</summary>
    public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Error error)
    {
        if (!IsSuccess) return this;
        return await predicate(Value) ? this : Failure(error);
    }
}
