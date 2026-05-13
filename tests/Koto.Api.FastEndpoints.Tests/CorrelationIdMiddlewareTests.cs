using AwesomeAssertions;
using Koto.Api.FastEndpoints.Middleware;
using Microsoft.AspNetCore.Http;

namespace Koto.Api.FastEndpoints.Tests;

public class CorrelationIdMiddlewareTests
{
    private const string Header = "X-Correlation-ID";

    // AsyncLocal flows DOWN into the pipeline, not back to the awaiting test.
    // Read CorrelationContext.Current.Value from inside the RequestDelegate.

    [Fact]
    public async Task Reads_correlation_id_from_request_header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[Header] = "corr-abc";
        var middleware = new CorrelationIdMiddleware();
        string? captured = null;

        await middleware.InvokeAsync(ctx, _ =>
        {
            captured = CorrelationContext.Current.Value;
            return Task.CompletedTask;
        });

        captured.Should().Be("corr-abc");
    }

    [Fact]
    public async Task Generates_correlation_id_when_header_absent()
    {
        var ctx = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware();
        string? captured = null;

        await middleware.InvokeAsync(ctx, _ =>
        {
            captured = CorrelationContext.Current.Value;
            return Task.CompletedTask;
        });

        captured.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Echoes_correlation_id_in_response_header()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[Header] = "corr-xyz";
        var middleware = new CorrelationIdMiddleware();

        await middleware.InvokeAsync(ctx, _ => Task.CompletedTask);

        ctx.Response.Headers[Header].ToString().Should().Be("corr-xyz");
    }

    [Fact]
    public async Task Generated_id_matches_response_header()
    {
        var ctx = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware();
        string? capturedFromContext = null;

        await middleware.InvokeAsync(ctx, _ =>
        {
            capturedFromContext = CorrelationContext.Current.Value;
            return Task.CompletedTask;
        });

        var responseHeader = ctx.Response.Headers[Header].ToString();
        responseHeader.Should().NotBeNullOrEmpty();
        responseHeader.Should().Be(capturedFromContext);
    }
}
