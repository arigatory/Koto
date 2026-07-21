using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Koto.EventSourcing.Marten.Tests;

// --- Мини-домен: два счёта, перевод между ними должен быть атомарным ---

public sealed record AccountId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static AccountId New() => new(Guid.NewGuid());
}

public sealed record AccountOpened(Guid AccountId) : DomainEvent;

public sealed record CoinsMoved(Guid AccountId, int Delta) : DomainEvent;

public sealed class Account : EventSourcedAggregateRoot<AccountId>
{
    public int Balance { get; private set; }

    private Account()
    {
    }

    public static Account Open()
    {
        var account = new Account();
        account.RaiseEvent(new AccountOpened(Guid.NewGuid()));
        return account;
    }

    public void Move(int delta) => RaiseEvent(new CoinsMoved(Id?.Value ?? Guid.Empty, delta));

    protected override void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                Id = new AccountId(opened.AccountId);
                break;
            case CoinsMoved moved:
                Balance += moved.Delta;
                break;
        }
    }
}

public sealed record ProcessedOperation(string Id);

public sealed class MartenUnitOfWorkTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddKotoMarten(_postgres.GetConnectionString());
        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Two_streams_and_a_document_commit_atomically()
    {
        var (from, to) = (Account.Open(), Account.Open());
        from.Move(-100);
        to.Move(100);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider
                .GetRequiredService<IEventSourcedRepository<Account, AccountId>>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

            repo.Append(from);
            repo.Append(to);
            session.Store(new ProcessedOperation("transfer:42"));

            await uow.CommitAsync();
        }

        from.UncommittedEvents.Should().BeEmpty("после коммита события очищены");
        to.UncommittedEvents.Should().BeEmpty();

        await using var readScope = _provider.CreateAsyncScope();
        var readRepo = readScope.ServiceProvider
            .GetRequiredService<IEventSourcedRepository<Account, AccountId>>();
        var query = readScope.ServiceProvider.GetRequiredService<IQuerySession>();

        (await readRepo.GetByIdAsync(from.Id))!.Balance.Should().Be(-100);
        (await readRepo.GetByIdAsync(to.Id))!.Balance.Should().Be(100);
        (await query.LoadAsync<ProcessedOperation>("transfer:42")).Should().NotBeNull();
    }

    [Fact]
    public async Task Rollback_discards_staged_events_and_documents()
    {
        var account = Account.Open();
        account.Move(50);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider
                .GetRequiredService<IEventSourcedRepository<Account, AccountId>>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

            repo.Append(account);
            session.Store(new ProcessedOperation("doomed:1"));

            await uow.RollbackAsync();
            await session.SaveChangesAsync(); // после отката сохранять нечего
        }

        account.UncommittedEvents.Should().NotBeEmpty("события не сохранены — не должны быть очищены");

        await using var readScope = _provider.CreateAsyncScope();
        var readRepo = readScope.ServiceProvider
            .GetRequiredService<IEventSourcedRepository<Account, AccountId>>();
        var query = readScope.ServiceProvider.GetRequiredService<IQuerySession>();

        (await readRepo.GetByIdAsync(account.Id)).Should().BeNull();
        (await query.LoadAsync<ProcessedOperation>("doomed:1")).Should().BeNull();
    }

    [Fact]
    public async Task Single_aggregate_SaveAsync_still_works()
    {
        var account = Account.Open();
        account.Move(10);

        await using var scope = _provider.CreateAsyncScope();
        var repo = scope.ServiceProvider
            .GetRequiredService<IEventSourcedRepository<Account, AccountId>>();

        await repo.SaveAsync(account);

        account.UncommittedEvents.Should().BeEmpty();
        (await repo.GetByIdAsync(account.Id))!.Balance.Should().Be(10);
    }
}
