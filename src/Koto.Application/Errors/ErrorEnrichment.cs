using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// The explicit verbs that move errors from the domain into the application layer:
/// <c>ForField</c> (bind to a request field), <c>Enrich()</c> (nothing to add), <c>Enrich(map)</c>
/// (replace with a typed, fuller error) and <c>WithDetails</c> (optional additions).
/// </summary>
public static class ErrorEnrichment
{
    /// <summary>Returns the error as an <see cref="EnrichedError"/> without additions; already enriched errors are returned as-is.</summary>
    public static EnrichedError Enrich(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error as EnrichedError ?? new EnrichedError(error);
    }

    /// <summary>Binds the error to the request field named <paramref name="field"/>.</summary>
    public static EnrichedError ForField(this Error error, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return error.Enrich() with { Field = field };
    }

    /// <summary>Adds optional details (merged over existing ones; later keys win).</summary>
    public static EnrichedError WithDetails(this Error error, params (string Key, object? Value)[] details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var enriched = error.Enrich();
        var merged = new Dictionary<string, object?>(enriched.Details, StringComparer.Ordinal);
        foreach (var (key, value) in details)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            merged[key] = value;
        }

        return enriched with { Details = merged };
    }

    /// <summary>Enriches every error without additions ("the data is already complete").</summary>
    public static ErrorList<EnrichedError> Enrich(this ErrorList<Error> errors) =>
        errors.Select(static e => e.Enrich());

    /// <summary>Enriches every error through <paramref name="map"/>, e.g. replacing a domain error with a fuller typed one.</summary>
    public static ErrorList<EnrichedError> Enrich(this ErrorList<Error> errors, Func<Error, EnrichedError> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return errors.Select(map);
    }

    /// <summary>Binds every error to the request field named <paramref name="field"/>.</summary>
    public static ErrorList<EnrichedError> ForField(this ErrorList<Error> errors, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        return errors.Select(e => e.ForField(field));
    }

    /// <summary>
    /// Binds all errors of a failed result to <paramref name="field"/> while keeping it a domain
    /// <see cref="Result{T}"/>, so several results can still be combined before the final <c>Enrich()</c>.
    /// </summary>
    public static Result<T> ForField<T>(this Result<T> result, string field)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? result
            : Result<T>.Failure(result.Errors.ForField(field).Select(static e => (Error)e));
    }

    /// <summary>
    /// Converts a domain result into the application result returned by handlers: the value is kept,
    /// plain errors are wrapped without additions, already enriched errors are kept as they are.
    /// </summary>
    public static AppResult<T> Enrich<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? AppResult<T>.Success(result.Value) : AppResult<T>.Failure(result.Errors.Enrich());
    }
}
