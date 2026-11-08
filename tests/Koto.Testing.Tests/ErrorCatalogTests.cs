using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Koto.Testing.Catalog;
using Koto.Validation;

namespace Koto.Testing.Tests;

public class ErrorCatalogTests
{
    private sealed record Quota(int MaxItems, string Scope) : Error("test.catalog.quota", "Quota exceeded.");
    private sealed record Plain() : Error("test.catalog.plain", "Plain.");
    private sealed record Duplicate() : Error("test.catalog.plain", "Same code again.");
    private sealed record Full(int Capacity) : Error("test.catalog.full", "Full.");
    private sealed record FullWithAlternative(Full Cause, Guid AlternativeId) : EnrichedError(Cause);

    [Fact]
    public void Scan_finds_records_with_codes_and_detail_keys()
    {
        var entries = ErrorCatalog.Scan(typeof(ErrorCatalogTests).Assembly);

        var quota = entries.Should().ContainSingle(e => e.Type == typeof(Quota)).Subject;
        quota.Code.Should().Be("test.catalog.quota");
        quota.DetailKeys.Should().BeEquivalentTo(["maxItems", "scope"]);
        entries.Should().Contain(e => e.Type == typeof(Plain) && e.DetailKeys.Count == 0);
    }

    [Fact]
    public void ShouldBeValid_reports_duplicate_codes()
    {
        var act = () => ErrorCatalog.ShouldBeValid(typeof(ErrorCatalogTests).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*test.catalog.plain*")
            .WithMessage($"*{nameof(Duplicate)}*");
    }

    [Fact]
    public void Koto_own_catalog_is_valid()
    {
        var act = () => ErrorCatalog.ShouldBeValid(
            typeof(Error).Assembly, typeof(EnrichedError).Assembly, typeof(ValidationRuleFailed).Assembly);

        act.Should().NotThrow();
    }

    [Fact]
    public void Scan_skips_abstract_types_and_the_generic_wrapper_but_includes_typed_enriched_records()
    {
        var entries = ErrorCatalog.Scan(typeof(EnrichedError).Assembly, typeof(ErrorCatalogTests).Assembly);

        entries.Should().NotContain(e => e.Type == typeof(Error));
        entries.Should().NotContain(e => e.Type == typeof(EnrichedError));
        var enriched = entries.Should().ContainSingle(e => e.Type == typeof(FullWithAlternative)).Subject;
        enriched.Code.Should().Be("test.catalog.full");
        enriched.DetailKeys.Should().BeEquivalentTo(["capacity", "alternativeId"]);
    }

    [Fact]
    public void Scan_gives_dynamic_code_records_a_valid_stub_code()
    {
        var entries = ErrorCatalog.Scan(typeof(ErrorCatalogTests).Assembly);

        entries.Should().Contain(e => e.Type == typeof(TestError) && e.Code == "stub.error.code");
    }
}
