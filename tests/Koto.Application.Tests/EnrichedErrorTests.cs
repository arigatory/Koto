using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Application.Tests;

public class EnrichedErrorTests
{
    private sealed record Full(int Capacity) : Error("test.meetup.full", "Full.");

    [Fact]
    public void Copies_code_and_message_from_inner()
    {
        var enriched = new EnrichedError(new Full(5));

        enriched.Code.Should().Be("test.meetup.full");
        enriched.Message.Should().Be("Full.");
        enriched.Inner.Should().BeOfType<Full>();
        enriched.Field.Should().BeNull();
        enriched.Details.Should().BeEmpty();
    }

    [Fact]
    public void Wrapping_enriched_error_collapses_to_single_layer()
    {
        var inner = new Full(5);
        var first = new EnrichedError(inner)
        {
            Field = "meetupId",
            Details = new Dictionary<string, object?> { ["hint"] = "x" },
        };

        var second = new EnrichedError(first);

        second.Inner.Should().BeSameAs(inner);
        second.Field.Should().Be("meetupId");
        second.Details.Should().ContainKey("hint").WhoseValue.Should().Be("x");
    }

    [Fact]
    public void Equality_includes_field_and_details_structurally()
    {
        var left = new EnrichedError(new Full(1)) { Field = "f", Details = new Dictionary<string, object?> { ["k"] = 1 } };
        var same = new EnrichedError(new Full(1)) { Field = "f", Details = new Dictionary<string, object?> { ["k"] = 1 } };
        var otherField = new EnrichedError(new Full(1)) { Field = "g", Details = new Dictionary<string, object?> { ["k"] = 1 } };
        var otherDetails = new EnrichedError(new Full(1)) { Field = "f", Details = new Dictionary<string, object?> { ["k"] = 2 } };

        left.Should().Be(same);
        left.GetHashCode().Should().Be(same.GetHashCode());
        left.Should().NotBe(otherField);
        left.Should().NotBe(otherDetails);
    }

    [Fact]
    public void Is_an_error_and_fits_into_a_domain_result()
    {
        Result<int> result = new EnrichedError(new Full(1)) { Field = "x" };

        result.FirstError.Should().BeOfType<EnrichedError>();
    }

    [Fact]
    public void With_expression_preserves_inner_and_type()
    {
        var enriched = new EnrichedError(new Full(1)) with { Field = "x" };

        enriched.Inner.Should().BeOfType<Full>();
        enriched.Field.Should().Be("x");
    }
}
