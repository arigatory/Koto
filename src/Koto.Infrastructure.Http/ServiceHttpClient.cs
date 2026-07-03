using System.Net;
using System.Net.Http.Json;
using Koto.Domain;

namespace Koto.Infrastructure.Http;

/// <summary>
/// Abstract base for typed HTTP clients that call other services.
/// Inherit and inject <see cref="HttpClient"/> to implement <c>IXxxService</c> interfaces
/// defined in the Application layer.
/// </summary>
public abstract class ServiceHttpClient
{
    /// <summary>The underlying <see cref="HttpClient"/> configured via DI.</summary>
    protected HttpClient Http { get; }

    /// <summary>Initializes a new <see cref="ServiceHttpClient"/>.</summary>
    protected ServiceHttpClient(HttpClient http)
    {
        Http = http;
    }

    /// <summary>
    /// Deserializes a successful response body to <typeparamref name="T"/>, or
    /// maps an error status code to a failure <see cref="Result{T}"/>.
    /// </summary>
    protected async Task<Result<T>> ReadResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
            return value!;
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return MapErrorResponse(response, body);
    }

    /// <summary>
    /// Maps a non-success HTTP response to a domain <see cref="Error"/>.
    /// Override to add service-specific error mapping.
    /// </summary>
    protected virtual Error MapErrorResponse(HttpResponseMessage response, string? body) =>
        response.StatusCode switch
        {
            HttpStatusCode.NotFound =>
                Errors.General.NotFound("resource"),
            HttpStatusCode.Conflict =>
                new Error("general.conflict", "A conflict occurred."),
            HttpStatusCode.UnprocessableEntity =>
                new Error("general.validation", body ?? "Validation failed."),
            var s when (int)s >= 500 =>
                new Error("general.unexpected", "An unexpected error occurred."),
            _ =>
                new Error("general.http-error", $"HTTP {(int)response.StatusCode}.")
        };
}
