using AwesomeAssertions;
using Koto.Api.AspNetCore;
using Koto.Domain;

namespace Koto.Api.AspNetCore.Tests;

public class KotoHttpErrorOptionsTests
{
    private static int StatusFor(string code, KotoHttpErrorOptions? options = null) =>
        (options ?? new KotoHttpErrorOptions()).StatusCodeFor(new Error(code, "msg"));

    // ── Default table ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("orders.order.not-found", 404)]
    [InlineData("users.user.not-found", 404)]
    [InlineData("orders.order.already-placed", 409)]
    [InlineData("users.email.already-exists", 409)]
    [InlineData("subscription.conflict", 409)]
    [InlineData("auth.token.unauthorized", 401)]
    [InlineData("auth.action.forbidden", 403)]
    [InlineData("general.value.is-required", 400)]
    [InlineData("general.invalid-length", 400)]
    [InlineData("general.collection-is-too-small", 400)]
    [InlineData("validation.failed", 400)]
    public void Default_table_maps_known_categories(string code, int expected) =>
        StatusFor(code).Should().Be(expected);

    [Theory]
    [InlineData("orders.order.payment-failed")]
    [InlineData("subscription.limit-reached")]
    public void Unmapped_business_errors_are_422_not_500(string code) =>
        StatusFor(code).Should().Be(422);

    [Fact]
    public void Field_errors_default_to_400()
    {
        var error = new Error("users.email.custom-rule", "msg") { Field = "Email" };
        new KotoHttpErrorOptions().StatusCodeFor(error).Should().Be(400);
    }

    [Fact]
    public void Fallback_is_configurable()
    {
        var options = new KotoHttpErrorOptions { FallbackStatusCode = 400 };
        StatusFor("orders.order.payment-failed", options).Should().Be(400);
    }

    // ── Priorities ─────────────────────────────────────────────────────────────

    [Fact]
    public void Exact_mapping_beats_everything()
    {
        var options = new KotoHttpErrorOptions().Map("orders.order.not-found", 410);
        StatusFor("orders.order.not-found", options).Should().Be(410);
    }

    [Fact]
    public void Custom_rule_beats_suffix()
    {
        var options = new KotoHttpErrorOptions()
            .Map(e => e.Code.Contains(".order.") ? 418 : null);
        StatusFor("orders.order.not-found", options).Should().Be(418);
    }

    [Fact]
    public void Suffix_beats_prefix()
    {
        // "general." prefix says 400, but ".not-found" suffix wins.
        StatusFor("general.not-found").Should().Be(404);
    }

    [Fact]
    public void User_suffix_beats_default_suffix()
    {
        var options = new KotoHttpErrorOptions().MapSuffix(".not-found", 444);
        StatusFor("orders.order.not-found", options).Should().Be(444);
    }

    [Fact]
    public void User_prefix_wins_over_default_prefix()
    {
        var options = new KotoHttpErrorOptions().MapPrefix("general.", 422);
        StatusFor("general.some-rule", options).Should().Be(422);
    }

    [Fact]
    public void MapPrefix_matches_custom_namespaces()
    {
        var options = new KotoHttpErrorOptions().MapPrefix("payments.", 502);
        StatusFor("payments.gateway-timeout", options).Should().Be(502);
    }
}
