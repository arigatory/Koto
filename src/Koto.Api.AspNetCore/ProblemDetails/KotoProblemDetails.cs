using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Factory for RFC 7807 Problem Details responses derived from Koto <see cref="Error"/>s.
/// Status codes come from <see cref="KotoHttpErrorOptions"/>; responses include
/// <c>errorCode</c>/<c>errorCodes</c> and (when provided) <c>correlationId</c> extensions.
/// </summary>
public static class KotoProblemDetails
{
    /// <summary>Creates an <see cref="IResult"/> representing a single <paramref name="error"/> as Problem Details.</summary>
    public static IResult From(Error error, KotoHttpErrorOptions options, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(options);

        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };
        if (error.Field is not null)
            extensions["field"] = error.Field;
        if (correlationId is not null)
            extensions["correlationId"] = correlationId;

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: options.StatusCodeFor(error),
            extensions: extensions);
    }

    /// <summary>
    /// Creates an <see cref="IResult"/> for one or more <paramref name="errors"/>.
    /// A single error delegates to <see cref="From(Error,KotoHttpErrorOptions,string?)"/>;
    /// multiple errors produce validation Problem Details (400) with an <c>errors</c>
    /// dictionary grouped by <see cref="Error.Field"/> and an <c>errorCodes</c> extension.
    /// </summary>
    public static IResult From(IReadOnlyList<Error> errors, KotoHttpErrorOptions options, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(options);
        if (errors.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));

        if (errors.Count == 1)
            return From(errors[0], options, correlationId);

        var extensions = new Dictionary<string, object?>
        {
            ["errorCodes"] = errors.Select(e => e.Code).ToArray(),
        };
        if (correlationId is not null)
            extensions["correlationId"] = correlationId;

        return TypedResults.ValidationProblem(
            errors: GroupByField(errors),
            extensions: extensions);
    }

    /// <summary>Groups error messages by <see cref="Error.Field"/> (empty key for cross-field errors).</summary>
    public static Dictionary<string, string[]> GroupByField(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors
            .GroupBy(e => e.Field ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);
    }
}
