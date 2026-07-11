using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class ResultCollectionsTests
{
    private static readonly Error ErrorA = new("test.a", "A failed");
    private static readonly Error ErrorB = new("test.b", "B failed");

    // ── Sequence ───────────────────────────────────────────────────────────────

    [Fact]
    public void Sequence_of_empty_collection_succeeds_with_empty_list()
    {
        var result = Result.Sequence(Array.Empty<Result<int>>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void Sequence_of_successes_preserves_order()
    {
        var result = Result.Sequence([Result<int>.Success(1), Result<int>.Success(2), Result<int>.Success(3)]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Sequence_with_failures_aggregates_all_errors()
    {
        var result = Result.Sequence([
            Result<int>.Failure(ErrorA),
            Result<int>.Success(2),
            Result<int>.Failure(ErrorB),
        ]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainInOrder(ErrorA, ErrorB);
    }

    [Fact]
    public void Sequence_failfast_stops_at_first_error()
    {
        var result = Result.Sequence(
            [Result<int>.Failure(ErrorA), Result<int>.Failure(ErrorB)],
            FailureMode.FailFast);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(ErrorA);
    }

    [Fact]
    public void Sequence_null_collection_throws()
    {
        var act = () => Result.Sequence<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Traverse ───────────────────────────────────────────────────────────────

    [Fact]
    public void Traverse_maps_and_preserves_order()
    {
        var result = Result.Traverse([1, 2, 3], x => Result<string>.Success($"v{x}"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainInOrder("v1", "v2", "v3");
    }

    [Fact]
    public void Traverse_aggregates_all_errors_by_default()
    {
        var result = Result.Traverse(
            [1, 2, 3],
            x => x == 2 ? Result<int>.Success(x) : Result<int>.Failure(x == 1 ? ErrorA : ErrorB));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainInOrder(ErrorA, ErrorB);
    }

    [Fact]
    public void Traverse_failfast_does_not_call_selector_after_first_error()
    {
        var visited = new List<int>();

        var result = Result.Traverse(
            [1, 2, 3],
            x =>
            {
                visited.Add(x);
                return Result<int>.Failure(ErrorA);
            },
            FailureMode.FailFast);

        result.IsFailure.Should().BeTrue();
        visited.Should().ContainSingle().Which.Should().Be(1);
    }

    // ── TraverseAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TraverseAsync_runs_sequentially_and_preserves_order()
    {
        var running = 0;

        var result = await Result.TraverseAsync([1, 2, 3], async x =>
        {
            Interlocked.Increment(ref running).Should().Be(1, "selector must never run concurrently");
            await Task.Yield();
            Interlocked.Decrement(ref running);
            return Result<int>.Success(x * 10);
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainInOrder(10, 20, 30);
    }

    [Fact]
    public async Task TraverseAsync_failfast_stops_visiting_after_first_error()
    {
        var visited = new List<int>();

        var result = await Result.TraverseAsync(
            [1, 2, 3],
            x =>
            {
                visited.Add(x);
                return Task.FromResult(Result<int>.Failure(ErrorA));
            },
            FailureMode.FailFast);

        result.IsFailure.Should().BeTrue();
        visited.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task TraverseAsync_respects_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        var visited = 0;

        var act = async () => await Result.TraverseAsync<int, int>(
            [1, 2, 3],
            (x, _) =>
            {
                visited++;
                cts.Cancel();
                return Task.FromResult(Result<int>.Success(x));
            },
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        visited.Should().Be(1, "the token is checked before each subsequent element");
    }
}
