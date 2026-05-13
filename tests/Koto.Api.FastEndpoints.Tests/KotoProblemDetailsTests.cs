using AwesomeAssertions;
using Koto.Api.FastEndpoints.ProblemDetails;
using Koto.Domain;

namespace Koto.Api.FastEndpoints.Tests;

public class KotoProblemDetailsTests
{
    // ── Status code mapping ────────────────────────────────────────────────────

    [Theory]
    [InlineData("orders.order.not-found", 404)]
    [InlineData("users.user.not-found", 404)]
    [InlineData("orders.order.already-placed", 409)]
    [InlineData("users.email.already-exists", 409)]
    [InlineData("general.value.is-required", 400)]
    [InlineData("general.value.too-long", 400)]
    [InlineData("orders.order.payment-failed", 500)]
    [InlineData("general.unexpected", 500)]
    public void StatusCodeFrom_maps_error_code_correctly(string code, int expectedStatus)
    {
        KotoProblemDetails.StatusCodeFrom(code).Should().Be(expectedStatus);
    }

    [Fact]
    public void StatusCodeFrom_prefers_not_found_over_already_for_ambiguous_codes()
    {
        // ".not-found" pattern takes priority (checked first in switch)
        KotoProblemDetails.StatusCodeFrom("orders.already-cancelled.not-found").Should().Be(404);
    }

    // ── Result creation ────────────────────────────────────────────────────────

    [Fact]
    public void From_returns_non_null_result()
    {
        var error = new Error("orders.order.not-found", "Order not found");

        var result = KotoProblemDetails.From(error, "corr-123");

        result.Should().NotBeNull();
    }

    [Fact]
    public void From_works_without_correlation_id()
    {
        var error = new Error("general.value.is-required", "Value is required");

        var result = KotoProblemDetails.From(error);

        result.Should().NotBeNull();
    }
}
