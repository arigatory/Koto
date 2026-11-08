using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// The one response shape for every transport (HTTP, SignalR, Kafka reply):
/// <c>{ success, result, errors, correlationId, timestamp }</c>. <c>success</c> is the discriminator
/// clients branch on. The HTTP layers send failures as <c>Envelope&lt;object?&gt;</c>, so <c>result</c> is
/// <c>null</c> whatever the result type; no custom JSON converter is involved, which keeps the OpenAPI
/// schema of <c>Envelope&lt;T&gt;</c> truthful.
/// </summary>
/// <typeparam name="T">The result type (<see cref="object"/> for void results and failures).</typeparam>
/// <param name="Success"><c>true</c> when the operation succeeded.</param>
/// <param name="Result">The value on success; <c>null</c> on failure.</param>
/// <param name="Errors">All errors on failure; empty on success.</param>
/// <param name="CorrelationId">Correlation identifier of the request, when known.</param>
/// <param name="Timestamp">UTC time the response was produced.</param>
public sealed record Envelope<T>(
    bool Success,
    T? Result,
    IReadOnlyList<EnvelopeError> Errors,
    string? CorrelationId,
    DateTimeOffset Timestamp);

/// <summary>Factories for <see cref="Envelope{T}"/>.</summary>
public static class Envelope
{
    private static readonly IReadOnlyList<EnvelopeError> NoErrors = [];

    /// <summary>A successful envelope carrying <paramref name="result"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="result"/> is <see cref="Unit"/> — void results go through <c>AppResult&lt;Unit&gt;.ToEnvelope()</c>.</exception>
    public static Envelope<T> Ok<T>(T result, DateTimeOffset timestamp, string? correlationId = null)
    {
        if (result is Unit)
            throw new ArgumentException("Void results are sent with AppResult<Unit>.ToEnvelope(), whose result is null.", nameof(result));
        return new(true, result, NoErrors, correlationId, timestamp);
    }

    /// <summary>A failed envelope; its result is <c>null</c>. Used for every failure on HTTP (void commands, binding errors, unhandled exceptions included).</summary>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static Envelope<object?> Fail(IReadOnlyList<EnrichedError> errors, DateTimeOffset timestamp, string? correlationId = null) =>
        new(false, null, ToWire(errors), correlationId, timestamp);

    /// <summary>
    /// Converts an application result into a typed envelope — for typed transports such as SignalR hub
    /// methods or Kafka replies. On failure <c>result</c> is <c>default</c>: <c>null</c> for reference types;
    /// a struct result (e.g. <see cref="Guid"/>) serializes as its default value there, so prefer reference
    /// DTOs for typed-transport results. HTTP never uses this path for failures.
    /// </summary>
    public static Envelope<T> ToEnvelope<T>(this AppResult<T> result, DateTimeOffset timestamp, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? Ok(result.Value, timestamp, correlationId)
            : new Envelope<T>(false, default, ToWire(result.Errors), correlationId, timestamp);
    }

    /// <summary>Converts a void application result into an envelope whose result is <c>null</c>.</summary>
    public static Envelope<object?> ToEnvelope(this AppResult<Unit> result, DateTimeOffset timestamp, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? new Envelope<object?>(true, null, NoErrors, correlationId, timestamp)
            : Fail(result.Errors, timestamp, correlationId);
    }

    private static IReadOnlyList<EnvelopeError> ToWire(IReadOnlyList<EnrichedError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("A failed envelope requires at least one error.", nameof(errors));
        return errors.Select(EnvelopeError.From).ToArray();
    }
}
