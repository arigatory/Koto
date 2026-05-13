using AwesomeAssertions;
using Koto.Messaging.Wolverine.Middleware;
using NSubstitute;
using Wolverine;

namespace Koto.Messaging.Wolverine.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Before_sets_correlation_id_from_envelope()
    {
        var envelope = new Envelope { CorrelationId = "corr-abc" };
        var middleware = new CorrelationIdMiddleware();

        await middleware.Before(envelope);

        CorrelationContext.CorrelationId.Value.Should().Be("corr-abc");
    }

    [Fact]
    public async Task Before_sets_null_when_envelope_has_no_correlation_id()
    {
        var envelope = new Envelope { CorrelationId = null };
        var middleware = new CorrelationIdMiddleware();

        await middleware.Before(envelope);

        CorrelationContext.CorrelationId.Value.Should().BeNull();
    }
}
