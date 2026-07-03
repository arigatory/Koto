using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Extensible registry that maps a Koto <see cref="Error"/> to an HTTP status code.
/// Resolution order: exact code → custom rules → suffix → prefix → field-error default (400)
/// → <see cref="FallbackStatusCode"/>. User-added mappings take precedence over the built-in
/// defaults within each category.
/// </summary>
/// <remarks>
/// Built-in defaults:
/// <list type="bullet">
///   <item><c>*.not-found</c> → 404</item>
///   <item><c>*.already-*</c> / <c>*.conflict</c> → 409</item>
///   <item><c>*.unauthorized</c> → 401</item>
///   <item><c>*.forbidden</c> → 403</item>
///   <item><c>general.*</c> / <c>validation.*</c> → 400</item>
///   <item>error with <see cref="Error.Field"/> set → 400</item>
///   <item>everything else → 422 (unmapped business rule violations are client errors, never 500;
///   500 is reserved for unhandled exceptions)</item>
/// </list>
/// </remarks>
public sealed class KotoHttpErrorOptions
{
    private readonly Dictionary<string, int> _exact = new(StringComparer.Ordinal);
    private readonly List<Func<Error, int?>> _rules = [];
    private readonly List<(string Suffix, int Status)> _suffixes = [];
    private readonly List<(string Prefix, int Status)> _prefixes = [];

    /// <summary>Initializes the options with the built-in default mappings.</summary>
    public KotoHttpErrorOptions()
    {
        _suffixes.Add((".not-found", StatusCodes.Status404NotFound));
        _suffixes.Add((".conflict", StatusCodes.Status409Conflict));
        _suffixes.Add((".unauthorized", StatusCodes.Status401Unauthorized));
        _suffixes.Add((".forbidden", StatusCodes.Status403Forbidden));
        _rules.Add(e => e.Code.Contains(".already-", StringComparison.Ordinal)
            ? StatusCodes.Status409Conflict
            : null);
        _prefixes.Add(("general.", StatusCodes.Status400BadRequest));
        _prefixes.Add(("validation.", StatusCodes.Status400BadRequest));
    }

    /// <summary>
    /// Status code for business errors not matched by any rule.
    /// Defaults to 422 Unprocessable Entity — a failed <c>Result</c> is a client-visible
    /// rule violation, not a server fault; 500 is reserved for unhandled exceptions.
    /// </summary>
    public int FallbackStatusCode { get; set; } = StatusCodes.Status422UnprocessableEntity;

    /// <summary>Maps an exact error code (highest priority), e.g. <c>Map("subscription.payment-failed", 502)</c>.</summary>
    public KotoHttpErrorOptions Map(string exactCode, int statusCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(exactCode);
        _exact[exactCode] = statusCode;
        return this;
    }

    /// <summary>Maps a custom predicate rule, e.g. <c>Map(e =&gt; e.Code.Contains(".quota-") ? 429 : null)</c>.</summary>
    public KotoHttpErrorOptions Map(Func<Error, int?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Insert(0, rule);
        return this;
    }

    /// <summary>Maps error codes ending with <paramref name="codeSuffix"/>, e.g. <c>MapSuffix(".expired", 410)</c>.</summary>
    public KotoHttpErrorOptions MapSuffix(string codeSuffix, int statusCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeSuffix);
        _suffixes.Insert(0, (codeSuffix, statusCode));
        return this;
    }

    /// <summary>Maps error codes starting with <paramref name="codePrefix"/>, e.g. <c>MapPrefix("payments.", 502)</c>.</summary>
    public KotoHttpErrorOptions MapPrefix(string codePrefix, int statusCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(codePrefix);
        _prefixes.Insert(0, (codePrefix, statusCode));
        return this;
    }

    /// <summary>Resolves the HTTP status code for <paramref name="error"/>.</summary>
    public int StatusCodeFor(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (_exact.TryGetValue(error.Code, out var exact))
            return exact;

        foreach (var rule in _rules)
            if (rule(error) is { } fromRule)
                return fromRule;

        foreach (var (suffix, status) in _suffixes)
            if (error.Code.EndsWith(suffix, StringComparison.Ordinal))
                return status;

        foreach (var (prefix, status) in _prefixes)
            if (error.Code.StartsWith(prefix, StringComparison.Ordinal))
                return status;

        if (error.Field is not null)
            return StatusCodes.Status400BadRequest;

        return FallbackStatusCode;
    }
}
