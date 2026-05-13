using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Koto.Infrastructure.Http;
using NSubstitute;

namespace Koto.Infrastructure.Http.Tests;

// ---------------------------------------------------------------------------
// Test doubles
// ---------------------------------------------------------------------------

file sealed record WeatherDto(string City, double Temperature);

file sealed class WeatherClient : ServiceHttpClient
{
    public WeatherClient(HttpClient http) : base(http) { }

    public Task<Koto.Domain.Result<WeatherDto>> GetAsync(string city, CancellationToken ct = default) =>
        ReadResultAsync<WeatherDto>(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new WeatherDto(city, 22.5))
            }, ct);

    public Task<Koto.Domain.Result<WeatherDto>> GetWithErrorAsync(
        HttpStatusCode status, CancellationToken ct = default) =>
        ReadResultAsync<WeatherDto>(new HttpResponseMessage(status), ct);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class ServiceHttpClientTests
{
    private static HttpClient DummyClient() =>
        new() { BaseAddress = new Uri("https://example.test") };

    [Fact]
    public async Task ReadResultAsync_deserializes_success_response()
    {
        var client = new WeatherClient(DummyClient());
        var result = await client.GetAsync("London");

        result.IsSuccess.Should().BeTrue();
        result.Value.City.Should().Be("London");
    }

    [Fact]
    public async Task ReadResultAsync_maps_404_to_not_found_error()
    {
        var client = new WeatherClient(DummyClient());
        var result = await client.GetWithErrorAsync(HttpStatusCode.NotFound);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("general.not-found");
    }

    [Fact]
    public async Task ReadResultAsync_maps_500_to_unexpected_error()
    {
        var client = new WeatherClient(DummyClient());
        var result = await client.GetWithErrorAsync(HttpStatusCode.InternalServerError);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("general.unexpected");
    }

    [Fact]
    public async Task CorrelationIdHandler_appends_header()
    {
        var accessor = Substitute.For<ICorrelationIdAccessor>();
        accessor.GetCorrelationId().Returns("trace-abc");

        HttpRequestMessage? captured = null;
        var inner = new DelegateHandler(req =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new CorrelationIdHandler(accessor) { InnerHandler = inner };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://x.test") };

        await client.GetAsync("/ping");

        captured!.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        values!.First().Should().Be("trace-abc");
    }

    [Fact]
    public async Task CorrelationIdHandler_skips_header_when_no_correlation_id()
    {
        var accessor = Substitute.For<ICorrelationIdAccessor>();
        accessor.GetCorrelationId().Returns((string?)null);

        HttpRequestMessage? captured = null;
        var inner = new DelegateHandler(req =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var handler = new CorrelationIdHandler(accessor) { InnerHandler = inner };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://x.test") };

        await client.GetAsync("/ping");

        captured!.Headers.Contains("X-Correlation-ID").Should().BeFalse();
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) => _fn(req);
    }
}
