namespace Koto.Domain;

/// <summary>
/// Base class for domain entities. Two entities are equal when they have the same
/// runtime type and the same <see cref="Id"/>. Transient entities (no identifier
/// assigned yet) are never equal to each other — identity-based equality is only
/// meaningful once an identity exists.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    /// <summary>The entity identifier.</summary>
    public TId Id { get; protected set; }

    /// <summary>
    /// <c>true</c> while the entity has no identifier yet (default value, e.g.
    /// <c>Guid.Empty</c> or <c>0</c> before the database assigns a key).
    /// </summary>
    /// <remarks>
    /// Note that <see cref="GetHashCode"/> is Id-based: an entity's hash changes when a
    /// transient entity later receives its identifier, so do not store transient entities
    /// in hash-based collections across the persistence boundary.
    /// </remarks>
    public bool IsTransient => EqualityComparer<TId>.Default.Equals(Id, default!);

    /// <summary>Initializes a new entity with the given <paramref name="id"/>.</summary>
    protected Entity(TId id) => Id = id;

    /// <summary>Parameterless constructor for ORM use.</summary>
    protected Entity() { Id = default!; }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (IsTransient || other.IsTransient) return false;
        return Id.Equals(other.Id);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
