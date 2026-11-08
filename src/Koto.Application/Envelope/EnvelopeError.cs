using System.Text.Json;

namespace Koto.Application;

/// <summary>
/// One error as it travels on the wire: code, message, the related field and flattened details.
/// <c>Field</c> is camelCased per path segment (<c>Items[0].Name</c> → <c>items[0].name</c>) so it matches
/// the JSON property names clients sent, whichever layer bound the error to the field.
/// </summary>
/// <param name="Code">Machine-readable code (<c>context.entity.problem</c>).</param>
/// <param name="Message">Human-readable message.</param>
/// <param name="Field">Request field the error relates to, or <c>null</c>.</param>
/// <param name="Details">Flattened data of the error (camelCase keys); empty when the error carries none.</param>
public sealed record EnvelopeError(
    string Code,
    string Message,
    string? Field,
    IReadOnlyDictionary<string, object?> Details)
{
    /// <summary>Flattens <paramref name="error"/> (see <see cref="ErrorDetails"/>).</summary>
    public static EnvelopeError From(EnrichedError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error.Code, error.Message, ToWireField(error.Field), ErrorDetails.Of(error));
    }

    private static string? ToWireField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return null;
        var segments = field.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var bracket = segments[i].IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? segments[i] : segments[i][..bracket];
            var suffix = bracket < 0 ? string.Empty : segments[i][bracket..];
            segments[i] = JsonNamingPolicy.CamelCase.ConvertName(name) + suffix;
        }

        return string.Join('.', segments);
    }
}
