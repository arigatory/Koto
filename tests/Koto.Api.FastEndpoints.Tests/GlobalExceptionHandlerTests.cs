using System.Text.Json;
using AwesomeAssertions;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Api.FastEndpoints.Tests;

public class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Writes_envelope_with_server_error_and_500()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedTimeProvider.Default));
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(ctx, new InvalidOperationException("boom: secret details"), CancellationToken.None);

        handled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(500);
        ctx.Response.Body.Position = 0;
        var envelope = await JsonSerializer.DeserializeAsync<Envelope<object?>>(ctx.Response.Body, Web);
        envelope!.Success.Should().BeFalse();
        envelope.Result.Should().BeNull();
        var error = envelope.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("general.server.internal-error");
        error.Message.Should().NotContain("secret");
        envelope.Timestamp.Should().Be(FixedTimeProvider.Default);
    }
}
