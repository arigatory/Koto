using AwesomeAssertions;
using Koto.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;

namespace Koto.Scheduling.Tests;

public class ScheduledJobBaseTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class SucceedingJob : ScheduledJobBase
    {
        public bool Executed { get; private set; }
        public override string JobId => "test.succeeding";

        public SucceedingJob() : base(NullLogger<ScheduledJobBase>.Instance) { }

        public override Task ExecuteAsync(CancellationToken ct)
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingJob : ScheduledJobBase
    {
        public override string JobId => "test.failing";

        public FailingJob() : base(NullLogger<ScheduledJobBase>.Instance) { }

        public override Task ExecuteAsync(CancellationToken ct)
            => throw new InvalidOperationException("job failure");
    }

    private static IJobExecutionContext MakeContext()
    {
        var ctx = Substitute.For<IJobExecutionContext>();
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_calls_ExecuteAsync_on_success()
    {
        var job = new SucceedingJob();

        await job.Execute(MakeContext());

        job.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_wraps_exception_in_JobExecutionException()
    {
        var job = new FailingJob();

        var act = () => job.Execute(MakeContext());

        await act.Should().ThrowAsync<JobExecutionException>();
    }

    [Fact]
    public async Task Execute_does_not_refire_on_failure()
    {
        var job = new FailingJob();
        JobExecutionException? thrown = null;

        try { await job.Execute(MakeContext()); }
        catch (JobExecutionException ex) { thrown = ex; }

        thrown.Should().NotBeNull();
        thrown!.RefireImmediately.Should().BeFalse();
    }
}
