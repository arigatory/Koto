using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.Middleware;

/// <summary>
/// ASP.NET Core middleware that propagates the <c>X-Correlation-ID</c> header.
/// Reads the header from the incoming request (or generates a new GUID if absent),
/// stores it in <see cref="CorrelationContext.Current"/>, and echoes it in the response header.
/// Register via <c>app.UseMiddleware&lt;CorrelationIdMiddleware&gt;()</c> or <c>app.UseKotoApi()</c>.
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    private const string HeaderName = "X-Correlation-ID";

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        CorrelationContext.Current.Value = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
