namespace Koto.Domain;

/// <summary>
/// Companion helpers for <see cref="Result{T}"/>: void results (<see cref="Result{T}"/> of
/// <see cref="Unit"/>) and aggregation of several results into one via <c>Combine</c>.
/// </summary>
public static class Result
{
    /// <summary>Creates a successful void result.</summary>
    public static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

    /// <summary>Creates a failed void result wrapping <paramref name="error"/>.</summary>
    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);

    /// <summary>Creates a failed void result carrying all of <paramref name="errors"/>.</summary>
    public static Result<Unit> Failure(IEnumerable<Error> errors) => Result<Unit>.Failure(errors);

    /// <summary>
    /// Combines two results: success with a tuple of both values when both succeed,
    /// otherwise a failure aggregating ALL errors from both (not just the first).
    /// </summary>
    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);
        return r1.IsSuccess && r2.IsSuccess
            ? Result<(T1, T2)>.Success((r1.Value, r2.Value))
            : Result<(T1, T2)>.Failure(CollectErrors(r1, r2));
    }

    /// <summary>
    /// Combines three results: success with a tuple of all values when all succeed,
    /// otherwise a failure aggregating ALL errors.
    /// </summary>
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(Result<T1> r1, Result<T2> r2, Result<T3> r3)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);
        ArgumentNullException.ThrowIfNull(r3);
        return r1.IsSuccess && r2.IsSuccess && r3.IsSuccess
            ? Result<(T1, T2, T3)>.Success((r1.Value, r2.Value, r3.Value))
            : Result<(T1, T2, T3)>.Failure(CollectErrors(r1, r2, r3));
    }

    /// <summary>
    /// Combines four results: success with a tuple of all values when all succeed,
    /// otherwise a failure aggregating ALL errors.
    /// </summary>
    public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
        Result<T1> r1, Result<T2> r2, Result<T3> r3, Result<T4> r4)
    {
        ArgumentNullException.ThrowIfNull(r1);
        ArgumentNullException.ThrowIfNull(r2);
        ArgumentNullException.ThrowIfNull(r3);
        ArgumentNullException.ThrowIfNull(r4);
        return r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess
            ? Result<(T1, T2, T3, T4)>.Success((r1.Value, r2.Value, r3.Value, r4.Value))
            : Result<(T1, T2, T3, T4)>.Failure(CollectErrors(r1, r2, r3, r4));
    }

    /// <summary>
    /// Combines any number of results into a void result: success when all succeed,
    /// otherwise a failure aggregating ALL errors in argument order.
    /// </summary>
    public static Result<Unit> Combine(params IResultBase[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (Array.IndexOf(results, null) >= 0)
            throw new ArgumentNullException(nameof(results), "Result collection must not contain null.");
        var errors = CollectErrors(results);
        return errors.Count == 0 ? Success() : Result<Unit>.Failure(errors);
    }

    private static List<Error> CollectErrors(params IResultBase[] results)
    {
        var errors = new List<Error>();
        foreach (var result in results)
            errors.AddRange(result.Errors);
        return errors;
    }
}
