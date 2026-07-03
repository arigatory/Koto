using AwesomeAssertions;
using Koto.Api.AspNetCore;
using Koto.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Api.AspNetCore.Tests;

public class ResultMappingTests
{
    private sealed class TestController : ControllerBase { }

    private static HttpContext CreateHttpContext(Action<KotoHttpErrorOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddKotoAspNetCore(configure);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static TestController CreateController(Action<KotoHttpErrorOptions>? configure = null) =>
        new() { ControllerContext = new ControllerContext { HttpContext = CreateHttpContext(configure) } };

    // ── Minimal API: ToHttpResult ──────────────────────────────────────────────

    [Fact]
    public void ToHttpResult_success_returns_200_with_value()
    {
        var httpContext = CreateHttpContext();

        var result = Result<string>.Success("hello").ToHttpResult(httpContext);

        result.Should().BeOfType<Ok<string>>().Which.Value.Should().Be("hello");
    }

    [Fact]
    public void ToHttpResult_unit_success_returns_204()
    {
        var result = Result.Success().ToHttpResult(CreateHttpContext());

        result.Should().BeOfType<NoContent>();
    }

    [Fact]
    public void ToHttpResult_failure_returns_problem_with_mapped_status()
    {
        var result = Result<string>.Failure(new Error("orders.order.not-found", "nope"))
            .ToHttpResult(CreateHttpContext());

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public void ToHttpResult_uses_configured_options()
    {
        var httpContext = CreateHttpContext(o => o.Map("subscription.payment-failed", 502));

        var result = Result<string>.Failure(new Error("subscription.payment-failed", "gateway down"))
            .ToHttpResult(httpContext);

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(502);
    }

    [Fact]
    public void ToHttpResult_works_without_registered_options()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = Result<string>.Failure(new Error("x.not-found", "nope")).ToHttpResult(httpContext);

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ToHttpResultAsync_maps_pending_dispatch()
    {
        var pending = Task.FromResult(Result<int>.Success(42));

        var result = await pending.ToHttpResultAsync(CreateHttpContext());

        result.Should().BeOfType<Ok<int>>().Which.Value.Should().Be(42);
    }

    // ── MVC: ToActionResult ────────────────────────────────────────────────────

    [Fact]
    public void ToActionResult_success_returns_ok_with_value()
    {
        var controller = CreateController();

        var actionResult = Result<string>.Success("hello").ToActionResult(controller);

        actionResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be("hello");
    }

    [Fact]
    public void ToActionResult_unit_success_returns_204()
    {
        var controller = CreateController();

        var actionResult = Result.Success().ToActionResult(controller);

        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void ToActionResult_failure_returns_problem_details_with_error_code()
    {
        var controller = CreateController();

        var actionResult = Result<string>.Failure(new Error("orders.order.not-found", "nope"))
            .ToActionResult(controller);

        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(404);
        var problem = objectResult.Value.Should().BeOfType<Microsoft.AspNetCore.Mvc.ProblemDetails>().Which;
        problem.Detail.Should().Be("nope");
        problem.Extensions["errorCode"].Should().Be("orders.order.not-found");
    }

    [Fact]
    public void ToActionResult_multi_error_failure_returns_validation_problem_details()
    {
        var controller = CreateController();
        var errors = new[]
        {
            new Error("general.value.is-required", "Email is required") { Field = "Email" },
            new Error("general.value.is-required", "Name is required") { Field = "Name" },
        };

        var actionResult = Result<string>.Failure(errors).ToActionResult(controller);

        var objectResult = actionResult.Result.Should().BeOfType<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(400);
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Which;
        problem.Errors.Should().ContainKeys("Email", "Name");
        problem.Extensions["errorCodes"].Should().BeEquivalentTo(
            new[] { "general.value.is-required", "general.value.is-required" });
    }

    [Fact]
    public async Task ToActionResultAsync_maps_pending_dispatch()
    {
        var controller = CreateController();
        var pending = Task.FromResult(Result<int>.Success(7));

        var actionResult = await pending.ToActionResultAsync(controller);

        actionResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(7);
    }
}
