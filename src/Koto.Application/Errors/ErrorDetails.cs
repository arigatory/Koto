using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Flattens an error into its wire <c>details</c>: the public properties of every record in the chain
/// (camelCase keys) plus <see cref="EnrichedError.Details"/>. The members declared by <see cref="Error"/>
/// and <see cref="EnrichedError"/> themselves (<c>Code</c>, <c>Message</c>, <c>Inner</c>, <c>Field</c>,
/// <c>Details</c>) are not details; a property with one of those names declared on your own record is.
/// Properties holding an <see cref="Error"/> are flattened recursively (each error visited once).
/// Outer properties override inner ones, and the explicit details dictionary overrides both.
/// </summary>
public static class ErrorDetails
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>Returns the flattened details of <paramref name="error"/> (empty when it carries no data).</summary>
    public static IReadOnlyDictionary<string, object?> Of(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var details = new Dictionary<string, object?>(StringComparer.Ordinal);
        Collect(error, details, new HashSet<Error>(ReferenceEqualityComparer.Instance));
        return details;
    }

    private static void Collect(Error error, Dictionary<string, object?> details, HashSet<Error> visited)
    {
        if (!visited.Add(error))
            return;

        var enriched = error as EnrichedError;
        if (enriched is not null)
            Collect(enriched.Inner, details, visited);

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
            Collect(nestedError, details, visited);
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
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsServiceMember(p))
            .ToArray());

    private static bool IsServiceMember(PropertyInfo property) =>
        property.DeclaringType == typeof(Error)
        || property.DeclaringType == typeof(EnrichedError)
        || property.Name == "EqualityContract";
}
