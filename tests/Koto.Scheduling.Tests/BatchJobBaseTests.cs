using AwesomeAssertions;
using Koto.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koto.Scheduling.Tests;

public class BatchJobBaseTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class TrackingBatchJob : BatchJobBase<int>
    {
        private readonly IReadOnlyList<int> _items;
        public List<int> Processed { get; } = [];
        public int FetchCallCount { get; private set; }
        public bool FailOnItem { get; set; }

        public override string JobId => "test.batch";
        protected override int BatchSize => 3;

        public TrackingBatchJob(IReadOnlyList<int> items)
            : base(NullLogger<BatchJobBase<int>>.Instance)
            => _items = items;

        protected override Task<IReadOnlyList<int>> FetchBatchAsync(
            int offset, int batchSize, CancellationToken ct)
        {
            FetchCallCount++;
            IReadOnlyList<int> page = _items.Skip(offset).Take(batchSize).ToList();
            return Task.FromResult(page);
        }

        protected override Task ProcessItemAsync(int item, CancellationToken ct)
        {
            if (FailOnItem && item % 2 == 0)
                throw new InvalidOperationException($"failed item {item}");

            Processed.Add(item);
            return Task.CompletedTask;
        }
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Processes_all_items_across_pages()
    {
        var items = Enumerable.Range(1, 7).ToList();
        var job = new TrackingBatchJob(items);

        await job.ExecuteAsync(CancellationToken.None);

        job.Processed.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task Stops_when_batch_is_empty()
    {
        var job = new TrackingBatchJob([]);

        await job.ExecuteAsync(CancellationToken.None);

        job.FetchCallCount.Should().Be(1);
        job.Processed.Should().BeEmpty();
    }

    [Fact]
    public async Task Continues_after_item_level_failure()
    {
        var items = Enumerable.Range(1, 5).ToList(); // 1,2,3,4,5 — even items fail
        var job = new TrackingBatchJob(items) { FailOnItem = true };

        await job.ExecuteAsync(CancellationToken.None);

        // only odd items processed; even items (2,4) failed but job continued
        job.Processed.Should().BeEquivalentTo([1, 3, 5]);
    }

    [Fact]
    public async Task Fetches_correct_number_of_pages_for_exact_multiple()
    {
        var items = Enumerable.Range(1, 6).ToList(); // exactly 2 pages of 3
        var job = new TrackingBatchJob(items);

        await job.ExecuteAsync(CancellationToken.None);

        // page 1: items 1-3 (full), page 2: items 4-6 (full = batch size → fetch page 3), page 3: empty → stop
        job.FetchCallCount.Should().Be(3);
        job.Processed.Should().HaveCount(6);
    }
}
