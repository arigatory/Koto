using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Koto.Testing.Fakes;

namespace Koto.Testing.Tests;

public class FakesTests
{
    // ── Fake domain ────────────────────────────────────────────────────────────

    private sealed class Product : AggregateRoot<Guid>
    {
        public string Name { get; private set; } = "";

        private Product(Guid id) : base(id) { }

        public static Product Create(string name) =>
            new Product(Guid.NewGuid()) { Name = name };
    }

    private sealed record ProductShipped(string Sku) : IntegrationEvent;

    // ── FakeRepository ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FakeRepository_add_and_get_by_id()
    {
        var repo = new FakeRepository<Product, Guid>();
        var product = Product.Create("Widget");

        repo.Add(product);
        var found = await repo.GetByIdAsync(product.Id);

        found.Should().BeSameAs(product);
    }

    [Fact]
    public async Task FakeRepository_returns_null_for_unknown_id()
    {
        var repo = new FakeRepository<Product, Guid>();

        var found = await repo.GetByIdAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public void FakeRepository_delete_removes_aggregate()
    {
        var repo = new FakeRepository<Product, Guid>();
        var product = Product.Create("Widget");
        repo.Add(product);

        repo.Delete(product);

        repo.All.Should().BeEmpty();
    }

    [Fact]
    public void FakeRepository_All_exposes_all_added_aggregates()
    {
        var repo = new FakeRepository<Product, Guid>();
        repo.Add(Product.Create("A"));
        repo.Add(Product.Create("B"));

        repo.All.Should().HaveCount(2);
    }

    // ── FakeIntegrationEventPublisher ──────────────────────────────────────────

    [Fact]
    public async Task FakePublisher_captures_published_events()
    {
        var publisher = new FakeIntegrationEventPublisher();
        var @event = new ProductShipped("SKU-001");

        await publisher.PublishAsync(@event);

        publisher.PublishedEvents.Should().ContainSingle().Which.Should().Be(@event);
    }

    [Fact]
    public async Task FakePublisher_GetPublishedEvent_returns_first_matching()
    {
        var publisher = new FakeIntegrationEventPublisher();
        var e1 = new ProductShipped("SKU-001");
        var e2 = new ProductShipped("SKU-002");

        await publisher.PublishAsync(e1);
        await publisher.PublishAsync(e2);

        publisher.GetPublishedEvent<ProductShipped>().Should().Be(e1);
    }

    [Fact]
    public void FakePublisher_GetPublishedEvent_throws_when_none_published()
    {
        var publisher = new FakeIntegrationEventPublisher();

        var act = () => publisher.GetPublishedEvent<ProductShipped>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task FakePublisher_Clear_removes_all_events()
    {
        var publisher = new FakeIntegrationEventPublisher();
        await publisher.PublishAsync(new ProductShipped("SKU-001"));

        publisher.Clear();

        publisher.PublishedEvents.Should().BeEmpty();
    }
}
