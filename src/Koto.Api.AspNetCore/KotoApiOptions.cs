using Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.AspNetCore;

/// <summary>How <see cref="EnrichmentGuard"/> reacts when an error reaches the API without its fuller typed form.</summary>
public enum EnrichmentGuardMode
{
    /// <summary>Do not check.</summary>
    Off,

    /// <summary>Log a warning and send the response as is (default outside Development).</summary>
    Warn,

    /// <summary>Throw <see cref="InvalidOperationException"/> (default in Development and tests).</summary>
    Throw,
}

/// <summary>
/// HTTP-layer options: the coarse error → status classifier and the enrichment guard mode.
/// The status is a class for infrastructure (retries, auth flows, alerting), not a second error code:
/// exact code → custom rules → <c>*.not-found</c> 404 → <c>*.already-*</c> / <c>*.conflict</c> 409 →
/// <c>*.unauthorized</c> 401 → <c>*.forbidden</c> 403 → <c>general.*</c> / <c>validation.*</c> / field-bound 400
/// → <see cref="FallbackStatusCode"/> (422). For several errors the first one decides.
/// </summary>
public sealed class KotoApiOptions
{
    private readonly Dictionary<string, int> _exact = new(StringComparer.Ordinal);
    private readonly List<Func<Error, int?>> _rules = [];

    /// <summary>
    /// Status for business errors not matched by any rule. Defaults to 422 Unprocessable Entity:
    /// a failed result is a client-visible rule violation, never a server fault (500 is reserved for exceptions).
    /// </summary>
    public int FallbackStatusCode { get; set; } = StatusCodes.Status422UnprocessableEntity;

    /// <summary><see cref="EnrichmentGuard"/> mode; <c>null</c> means Throw in Development, Warn elsewhere.</summary>
    public EnrichmentGuardMode? EnrichmentGuard { get; set; }

    /// <summary>Maps an exact error code (highest priority), e.g. <c>Map("payments.gateway.failed", 502)</c>.</summary>
    public KotoApiOptions Map(string exactCode, int statusCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(exactCode);
        _exact[exactCode] = statusCode;
        return this;
    }

    /// <summary>Adds a custom rule evaluated before the built-in classifier; later rules win, e.g. <c>Map(e =&gt; e.Code.EndsWith(".rate-limited") ? 429 : null)</c>.</summary>
    public KotoApiOptions Map(Func<Error, int?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Insert(0, rule);
        return this;
    }

    /// <summary>Resolves the HTTP status code for <paramref name="error"/>.</summary>
    public int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (_exact.TryGetValue(error.Code, out var exact))
            return exact;

        foreach (var rule in _rules)
        {
            if (rule(error) is { } fromRule)
                return fromRule;
        }

        var code = error.Code;
        if (code.EndsWith(".not-found", StringComparison.Ordinal)) return StatusCodes.Status404NotFound;
        if (code.Contains(".already-", StringComparison.Ordinal) || code.EndsWith(".conflict", StringComparison.Ordinal)) return StatusCodes.Status409Conflict;
        if (code.EndsWith(".unauthorized", StringComparison.Ordinal)) return StatusCodes.Status401Unauthorized;
        if (code.EndsWith(".forbidden", StringComparison.Ordinal)) return StatusCodes.Status403Forbidden;
        if (code.StartsWith("general.", StringComparison.Ordinal)
            || code.StartsWith("validation.", StringComparison.Ordinal)
            || error is EnrichedError { Field: not null })
        {
            return StatusCodes.Status400BadRequest;
        }

        return FallbackStatusCode;
    }

    /// <summary>Resolves the status for a failure with several errors: the first error decides.</summary>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public int StatusCodeFor(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("At least one error is required.", nameof(errors));
        return StatusCodeFor(errors[0]);
    }
}
