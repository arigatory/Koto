using AwesomeAssertions;
using Koto.Infrastructure.Http;

namespace Koto.Infrastructure.Http.Tests;

public sealed class ServiceKeyHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task Adds_service_key_header_to_every_request()
    {
        var inner = new CapturingHandler();
        using var client = new HttpClient(
            new ServiceKeyHandler(ServiceAuthDefaults.HeaderName, "s2s-key") { InnerHandler = inner });

        await client.GetAsync(new Uri("http://wallet/internal/holds"));

        inner.LastRequest!.Headers.GetValues(ServiceAuthDefaults.HeaderName)
            .Should().ContainSingle().Which.Should().Be("s2s-key");
    }
}
