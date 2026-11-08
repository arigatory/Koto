using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Application.Tests;

public class ErrorDetailsTests
{
    private sealed record Plain() : Error("test.details.plain", "Plain.");
    private sealed record Quota(int MaxItems) : Error("test.details.quota", "Quota.");
    private sealed record Full(int Capacity) : Error("test.details.full", "Full.");
    private sealed record FullWithAlternative(Full Cause, Guid AlternativeId) : EnrichedError(Cause);
    private sealed record FullOverriding(Full Cause, int Capacity) : EnrichedError(Cause);
    private sealed record Shadowing(string Field, string Details, string Inner) : Error("test.details.shadowing", "Shadowing.");

    private sealed record Linked() : Error("test.details.linked", "Linked.")
    {
        public Error? Next { get; set; }
    }

    [Fact]
    public void Plain_error_without_properties_yields_empty()
    {
        ErrorDetails.Of(new Plain()).Should().BeEmpty();
    }

    [Fact]
    public void Record_properties_become_camel_case_keys()
    {
        var details = ErrorDetails.Of(new Quota(5));

        details.Should().HaveCount(1);
        details["maxItems"].Should().Be(5);
    }

    [Fact]
    public void Flattens_nested_error_properties()
    {
        var alternative = Guid.NewGuid();

        var details = ErrorDetails.Of(new FullWithAlternative(new Full(3), alternative));

        details.Should().HaveCount(2);
        details["capacity"].Should().Be(3);
        details["alternativeId"].Should().Be(alternative);
        details.Keys.Should().NotContain(["cause", "inner", "code", "message", "field", "details"]);
    }

    [Fact]
    public void Enriched_wrapper_without_properties_exposes_inner_properties()
    {
        var details = ErrorDetails.Of(new EnrichedError(new Quota(5)) { Field = "items" });

        details.Should().HaveCount(1);
        details["maxItems"].Should().Be(5);
    }

    [Fact]
    public void Explicit_details_override_properties()
    {
        var enriched = new EnrichedError(new Quota(5))
        {
            Details = new Dictionary<string, object?> { ["maxItems"] = 7, ["extra"] = true },
        };

        var details = ErrorDetails.Of(enriched);

        details["maxItems"].Should().Be(7);
        details["extra"].Should().Be(true);
    }

    [Fact]
    public void Outer_properties_override_inner()
    {
        var details = ErrorDetails.Of(new FullOverriding(new Full(3), Capacity: 9));

        details["capacity"].Should().Be(9);
    }

    [Fact]
    public void Record_properties_named_like_service_members_are_kept()
    {
        var details = ErrorDetails.Of(new Shadowing("f", "d", "i"));

        details.Should().BeEquivalentTo(new Dictionary<string, object?> { ["field"] = "f", ["details"] = "d", ["inner"] = "i" });
    }

    [Fact]
    public void Self_referencing_error_chain_terminates()
    {
        var linked = new Linked();
        linked.Next = linked;

        var act = () => ErrorDetails.Of(linked);

        act.Should().NotThrow();
    }
}
