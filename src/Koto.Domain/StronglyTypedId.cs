namespace Koto.Domain;

/// <summary>
/// Base record for strongly-typed identifiers. Derive to create a dedicated ID type:
/// <c>public sealed record OrderId(Guid Value) : StronglyTypedId&lt;Guid&gt;(Value);</c>
/// </summary>
/// <typeparam name="T">The underlying primitive type (e.g. <see cref="Guid"/>, <see cref="int"/>).</typeparam>
public abstract record StronglyTypedId<T>(T Value) : IComparable<StronglyTypedId<T>>
    where T : notnull, IComparable<T>
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> is a different identifier type. Ordering across distinct
    /// ID types (e.g. <c>OrderId</c> vs <c>CustomerId</c>) is undefined and always a bug.
    /// </exception>
    public int CompareTo(StronglyTypedId<T>? other)
    {
        if (other is null) return 1;
        if (other.GetType() != GetType())
            throw new ArgumentException(
                $"Cannot compare {GetType().Name} with {other.GetType().Name}.", nameof(other));
        return Value.CompareTo(other.Value);
    }

    /// <summary>Returns the string representation of the underlying <see cref="Value"/>.</summary>
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
