using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// An <see cref="Error"/> enriched by the application layer with request context: the
/// <see cref="Field"/> it relates to and optional <see cref="Details"/>. The original error stays
/// available as <see cref="Inner"/>; <see cref="Error.Code"/> and <see cref="Error.Message"/> are copied from it.
/// <para>
/// Guaranteed data belongs in record properties, not in <see cref="Details"/>: derive a record to add
/// typed data on top of a domain error —
/// <c>sealed record MeetupFullWithAlternative(MeetupFull Cause, Guid AlternativeMeetupId) : EnrichedError(Cause);</c>
/// </para>
/// </summary>
public record EnrichedError : Error
{
    private static readonly IReadOnlyDictionary<string, object?> NoDetails =
        new Dictionary<string, object?>(0, StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, object?> _details = NoDetails;

    /// <summary>Wraps <paramref name="inner"/>. Wrapping an already enriched error keeps its field and details instead of nesting.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <c>null</c>.</exception>
    public EnrichedError(Error inner) : base(Unwrap(inner).Code, inner.Message)
    {
        if (inner is EnrichedError enriched)
        {
            Inner = enriched.Inner;
            Field = enriched.Field;
            _details = enriched._details;
        }
        else
        {
            Inner = inner;
        }
    }

    /// <summary>The original error this one enriches (never an <see cref="EnrichedError"/> itself).</summary>
    public Error Inner { get; }

    /// <summary>Name of the request field/property the error relates to; <c>null</c> for errors not tied to a field.</summary>
    public string? Field { get; init; }

    /// <summary>
    /// Optional, non-contractual additions (clients treat them as optional). Keys are ordinal strings;
    /// values should be scalars or arrays of scalars.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Details
    {
        get => _details;
        init => _details = value ?? throw new ArgumentNullException(nameof(Details));
    }

    /// <summary>Structural equality: same runtime type, code, message, inner error, field and details.</summary>
    public virtual bool Equals(EnrichedError? other) =>
        other is not null
        && EqualityContract == other.EqualityContract
        && Code == other.Code
        && Message == other.Message
        && Inner.Equals(other.Inner)
        && Field == other.Field
        && DetailsEqual(_details, other._details);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        hash.Add(Code, StringComparer.Ordinal);
        hash.Add(Inner);
        hash.Add(Field, StringComparer.Ordinal);
        hash.Add(_details.Count);
        return hash.ToHashCode();
    }

    private static Error Unwrap(Error inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        return inner;
    }

    private static bool DetailsEqual(IReadOnlyDictionary<string, object?> left, IReadOnlyDictionary<string, object?> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
                return false;
        }

        return true;
    }
}
