namespace Koto.Domain;

/// <summary>Shared error catalog, organized by area. Create entries with <c>new</c>: <c>new Errors.General.NotFound("order", id)</c>.</summary>
public static class Errors
{
    /// <summary>General-purpose errors applicable across all domains.</summary>
    public static class General
    {
        /// <summary>A required value was not provided.</summary>
        public sealed record ValueIsRequired()
            : Error("general.value.is-required", "A value is required.");

        /// <summary>A value's length is outside the allowed range.</summary>
        /// <param name="Min">Minimum allowed length.</param>
        /// <param name="Max">Maximum allowed length.</param>
        public sealed record InvalidLength(int Min, int Max)
            : Error("general.value.invalid-length", $"Length must be between {Min} and {Max}.");

        /// <summary>An entity or resource could not be found.</summary>
        /// <param name="Entity">Name of the entity type or resource.</param>
        /// <param name="Id">Optional identifier of the missing resource.</param>
        public sealed record NotFound(string Entity, object? Id = null)
            : Error("general.entity.not-found", Id is null
                ? $"'{Entity}' was not found."
                : $"'{Entity}' with ID '{Id}' was not found.");

        /// <summary>A collection contains fewer items than required.</summary>
        /// <param name="Min">Minimum number of items.</param>
        /// <param name="Actual">Actual number of items.</param>
        public sealed record CollectionIsTooSmall(int Min, int Actual)
            : Error("general.collection.too-small", $"Collection must have at least {Min} items, but has {Actual}.");

        /// <summary>A collection contains more items than allowed.</summary>
        /// <param name="Max">Maximum number of items.</param>
        /// <param name="Actual">Actual number of items.</param>
        public sealed record CollectionIsTooLarge(int Max, int Actual)
            : Error("general.collection.too-large", $"Collection must have at most {Max} items, but has {Actual}.");
    }
}
