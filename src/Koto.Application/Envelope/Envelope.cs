using System.Text.Json.Serialization;
using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// The one response shape for every transport (HTTP, SignalR, Kafka reply):
/// <c>{ success, result, errors, correlationId, timestamp }</c>. On failure <c>result</c> is
/// serialized as <c>null</c>; <c>success</c> is the discriminator clients branch on.
/// </summary>
/// <typeparam name="T">The result type (<see cref="object"/> for void results).</typeparam>
/// <param name="Success"><c>true</c> when the operation succeeded.</param>
/// <param name="Result">The value on success; ignored (serialized as <c>null</c>) on failure.</param>
/// <param name="Errors">All errors on failure; empty on success.</param>
/// <param name="CorrelationId">Correlation identifier of the request, when known.</param>
/// <param name="Timestamp">UTC time the response was produced.</param>
[JsonConverter(typeof(EnvelopeJsonConverterFactory))]
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
    public static Envelope<T> Ok<T>(T result, DateTimeOffset timestamp, string? correlationId = null) =>
        new(true, result, NoErrors, correlationId, timestamp);

    /// <summary>A failed envelope typed as <typeparamref name="T"/> (the result is serialized as <c>null</c>).</summary>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static Envelope<T> Fail<T>(IReadOnlyList<EnrichedError> errors, DateTimeOffset timestamp, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
            throw new ArgumentException("A failed envelope requires at least one error.", nameof(errors));
        return new(false, default, errors.Select(EnvelopeError.From).ToArray(), correlationId, timestamp);
    }

    /// <summary>A failed envelope without a result type (void commands, binding errors, unhandled exceptions).</summary>
    public static Envelope<object?> Fail(IReadOnlyList<EnrichedError> errors, DateTimeOffset timestamp, string? correlationId = null) =>
        Fail<object?>(errors, timestamp, correlationId);

    /// <summary>Converts an application result into its envelope.</summary>
    public static Envelope<T> ToEnvelope<T>(this AppResult<T> result, DateTimeOffset timestamp, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? Ok(result.Value, timestamp, correlationId)
            : Fail<T>(result.Errors, timestamp, correlationId);
    }

    /// <summary>Converts a void application result into an envelope whose result is <c>null</c>.</summary>
    public static Envelope<object?> ToEnvelope(this AppResult<Unit> result, DateTimeOffset timestamp, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? Ok<object?>(null, timestamp, correlationId)
            : Fail(result.Errors, timestamp, correlationId);
    }
}
