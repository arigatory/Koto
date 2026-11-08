using Koto.Api.AspNetCore;
using Koto.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Api.FastEndpoints.Middleware;

/// <summary>
/// Catches unhandled exceptions and responds with a 500 <see cref="Envelope{T}"/> carrying
/// <see cref="ServerError"/> (exception details stay in the log, never in the response).
/// Registered by <c>AddKotoApi()</c> / <c>UseKotoApi()</c>.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Initializes a new <see cref="GlobalExceptionHandler"/>.</summary>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    /// <summary>
    /// Entry point for <c>UseExceptionHandler</c>: handles the exception captured in
    /// <see cref="IExceptionHandlerFeature"/> with the registered handler (or a default one).
    /// </summary>
    public static Task HandleAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var feature = httpContext.Features.Get<IExceptionHandlerFeature>();
        if (feature is null)
            return Task.CompletedTask;

        var handler = httpContext.RequestServices.GetService<GlobalExceptionHandler>()
            ?? new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        return handler.TryHandleAsync(httpContext, feature.Error, httpContext.RequestAborted).AsTask();
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationContext.Current.Value;
        _logger.LogError(exception, "Unhandled exception (CorrelationId: {CorrelationId})", correlationId);

        var envelope = Envelope.Fail(
            [new ServerError().Enrich()],
            httpContext.GetKotoTimeProvider().GetUtcNow(),
            correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
