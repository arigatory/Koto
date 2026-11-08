using Koto.Domain;

namespace Koto.Infrastructure.Http;

/// <summary>Errors for HTTP responses of other services that do not carry a Koto envelope.</summary>
public static class HttpErrors
{
    /// <summary>The remote resource was not found (404).</summary>
    public sealed record ResourceNotFound() : Error("general.resource.not-found", "The requested resource was not found.");

    /// <summary>The remote service reported a conflict (409).</summary>
    public sealed record ResourceConflict() : Error("general.resource.conflict", "A conflict occurred.");

    /// <summary>The remote service rejected the request (400/422); the message is the response body when present.</summary>
    /// <param name="Message">The remote message.</param>
    public sealed record RequestInvalid(string Message) : Error("general.request.invalid", Message);

    /// <summary>The remote service failed (5xx).</summary>
    /// <param name="StatusCode">The HTTP status code.</param>
    public sealed record ServerUnexpected(int StatusCode) : Error("general.server.unexpected", "The remote service failed unexpectedly.");

    /// <summary>The remote service reported success but sent no result where one was expected.</summary>
    public sealed record ResponseEmpty() : Error("general.response.empty", "The remote service returned an empty result.");

    /// <summary>Any other non-success HTTP status.</summary>
    /// <param name="StatusCode">The HTTP status code.</param>
    public sealed record HttpFailure(int StatusCode) : Error("general.server.http-error", $"The remote service responded with HTTP {StatusCode}.");
}
