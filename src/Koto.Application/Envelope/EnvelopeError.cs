namespace Koto.Application;

/// <summary>One error as it travels on the wire: code, message, the related field and flattened details.</summary>
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
        return new(error.Code, error.Message, error.Field, ErrorDetails.Of(error));
    }
}
