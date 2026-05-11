namespace Koto.Domain;

/// <summary>
/// Base class for value objects. Equality is determined by comparing all components
/// returned by <see cref="GetEqualityComponents"/>.
/// </summary>
public abstract class ValueObject
{
    /// <summary>Returns the components used to determine equality.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        return GetEqualityComponents()
            .SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, HashCode.Combine);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
