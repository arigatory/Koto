using AwesomeAssertions;
using Koto.Domain;
using Koto.Infrastructure.EFCore;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Koto.Infrastructure.EFCore.Tests;

// ---------------------------------------------------------------------------
// Test doubles
// ---------------------------------------------------------------------------

public sealed record TestId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static TestId New() => new(Guid.NewGuid());
}

public sealed class TestAggregate : AggregateRoot<TestId>
{
    public string Name { get; private set; } = string.Empty;

    public TestAggregate(TestId id, string name) : base(id) { Name = name; }

    private TestAggregate() { }
}

public sealed class TestDbContext : KotoDbContext
{
    public TestDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAggregate>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.Name).IsRequired();
        });
    }
}

// ---------------------------------------------------------------------------
// Fixture — one PostgreSQL container shared across all tests in this class
// ---------------------------------------------------------------------------

public sealed class RepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("koto_tests")
        .Build();

    private TestDbContext _context = null!;
    private Repository<TestAggregate, TestId> _repo = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new TestDbContext(opts);
        await _context.Database.EnsureCreatedAsync();
        _repo = new Repository<TestAggregate, TestId>(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task Add_and_GetById_round_trips()
    {
        var id = TestId.New();
        _repo.Add(new TestAggregate(id, "hello"));
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("hello");
    }

    [Fact]
    public async Task GetById_returns_null_for_unknown_id()
    {
        var result = await _repo.GetByIdAsync(TestId.New());
        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_removes_the_aggregate()
    {
        var id = TestId.New();
        _repo.Add(new TestAggregate(id, "bye"));
        await _context.SaveChangesAsync();

        var agg = await _repo.GetByIdAsync(id);
        _repo.Delete(agg!);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        (await _repo.GetByIdAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task StronglyTypedId_persists_and_reads_correctly()
    {
        var id = TestId.New();
        _repo.Add(new TestAggregate(id, "typed-id-test"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _repo.GetByIdAsync(id);
        loaded!.Id.Should().Be(id);
        loaded.Id.Value.Should().Be(id.Value);
    }
}
