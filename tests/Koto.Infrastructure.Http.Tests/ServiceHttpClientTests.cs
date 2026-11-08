using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
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

    public Task<Result<WeatherDto>> ReadAsync(HttpResponseMessage response, CancellationToken ct = default) =>
        ReadResultAsync<WeatherDto>(response, ct);

    public Task<Result<Unit>> ReadUnitAsync(HttpResponseMessage response, CancellationToken ct = default) =>
        ReadResultAsync<Unit>(response, ct);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public sealed class ServiceHttpClientTests
{
    private static HttpClient DummyClient() =>
        new() { BaseAddress = new Uri("https://example.test") };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Bare_dto_success_response_is_deserialized()
    {
        var client = new WeatherClient(DummyClient());

        var result = await client.ReadAsync(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new WeatherDto("London", 22.5)),
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.City.Should().Be("London");
    }

    [Fact]
    public async Task Envelope_success_returns_result()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":true,"result":{"city":"Oslo","temperature":3.5},"errors":[],"correlationId":"c","timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadAsync(Json(HttpStatusCode.OK, body));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new WeatherDto("Oslo", 3.5));
    }

    [Fact]
    public async Task Envelope_void_success_returns_unit()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":true,"result":null,"errors":[],"correlationId":null,"timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadUnitAsync(Json(HttpStatusCode.OK, body));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Envelope_failure_maps_to_remote_errors_with_details()
    {
        var client = new WeatherClient(DummyClient());
        var body = """
            {"success":false,"result":null,
             "errors":[{"code":"identity.otp.too-many-attempts","message":"Too many attempts.","field":null,"details":{"retryAfterSeconds":42}},
                       {"code":"general.value.is-required","message":"Required.","field":"email","details":{}}],
             "correlationId":"c","timestamp":"2026-11-08T02:11:03+00:00"}
            """;

        var result = await client.ReadAsync(Json(HttpStatusCode.UnprocessableEntity, body));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        var first = result.FirstError.Should().BeOfType<RemoteError>().Subject;
        first.Code.Should().Be("identity.otp.too-many-attempts");
        first.Message.Should().Be("Too many attempts.");
        first.Field.Should().BeNull();
        ((JsonElement)first.Details["retryAfterSeconds"]!).GetInt32().Should().Be(42);
        result.Errors[1].Should().BeOfType<RemoteError>().Which.Field.Should().Be("email");
    }

    [Fact]
    public async Task Remote_error_keeps_field_and_details_when_enriched_again()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":false,"result":null,"errors":[{"code":"identity.otp.too-many-attempts","message":"m","field":"code","details":{"retryAfterSeconds":42}}],"correlationId":null,"timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadAsync(Json(HttpStatusCode.UnprocessableEntity, body));
        var enriched = result.FirstError.Enrich();

        enriched.Should().BeOfType<RemoteError>();
        enriched.Field.Should().Be("code");
        ((JsonElement)ErrorDetails.Of(enriched)["retryAfterSeconds"]!).GetInt32().Should().Be(42);
        EnvelopeError.From(enriched).Details.Should().ContainKey("retryAfterSeconds");
    }

    [Fact]
    public async Task Envelope_with_null_result_for_a_non_unit_type_is_a_failure_not_an_exception()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":true,"result":null,"errors":[],"correlationId":null,"timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadAsync(Json(HttpStatusCode.OK, body));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().BeOfType<HttpErrors.ResponseEmpty>();
    }

    [Fact]
    public async Task Remote_error_without_details_defaults_to_empty()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":false,"result":null,"errors":[{"code":"identity.otp.expired","message":"m","field":null,"details":null}],"correlationId":null,"timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadAsync(Json(HttpStatusCode.UnprocessableEntity, body));

        result.FirstError.Should().BeOfType<RemoteError>().Which.Details.Should().BeEmpty();
    }

    [Fact]
    public async Task Envelope_error_with_malformed_code_is_kept_as_unknown_remote_error()
    {
        var client = new WeatherClient(DummyClient());
        var body = """{"success":false,"result":null,"errors":[{"code":"Weird Code","message":"m","field":null,"details":{}}],"correlationId":null,"timestamp":"2026-11-08T02:11:03+00:00"}""";

        var result = await client.ReadAsync(Json(HttpStatusCode.BadRequest, body));

        var error = result.FirstError.Should().BeOfType<RemoteError>().Subject;
        error.Code.Should().Be("general.remote.unknown-error");
        error.Details["remoteCode"].Should().Be("Weird Code");
    }

    [Fact]
    public async Task Non_envelope_404_maps_to_resource_not_found()
    {
        var client = new WeatherClient(DummyClient());

        var result = await client.ReadAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("general.resource.not-found");
    }

    [Fact]
    public async Task Non_envelope_500_maps_to_server_unexpected_with_status_detail()
    {
        var client = new WeatherClient(DummyClient());

        var result = await client.ReadAsync(Json(HttpStatusCode.InternalServerError, "<html>oops</html>"));

        result.IsFailure.Should().BeTrue();
        var error = result.FirstError.Should().BeOfType<HttpErrors.ServerUnexpected>().Subject;
        error.Code.Should().Be("general.server.unexpected");
        error.StatusCode.Should().Be(500);
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
