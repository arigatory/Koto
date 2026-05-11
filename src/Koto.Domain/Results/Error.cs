namespace Koto.Domain;

/// <summary>
/// Represents a structured domain error with a machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>.
/// </summary>
/// <param name="Code">Dot-separated error code, e.g. <c>"orders.order.not-found"</c>.</param>
/// <param name="Message">Human-readable description of the error.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Returns a serialized representation: <c>"code::message"</c>.</summary>
    public string Serialize() => $"{Code}::{Message}";
}
