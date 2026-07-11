namespace Koto.Domain;

/// <summary>
/// Controls how <see cref="Result.Sequence{T}"/> / <see cref="Result.Traverse{TIn,TOut}"/>
/// react to failed elements.
/// </summary>
public enum FailureMode
{
    /// <summary>Visit every element and aggregate ALL errors into the failed result.</summary>
    Aggregate = 0,

    /// <summary>
    /// Stop at the first failed element and return only its errors. Useful when the
    /// selector performs I/O and visiting the remaining elements would be wasted work.
    /// </summary>
    FailFast = 1,
}

public static partial class Result
{
    /// <summary>
    /// Folds a collection of results into one: when every element succeeds, returns the
    /// values in the original order; otherwise returns a failure carrying the errors
    /// according to <paramref name="mode"/>.
    /// </summary>
    /// <example><code>
    /// Result&lt;IReadOnlyList&lt;Email&gt;&gt; emails = Result.Sequence(rawEmails.Select(Email.Create));
    /// </code></example>
    /// <param name="results">The results to fold, in order.</param>
    /// <param name="mode">Error accumulation strategy; all errors by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> (or an element) is <c>null</c>.</exception>
    public static Result<IReadOnlyList<T>> Sequence<T>(
        IEnumerable<Result<T>> results, FailureMode mode = FailureMode.Aggregate)
    {
        ArgumentNullException.ThrowIfNull(results);

        var values = new List<T>(results.TryGetNonEnumeratedCount(out var count) ? count : 4);
        List<Error>? errors = null;

        foreach (var result in results)
        {
            ArgumentNullException.ThrowIfNull(result, nameof(results));
            if (result.IsSuccess)
            {
                if (errors is null) values.Add(result.Value);
            }
            else
            {
                (errors ??= []).AddRange(result.Errors);
                if (mode == FailureMode.FailFast) break;
            }
        }

        return errors is null
            ? Result<IReadOnlyList<T>>.Success(values)
            : Result<IReadOnlyList<T>>.Failure(errors);
    }

    /// <summary>
    /// Maps every element through <paramref name="selector"/> and folds the outcomes into
    /// one result (<c>map</c> + <see cref="Sequence{T}"/> in a single pass, with no
    /// intermediate collection of results).
    /// </summary>
    /// <example><code>
    /// Result&lt;IReadOnlyList&lt;Money&gt;&gt; prices = Result.Traverse(lines, l => Money.Create(l.Amount));
    /// </code></example>
    /// <param name="items">The source elements, in order.</param>
    /// <param name="selector">Maps an element to a result.</param>
    /// <param name="mode">Error accumulation strategy; all errors by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Result<IReadOnlyList<TOut>> Traverse<TIn, TOut>(
        IEnumerable<TIn> items, Func<TIn, Result<TOut>> selector, FailureMode mode = FailureMode.Aggregate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        var values = new List<TOut>(items.TryGetNonEnumeratedCount(out var count) ? count : 4);
        List<Error>? errors = null;

        foreach (var item in items)
        {
            var result = selector(item);
            if (result.IsSuccess)
            {
                if (errors is null) values.Add(result.Value);
            }
            else
            {
                (errors ??= []).AddRange(result.Errors);
                if (mode == FailureMode.FailFast) break;
            }
        }

        return errors is null
            ? Result<IReadOnlyList<TOut>>.Success(values)
            : Result<IReadOnlyList<TOut>>.Failure(errors);
    }

    /// <summary>
    /// Async <see cref="Traverse{TIn,TOut}"/>: awaits <paramref name="selector"/> for each
    /// element SEQUENTIALLY (never in parallel) — in domain scenarios ordering and early
    /// access to errors matter more than concurrency.
    /// </summary>
    /// <example><code>
    /// var jumps = await Result.TraverseAsync(request.Jumps, ResolveJumpAsync, FailureMode.FailFast);
    /// </code></example>
    /// <param name="items">The source elements, in order.</param>
    /// <param name="selector">Maps an element to an asynchronous result.</param>
    /// <param name="mode">Error accumulation strategy; all errors by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    public static Task<Result<IReadOnlyList<TOut>>> TraverseAsync<TIn, TOut>(
        IEnumerable<TIn> items, Func<TIn, Task<Result<TOut>>> selector, FailureMode mode = FailureMode.Aggregate)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);
        return TraverseAsync(items, (item, _) => selector(item), mode, CancellationToken.None);
    }

    /// <summary>
    /// Async <see cref="Traverse{TIn,TOut}"/> with cancellation: awaits
    /// <paramref name="selector"/> for each element SEQUENTIALLY and checks
    /// <paramref name="cancellationToken"/> before each call.
    /// </summary>
    /// <example><code>
    /// var jumps = await Result.TraverseAsync(request.Jumps, ResolveJumpAsync, FailureMode.FailFast, ct);
    /// </code></example>
    /// <param name="items">The source elements, in order.</param>
    /// <param name="selector">Maps an element to an asynchronous result; receives the token.</param>
    /// <param name="mode">Error accumulation strategy; all errors by default.</param>
    /// <param name="cancellationToken">Checked before each selector invocation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="selector"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static async Task<Result<IReadOnlyList<TOut>>> TraverseAsync<TIn, TOut>(
        IEnumerable<TIn> items,
        Func<TIn, CancellationToken, Task<Result<TOut>>> selector,
        FailureMode mode = FailureMode.Aggregate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(selector);

        var values = new List<TOut>(items.TryGetNonEnumeratedCount(out var count) ? count : 4);
        List<Error>? errors = null;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await selector(item, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                if (errors is null) values.Add(result.Value);
            }
            else
            {
                (errors ??= []).AddRange(result.Errors);
                if (mode == FailureMode.FailFast) break;
            }
        }

        return errors is null
            ? Result<IReadOnlyList<TOut>>.Success(values)
            : Result<IReadOnlyList<TOut>>.Failure(errors);
    }
}
