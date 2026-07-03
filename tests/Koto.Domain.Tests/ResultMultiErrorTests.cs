using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class ResultMultiErrorTests
{
    private static readonly Error ErrorA = new("test.a", "A failed");
    private static readonly Error ErrorB = new("test.b", "B failed");

    // ── Multi-error construction ───────────────────────────────────────────────

    [Fact]
    public void Failure_with_many_errors_exposes_all_and_first_as_Error()
    {
        var result = Result<int>.Failure([ErrorA, ErrorB]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainInOrder(ErrorA, ErrorB);
        result.Error.Should().Be(ErrorA);
    }

    [Fact]
    public void Success_has_empty_errors()
    {
        Result<int>.Success(1).Errors.Should().BeEmpty();
    }

    // ── Guards ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Success_with_null_value_throws()
    {
        var act = () => Result<string>.Success(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_with_null_error_throws()
    {
        var act = () => Result<string>.Failure((Error)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_with_empty_error_list_throws()
    {
        var act = () => Result<string>.Failure(Array.Empty<Error>());
        act.Should().Throw<ArgumentException>();
    }

    // ── Propagation ────────────────────────────────────────────────────────────

    [Fact]
    public void Map_and_Bind_propagate_all_errors()
    {
        var failed = Result<int>.Failure([ErrorA, ErrorB]);

        failed.Map(x => x + 1).Errors.Should().HaveCount(2);
        failed.Bind(x => Result<string>.Success(x.ToString())).Errors.Should().HaveCount(2);
    }

    [Fact]
    public void TapErrors_receives_all_errors()
    {
        IReadOnlyList<Error>? seen = null;

        Result<int>.Failure([ErrorA, ErrorB]).TapErrors(errors => seen = errors);

        seen.Should().NotBeNull();
        seen.Should().HaveCount(2);
    }

    // ── Result companion: Success/Failure/Combine ──────────────────────────────

    [Fact]
    public void NonGeneric_Success_and_Failure_produce_unit_results()
    {
        Result.Success().IsSuccess.Should().BeTrue();
        Result.Failure(ErrorA).Error.Should().Be(ErrorA);
        Result.Failure([ErrorA, ErrorB]).Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Combine_two_successes_yields_tuple()
    {
        var combined = Result.Combine(Result<int>.Success(1), Result<string>.Success("x"));

        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Be((1, "x"));
    }

    [Fact]
    public void Combine_aggregates_all_errors_in_order()
    {
        var combined = Result.Combine(
            Result<int>.Failure(ErrorA),
            Result<string>.Success("x"),
            Result<int>.Failure(ErrorB));

        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().ContainInOrder(ErrorA, ErrorB);
    }

    [Fact]
    public void Combine_params_overload_aggregates_mixed_results()
    {
        var combined = Result.Combine(
            Result<int>.Failure([ErrorA, ErrorB]),
            Result.Success(),
            Result<string>.Failure(ErrorA));

        combined.IsFailure.Should().BeTrue();
        combined.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void Combine_params_overload_with_all_successes_is_success()
    {
        Result.Combine(Result<int>.Success(1), Result.Success()).IsSuccess.Should().BeTrue();
    }

    // ── MatchAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_with_async_success_handler()
    {
        var value = await Result<int>.Success(21).MatchAsync(
            onSuccess: async x => { await Task.Yield(); return x * 2; },
            onFailure: _ => -1);

        value.Should().Be(42);
    }

    [Fact]
    public async Task MatchAsync_with_sync_failure_handler()
    {
        var value = await Result<int>.Failure(ErrorA).MatchAsync(
            onSuccess: async x => { await Task.Yield(); return x; },
            onFailure: error => error.Code.Length);

        value.Should().Be(ErrorA.Code.Length);
    }

    [Fact]
    public async Task MatchAsync_with_both_async_handlers()
    {
        var value = await Result<int>.Failure(ErrorA).MatchAsync(
            onSuccess: async x => { await Task.Yield(); return "ok"; },
            onFailure: async e => { await Task.Yield(); return e.Code; });

        value.Should().Be("test.a");
    }
}
