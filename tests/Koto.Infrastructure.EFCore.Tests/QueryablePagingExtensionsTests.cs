using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Testcontainers.PostgreSql;

namespace Koto.Infrastructure.EFCore.Tests;

public sealed class PagingDbContext(DbContextOptions<PagingDbContext> options) : DbContext(options)
{
    public DbSet<PagingItem> Items => Set<PagingItem>();
}

public sealed class PagingItem
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class QueryablePagingExtensionsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private PagingDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _context = new PagingDbContext(new DbContextOptionsBuilder<PagingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);
        await _context.Database.EnsureCreatedAsync();

        _context.Items.AddRange(Enumerable.Range(1, 25).Select(i => new PagingItem
        {
            Id = i,
            Name = $"item-{i:D2}",
        }));
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Returns_requested_page_with_totals()
    {
        var page = await _context.Items.OrderBy(i => i.Id).ToPagedListAsync(page: 2, pageSize: 10);

        page.Items.Should().HaveCount(10);
        page.Items[0].Id.Should().Be(11);
        page.TotalCount.Should().Be(25);
        page.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Page_past_the_end_is_empty_but_keeps_total()
    {
        var page = await _context.Items.OrderBy(i => i.Id).ToPagedListAsync(page: 9, pageSize: 10);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task Filtered_query_counts_after_filter()
    {
        var page = await _context.Items
            .Where(i => i.Id > 20)
            .OrderBy(i => i.Id)
            .ToPagedListAsync(page: 1, pageSize: 10);

        page.TotalCount.Should().Be(5);
        page.Items.Should().HaveCount(5);
    }
}
