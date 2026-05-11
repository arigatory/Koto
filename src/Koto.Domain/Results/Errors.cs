namespace Koto.Domain;

/// <summary>Shared error factories, organized by domain area.</summary>
public static class Errors
{
    /// <summary>General-purpose errors applicable across all domains.</summary>
    public static class General
    {
        /// <summary>A required value was not provided.</summary>
        public static Error ValueIsRequired() =>
            new("general.value.is-required", "A value is required.");

        /// <summary>A value's length is outside the allowed range.</summary>
        public static Error InvalidLength(int min, int max) =>
            new("general.invalid-length", $"Length must be between {min} and {max}.");

        /// <summary>An entity or resource could not be found.</summary>
        /// <param name="field">The name of the field or entity type.</param>
        /// <param name="id">Optional identifier of the missing resource.</param>
        public static Error NotFound(string field, object? id = null) =>
            new("general.not-found", id is null
                ? $"'{field}' was not found."
                : $"'{field}' with ID '{id}' was not found.");

        /// <summary>A collection contains fewer items than required.</summary>
        public static Error CollectionIsTooSmall(int min, int actual) =>
            new("general.collection-is-too-small",
                $"Collection must have at least {min} items, but has {actual}.");

        /// <summary>A collection contains more items than allowed.</summary>
        public static Error CollectionIsTooLarge(int max, int actual) =>
            new("general.collection-is-too-large",
                $"Collection must have at most {max} items, but has {actual}.");
    }
}
