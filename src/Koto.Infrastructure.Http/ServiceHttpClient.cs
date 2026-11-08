using System.Net;
using System.Text.Json;
using Koto.Application;
using Koto.Domain;

namespace Koto.Infrastructure.Http;

/// <summary>
/// Abstract base for typed HTTP clients that call other services (anti-corruption layer).
/// Inherit and inject <see cref="HttpClient"/> to implement the <c>IXxxService</c> interfaces
/// defined in the Application layer.
/// </summary>
public abstract class ServiceHttpClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The underlying <see cref="HttpClient"/> configured via DI.</summary>
    protected HttpClient Http { get; }

    /// <summary>Initializes a new <see cref="ServiceHttpClient"/>.</summary>
    protected ServiceHttpClient(HttpClient http)
    {
        Http = http;
    }

    /// <summary>
    /// Reads a response into a domain <see cref="Result{T}"/>. A Koto <see cref="Envelope{T}"/> body is
    /// unwrapped: its result on success, its errors as <see cref="RemoteError"/>s on failure. Any other
    /// body is deserialized as <typeparamref name="T"/> on a success status, or mapped through
    /// <see cref="MapErrorResponse"/> otherwise.
    /// </summary>
    protected async Task<Result<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (TryReadEnvelope<T>(body, out var envelope))
        {
            if (!envelope.Success)
                return Result<T>.Failure(envelope.Errors.Select(RemoteError.From));
            if (typeof(T) == typeof(Unit))
                return Result<T>.Success((T)(object)Unit.Value);
            return envelope.Result is null
                ? Result<T>.Failure(new HttpErrors.ResponseEmpty())
                : Result<T>.Success(envelope.Result);
        }

        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(Unit) || string.IsNullOrWhiteSpace(body))
                return Result<T>.Success((T)(object)Unit.Value);
            return Result<T>.Success(JsonSerializer.Deserialize<T>(body, Json)!);
        }

        return MapErrorResponse(response, body);
    }

    /// <summary>
    /// Maps a non-success HTTP response without a Koto envelope to an <see cref="Error"/>
    /// (see <see cref="HttpErrors"/>). Override to add service-specific mapping.
    /// </summary>
    protected virtual Error MapErrorResponse(HttpResponseMessage response, string? body)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new HttpErrors.ResourceNotFound(),
            HttpStatusCode.Conflict => new HttpErrors.ResourceConflict(),
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                new HttpErrors.RequestInvalid(string.IsNullOrWhiteSpace(body) ? "The remote service rejected the request." : body),
            var status when (int)status >= 500 => new HttpErrors.ServerUnexpected((int)status),
            var status => new HttpErrors.HttpFailure((int)status),
        };
    }

    private static bool TryReadEnvelope<T>(string body, out Envelope<T> envelope)
    {
        envelope = null!;
        if (string.IsNullOrWhiteSpace(body) || body.TrimStart()[0] != '{')
            return false;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetProperty(root, "success", out var success)
                || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !TryGetProperty(root, "errors", out var errors)
                || errors.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            envelope = JsonSerializer.Deserialize<Envelope<T>>(body, Json)!;
            return envelope is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
