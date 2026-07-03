namespace Koto.Domain;

/// <summary>
/// Represents a structured domain error with a machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>.
/// </summary>
/// <param name="Code">Dot-separated error code, e.g. <c>"orders.order.not-found"</c>.</param>
/// <param name="Message">Human-readable description of the error.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// Name of the field/property the error relates to, when applicable.
    /// Typically set by the application layer (e.g. the validation pipeline) so that
    /// HTTP responses can group errors per field; domain factories usually leave it <c>null</c>.
    /// </summary>
    public string? Field { get; init; }
}
