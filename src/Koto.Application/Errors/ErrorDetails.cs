using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Flattens an error into its wire <c>details</c>: the public properties of every record in the chain
/// (camelCase keys) plus <see cref="EnrichedError.Details"/>. Service properties (<c>Code</c>,
/// <c>Message</c>, <c>Field</c>, <c>Details</c>, <c>Inner</c>) are excluded; properties holding an
/// <see cref="Error"/> are flattened recursively. Outer properties override inner ones, and the explicit
/// details dictionary overrides both.
/// </summary>
public static class ErrorDetails
{
    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
    {
        nameof(Error.Code), nameof(Error.Message), nameof(EnrichedError.Field),
        nameof(EnrichedError.Details), nameof(EnrichedError.Inner), "EqualityContract",
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>Returns the flattened details of <paramref name="error"/> (empty when it carries no data).</summary>
    public static IReadOnlyDictionary<string, object?> Of(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var details = new Dictionary<string, object?>(StringComparer.Ordinal);
        Collect(error, details);
        return details;
    }

    private static void Collect(Error error, Dictionary<string, object?> details)
    {
        var enriched = error as EnrichedError;
        if (enriched is not null)
            Collect(enriched.Inner, details);

        var nested = new List<Error>();
        var scalars = new List<(string Key, object? Value)>();
        foreach (var property in PropertiesOf(error.GetType()))
        {
            var value = property.GetValue(error);
            if (value is Error nestedError)
            {
                if (enriched is null || !ReferenceEquals(nestedError, enriched.Inner))
                    nested.Add(nestedError);
            }
            else
            {
                scalars.Add((JsonNamingPolicy.CamelCase.ConvertName(property.Name), value));
            }
        }

        foreach (var nestedError in nested)
            Collect(nestedError, details);
        foreach (var (key, value) in scalars)
            details[key] = value;
        if (enriched is not null)
        {
            foreach (var (key, value) in enriched.Details)
                details[key] = value;
        }
    }

    private static PropertyInfo[] PropertiesOf(Type type) =>
        PropertyCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !Excluded.Contains(p.Name))
            .ToArray());
}
