using Koto.Domain;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.ProblemDetails;

/// <summary>
/// Factory for RFC 7807 Problem Details responses derived from <see cref="Error"/>.
/// Maps error codes to HTTP status codes and includes <c>errorCode</c> and <c>correlationId</c> extensions.
/// </summary>
public static class KotoProblemDetails
{
    /// <summary>
    /// Creates an <see cref="IResult"/> representing the <paramref name="error"/> as Problem Details.
    /// </summary>
    public static IResult From(Error error, string? correlationId = null)
    {
        var status = StatusCodeFrom(error.Code);
        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = error.Code,
                ["correlationId"] = correlationId
            });
    }

    /// <summary>
    /// Maps a Koto error code to an HTTP status code.
    /// <list type="bullet">
    ///   <item><c>*.not-found</c> → 404</item>
    ///   <item><c>*.already-*</c> → 409</item>
    ///   <item><c>general.value.*</c> → 400</item>
    ///   <item>everything else → 500</item>
    /// </list>
    /// </summary>
    public static int StatusCodeFrom(string errorCode) => errorCode switch
    {
        var c when c.EndsWith(".not-found")     => StatusCodes.Status404NotFound,
        var c when c.Contains(".already-")      => StatusCodes.Status409Conflict,
        var c when c.StartsWith("general.value.") => StatusCodes.Status400BadRequest,
        _                                        => StatusCodes.Status500InternalServerError
    };
}
