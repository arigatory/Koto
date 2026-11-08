namespace Koto.Domain;

/// <summary>
/// Base record for every error. Errors form a catalog of record types: the type is the contract and
/// its properties are the data the error guarantees (they become <c>details</c> on the wire).
/// There are no inline errors — derive a record where the condition is detected:
/// <code>
/// public sealed record OtpExpired(DateTimeOffset ExpiredAt)
///     : Error("identity.otp.expired", "The code has expired.");
/// </code>
/// </summary>
/// <param name="Code">Machine-readable code with at least three kebab-case segments, e.g. <c>orders.order.not-found</c>.</param>
/// <param name="Message">Human-readable description of this occurrence.</param>
public abstract record Error(string Code, string Message)
{
    /// <summary>Machine-readable code; validated by <see cref="ErrorCode.Validate"/> on construction.</summary>
    public string Code { get; } = ErrorCode.Validate(Code);

    /// <summary>
    /// Human-readable message. May be replaced with a <c>with</c> expression (for example to prefix a row
    /// number) — the runtime type and <see cref="Code"/> are preserved.
    /// </summary>
    public string Message { get; init; } = Message ?? throw new ArgumentNullException(nameof(Message));
}
