using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Koto.Api.FastEndpoints.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns a 500 Problem Details response.
/// Register via <c>services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;()</c> and <c>app.UseExceptionHandler()</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Initializes a new <see cref="GlobalExceptionHandler"/>.</summary>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationContext.Current.Value;

        _logger.LogError(exception,
            "Unhandled exception (CorrelationId: {CorrelationId})", correlationId);

        var result = TypedResults.Problem(
            title: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = correlationId
            });

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
        return true;
    }
}
