using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;

namespace Koto.Api.AspNetCore.Tests;

public class KotoApiOptionsTests
{
    private static int StatusFor(string code, KotoApiOptions? options = null) =>
        (options ?? new KotoApiOptions()).StatusCodeFor(new TestError(code, "msg"));

    [Theory]
    [InlineData("orders.order.not-found", 404)]
    [InlineData("general.entity.not-found", 404)]
    [InlineData("orders.order.already-placed", 409)]
    [InlineData("users.email.already-exists", 409)]
    [InlineData("subscription.plan.conflict", 409)]
    [InlineData("auth.token.unauthorized", 401)]
    [InlineData("auth.action.forbidden", 403)]
    [InlineData("general.value.is-required", 400)]
    [InlineData("general.value.invalid-length", 400)]
    [InlineData("validation.rule.failed", 400)]
    public void Built_in_classifier_maps_known_categories(string code, int expected) =>
        StatusFor(code).Should().Be(expected);

    [Theory]
    [InlineData("orders.order.payment-failed")]
    [InlineData("subscription.limit.reached")]
    public void Unmapped_business_errors_are_422_not_500(string code) =>
        StatusFor(code).Should().Be(422);

    [Fact]
    public void Field_bound_errors_default_to_400()
    {
        var error = new TestError("users.email.custom-rule", "msg").ForField("Email");

        new KotoApiOptions().StatusCodeFor(error).Should().Be(400);
    }

    [Fact]
    public void Fallback_is_configurable()
    {
        var options = new KotoApiOptions { FallbackStatusCode = 400 };

        StatusFor("orders.order.payment-failed", options).Should().Be(400);
    }

    [Fact]
    public void Exact_mapping_beats_everything()
    {
        var options = new KotoApiOptions().Map("orders.order.not-found", 410);

        StatusFor("orders.order.not-found", options).Should().Be(410);
    }

    [Fact]
    public void Custom_rule_beats_built_in_classifier()
    {
        var options = new KotoApiOptions().Map(e => e.Code.Contains(".order.", StringComparison.Ordinal) ? 418 : null);

        StatusFor("orders.order.not-found", options).Should().Be(418);
    }

    [Fact]
    public void Status_for_list_is_status_of_first_error()
    {
        var errors = new Error[]
        {
            new TestError("orders.order.payment-failed", "first").ForField("payment"),   // field-bound → 400
            new TestError("orders.order.not-found", "second"),                           // would be 404 on its own
        };

        new KotoApiOptions().StatusCodeFor(errors).Should().Be(400);
    }

    [Fact]
    public void Status_for_empty_list_throws()
    {
        var act = () => new KotoApiOptions().StatusCodeFor(Array.Empty<Error>());

        act.Should().Throw<ArgumentException>();
    }
}
