using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Application.Tests;

public class ErrorEnrichmentTests
{
    private static readonly Error A = new TestError("test.enrich.a", "A");
    private static readonly Error B = new TestError("test.enrich.b", "B");

    [Fact]
    public void ForField_sets_field_on_every_error()
    {
        var list = new ErrorList<Error>([A, B]);

        var enriched = list.ForField("Email");

        enriched.Should().HaveCount(2);
        enriched.Should().AllSatisfy(e => e.Field.Should().Be("Email"));
        enriched[0].Inner.Should().BeSameAs(A);
    }

    [Fact]
    public void Enrich_wraps_plain_errors_and_leaves_already_enriched_untouched()
    {
        var already = new EnrichedError(A) { Field = "X" };
        var list = new ErrorList<Error>([already, B]);

        var enriched = list.Enrich();

        enriched[0].Should().BeSameAs(already);
        enriched[1].Inner.Should().BeSameAs(B);
    }

    [Fact]
    public void Enrich_with_map_applies_mapping()
    {
        var list = new ErrorList<Error>([A]);

        var enriched = list.Enrich(e => e.WithDetails(("k", 1)));

        enriched[0].Details["k"].Should().Be(1);
    }

    [Fact]
    public void Error_verbs_compose_without_double_wrapping()
    {
        var enriched = A.ForField("f").WithDetails(("a", 1), ("b", "x"));

        enriched.Field.Should().Be("f");
        enriched.Details.Should().HaveCount(2);
        enriched.Inner.Should().BeSameAs(A);
    }

    [Fact]
    public void ForField_on_already_enriched_replaces_field()
    {
        var enriched = new EnrichedError(A) { Field = "old" }.ForField("new");

        enriched.Field.Should().Be("new");
        enriched.Inner.Should().BeSameAs(A);
    }

    [Fact]
    public void Result_ForField_keeps_success_value()
    {
        Result<int>.Success(1).ForField("x").Value.Should().Be(1);
    }

    [Fact]
    public void Result_ForField_enriches_all_errors_and_stays_a_domain_result()
    {
        var result = Result<int>.Failure([A, B]).ForField("x");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().AllSatisfy(e => e.Should().BeOfType<EnrichedError>().Which.Field.Should().Be("x"));
    }
}
