using AwesomeAssertions;
using Koto.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Testcontainers.PostgreSql;

namespace Koto.Infrastructure.EFCore.Tests;

// Кросс-агрегатная ссылка: Shipment.OrderRef — НЕ ключ и НЕ настроен вручную.
public sealed record ShipmentId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static ShipmentId New() => new(Guid.NewGuid());
}

public sealed record OrderRefId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrderRefId New() => new(Guid.NewGuid());
}

public sealed class Shipment : AggregateRoot<ShipmentId>
{
    public OrderRefId OrderRef { get; private set; } = null!;

    private Shipment()
    {
    }

    public static Shipment Create(OrderRefId orderRef) =>
        new() { Id = ShipmentId.New(), OrderRef = orderRef };
}

public sealed class ShipmentDbContext(DbContextOptions<ShipmentDbContext> options)
    : KotoDbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Shipment>().HasKey(s => s.Id);
        // Ничего про OrderRef: конвертация должна прийти из авто-сканирования KotoDbContext.
    }
}

public sealed class AutoStronglyTypedIdConversionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private ShipmentDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _context = new ShipmentDbContext(new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);
        var creator = _context.Database.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Non_key_strongly_typed_id_round_trips_without_manual_configuration()
    {
        var orderRef = OrderRefId.New();
        _context.Shipments.Add(Shipment.Create(orderRef));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.Shipments.SingleAsync(s => s.OrderRef == orderRef);

        loaded.OrderRef.Should().Be(orderRef);
    }

    [Fact]
    public void Model_maps_id_reference_as_scalar_property_not_navigation()
    {
        var entity = _context.Model.FindEntityType(typeof(Shipment))!;

        entity.FindProperty(nameof(Shipment.OrderRef)).Should().NotBeNull(
            "ссылка на чужой StronglyTypedId должна быть скалярной колонкой");
        entity.GetNavigations().Should().BeEmpty();
    }
}
