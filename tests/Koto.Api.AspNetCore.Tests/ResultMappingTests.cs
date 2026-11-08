using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Api.AspNetCore.Tests;

public class ResultMappingTests
{
    private sealed class TestController : ControllerBase { }

    private static readonly EnrichedError NotFound = new TestError("orders.order.not-found", "nope").Enrich();

    private static HttpContext CreateHttpContext(Action<KotoApiOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedTimeProvider.Default));
        services.AddKotoAspNetCore(configure);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static TestController CreateController(Action<KotoApiOptions>? configure = null) =>
        new() { ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(configure) } };

    // ── Minimal API: ToHttpResult ──────────────────────────────────────────────

    [Fact]
    public void ToHttpResult_success_returns_200_with_envelope()
    {
        var result = AppResult<string>.Success("hello").ToHttpResult(CreateHttpContext(), "corr-1");

        var json = result.Should().BeOfType<JsonHttpResult<Envelope<string>>>().Which;
        json.StatusCode.Should().Be(200);
        json.Value!.Success.Should().BeTrue();
        json.Value.Result.Should().Be("hello");
        json.Value.Errors.Should().BeEmpty();
        json.Value.CorrelationId.Should().Be("corr-1");
        json.Value.Timestamp.Should().Be(FixedTimeProvider.Default);
    }

    [Fact]
    public void ToHttpResult_unit_success_returns_200_with_null_result()
    {
        var result = AppResult.Success().ToHttpResult(CreateHttpContext());

        var json = result.Should().BeOfType<JsonHttpResult<Envelope<object?>>>().Which;
        json.StatusCode.Should().Be(200);
        json.Value!.Success.Should().BeTrue();
        json.Value.Result.Should().BeNull();
    }

    [Fact]
    public void ToHttpResult_failure_uses_status_from_options_and_lists_errors()
    {
        var result = AppResult<string>.Failure(NotFound).ToHttpResult(CreateHttpContext());

        // failures travel as Envelope<object?> (result: null for every T); Envelope<T> stays the declared 200 type
        var json = result.Should().BeOfType<JsonHttpResult<Envelope<object?>>>().Which;
        json.StatusCode.Should().Be(404);
        json.Value!.Success.Should().BeFalse();
        json.Value.Errors.Should().ContainSingle().Which.Code.Should().Be("orders.order.not-found");
    }

    [Fact]
    public void ToHttpResult_uses_configured_options()
    {
        var httpContext = CreateHttpContext(o => o.Map("subscription.payment.failed", 502));

        var result = AppResult<string>.Failure(new TestError("subscription.payment.failed", "gateway down").Enrich())
            .ToHttpResult(httpContext);

        result.Should().BeOfType<JsonHttpResult<Envelope<object?>>>().Which.StatusCode.Should().Be(502);
    }

    [Fact]
    public void ToHttpResult_works_without_registered_services()
    {
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };

        var result = AppResult<string>.Failure(NotFound).ToHttpResult(httpContext);

        var json = result.Should().BeOfType<JsonHttpResult<Envelope<object?>>>().Which;
        json.StatusCode.Should().Be(404);
        json.Value!.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ToHttpResultAsync_maps_pending_dispatch()
    {
        var pending = Task.FromResult(AppResult<int>.Success(42));

        var result = await pending.ToHttpResultAsync(CreateHttpContext());

        result.Should().BeOfType<JsonHttpResult<Envelope<int>>>().Which.Value!.Result.Should().Be(42);
    }

    // ── MVC: ToActionResult ────────────────────────────────────────────────────

    [Fact]
    public void ToActionResult_success_returns_envelope_value()
    {
        var actionResult = AppResult<string>.Success("hello").ToActionResult(CreateController());

        actionResult.Value.Should().NotBeNull();
        actionResult.Value!.Success.Should().BeTrue();
        actionResult.Value.Result.Should().Be("hello");
    }

    [Fact]
    public void ToActionResult_unit_success_returns_envelope_with_null_result()
    {
        var actionResult = AppResult.Success().ToActionResult(CreateController());

        actionResult.Value!.Success.Should().BeTrue();
        actionResult.Value.Result.Should().BeNull();
    }

    [Fact]
    public void ToActionResult_failure_returns_object_result_with_status_and_envelope()
    {
        var actionResult = AppResult<string>.Failure(NotFound).ToActionResult(CreateController(), "corr-2");

        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(404);
        var envelope = objectResult.Value.Should().BeOfType<Envelope<object?>>().Which;
        envelope.Success.Should().BeFalse();
        envelope.Errors.Should().ContainSingle().Which.Code.Should().Be("orders.order.not-found");
        envelope.CorrelationId.Should().Be("corr-2");
    }

    [Fact]
    public async Task ToActionResultAsync_maps_pending_dispatch()
    {
        var pending = Task.FromResult(AppResult<int>.Success(7));

        var actionResult = await pending.ToActionResultAsync(CreateController());

        actionResult.Value!.Result.Should().Be(7);
    }
}
