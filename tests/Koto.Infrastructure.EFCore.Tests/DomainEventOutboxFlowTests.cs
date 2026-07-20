using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace Koto.Infrastructure.EFCore.Tests;

// --- Мини-домен для сквозного сценария ---

public sealed record ParcelId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static ParcelId New() => new(Guid.NewGuid());
}

public sealed record ParcelShippedDomainEvent(ParcelId ParcelId) : DomainEvent;

public sealed class Parcel : AggregateRoot<ParcelId>
{
    private Parcel()
    {
    }

    public static Parcel Ship()
    {
        var parcel = new Parcel { Id = ParcelId.New() };
        parcel.AddDomainEvent(new ParcelShippedDomainEvent(parcel.Id));
        return parcel;
    }
}

public sealed class ParcelDbContext(DbContextOptions<ParcelDbContext> options) : KotoDbContext(options)
{
    public DbSet<Parcel> Parcels => Set<Parcel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Parcel>().HasKey(p => p.Id);
    }
}

/// <summary>Wolverine-хендлер, фиксирующий получение доменного события.</summary>
public static class ParcelShippedHandler
{
    public static readonly TaskCompletionSource<ParcelShippedDomainEvent> Received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void Handle(ParcelShippedDomainEvent @event) => Received.TrySetResult(@event);
}

/// <summary>
/// Сквозная проверка обещания README: изменение агрегата + CommitAsync из ОБЫЧНОГО кода
/// (не Wolverine-хендлера) публикует доменные события через durable outbox
/// в in-process хендлер.
/// </summary>
public sealed class DomainEventOutboxFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Discovery.IncludeAssembly(typeof(ParcelShippedHandler).Assembly);
                opts.PersistMessagesWithPostgresql(_postgres.GetConnectionString());
                opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
                opts.UseEntityFrameworkCoreTransactions();
            })
            .ConfigureServices(services => services.AddKotoEFCore<ParcelDbContext>(
                o => o.UseNpgsql(_postgres.GetConnectionString())))
            .StartAsync();

        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ParcelDbContext>().Database;
        // EnsureCreated пропускает создание схемы, если в базе уже есть таблицы (их создал Wolverine).
        var creator = (Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator)
            database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Commit_from_plain_code_delivers_domain_event_to_in_process_handler()
    {
        Parcel parcel;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ParcelDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            parcel = Parcel.Ship();
            context.Parcels.Add(parcel);
            await unitOfWork.CommitAsync();
        }

        var received = await ParcelShippedHandler.Received.Task.WaitAsync(TimeSpan.FromSeconds(20));
        received.ParcelId.Should().Be(parcel.Id);

        // Событие сохранено, а после SaveChanges агрегат очищен от uncommitted events.
        parcel.DomainEvents.Should().BeEmpty();
    }
}
