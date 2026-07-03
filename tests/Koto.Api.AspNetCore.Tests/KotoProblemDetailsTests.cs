using AwesomeAssertions;
using Koto.Api.AspNetCore;
using Koto.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Koto.Api.AspNetCore.Tests;

public class KotoProblemDetailsTests
{
    private static readonly KotoHttpErrorOptions Options = new();

    // ── Single error ───────────────────────────────────────────────────────────

    [Fact]
    public void Single_error_produces_problem_with_mapped_status_and_error_code()
    {
        var error = new Error("orders.order.not-found", "Order not found");

        var result = KotoProblemDetails.From(error, Options, "corr-123");

        var problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.StatusCode.Should().Be(404);
        problem.ProblemDetails.Detail.Should().Be("Order not found");
        problem.ProblemDetails.Extensions["errorCode"].Should().Be("orders.order.not-found");
        problem.ProblemDetails.Extensions["correlationId"].Should().Be("corr-123");
    }

    [Fact]
    public void Single_error_omits_correlation_and_field_when_absent()
    {
        var result = KotoProblemDetails.From(new Error("general.value.is-required", "Required"), Options);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Extensions.Should().NotContainKey("correlationId");
        problem.ProblemDetails.Extensions.Should().NotContainKey("field");
    }

    [Fact]
    public void Single_error_includes_field_when_present()
    {
        var error = new Error("general.value.is-required", "Required") { Field = "Email" };

        var result = KotoProblemDetails.From(error, Options);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.ProblemDetails.Extensions["field"].Should().Be("Email");
    }

    // ── Multiple errors ────────────────────────────────────────────────────────

    [Fact]
    public void Multiple_errors_produce_validation_problem_grouped_by_field()
    {
        var errors = new[]
        {
            new Error("general.value.is-required", "Email is required") { Field = "Email" },
            new Error("general.value.is-required", "Name is required") { Field = "Name" },
            new Error("general.invalid-length", "Name is too short") { Field = "Name" },
        };

        var result = KotoProblemDetails.From(errors, Options, "corr-9");

        var problem = result.Should().BeOfType<ValidationProblem>().Which;
        problem.ProblemDetails.Errors.Should().ContainKey("Email").WhoseValue.Should().HaveCount(1);
        problem.ProblemDetails.Errors.Should().ContainKey("Name").WhoseValue.Should().HaveCount(2);
        problem.ProblemDetails.Extensions["errorCodes"].Should().BeEquivalentTo(new[]
        {
            "general.value.is-required", "general.value.is-required", "general.invalid-length",
        });
        problem.ProblemDetails.Extensions["correlationId"].Should().Be("corr-9");
    }

    [Fact]
    public void Errors_without_field_group_under_empty_key()
    {
        var errors = new[]
        {
            new Error("orders.total.mismatch", "Totals do not match"),
            new Error("general.value.is-required", "Email is required") { Field = "Email" },
        };

        var grouped = KotoProblemDetails.GroupByField(errors);

        grouped[""].Should().ContainSingle().Which.Should().Be("Totals do not match");
        grouped["Email"].Should().ContainSingle();
    }

    [Fact]
    public void Single_element_list_delegates_to_single_error_overload()
    {
        var result = KotoProblemDetails.From(
            new[] { new Error("orders.order.not-found", "nope") }, Options);

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public void Empty_error_list_throws()
    {
        var act = () => KotoProblemDetails.From(Array.Empty<Error>(), Options);
        act.Should().Throw<ArgumentException>();
    }
}
