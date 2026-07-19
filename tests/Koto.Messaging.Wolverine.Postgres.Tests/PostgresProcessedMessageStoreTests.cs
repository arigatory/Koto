using AwesomeAssertions;
using Koto.Messaging.Wolverine.Postgres;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Koto.Messaging.Wolverine.Postgres.Tests;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;

[Collection(nameof(PostgresCollection))]
public sealed class PostgresProcessedMessageStoreTests
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresProcessedMessageStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private PostgresProcessedMessageStore CreateStore(
        Npgsql.NpgsqlDataSource dataSource,
        TimeSpan? window = null,
        Action<PostgresProcessedMessageStoreOptions>? configure = null)
    {
        var storeOptions = new PostgresProcessedMessageStoreOptions();
        configure?.Invoke(storeOptions);

        var wolverineOptions = new KotoWolverineOptions();
        if (window is not null)
            wolverineOptions.IdempotencyWindow = window.Value;

        return new PostgresProcessedMessageStore(
            dataSource,
            Options.Create(storeOptions),
            Options.Create(wolverineOptions));
    }

    [Fact]
    public async Task IsProcessed_returns_false_for_unknown_message()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var store = CreateStore(dataSource);

        var result = await store.IsProcessedAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Mark_then_IsProcessed_survives_a_new_store_instance()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var messageId = Guid.NewGuid();

        var writer = CreateStore(dataSource);
        await writer.MarkAsProcessedAsync(messageId);

        // Новый экземпляр стора — имитация рестарта сервиса: in-memory здесь бы забыл.
        var reader = CreateStore(dataSource);
        var result = await reader.IsProcessedAsync(messageId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Mark_twice_does_not_throw()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var store = CreateStore(dataSource);
        var messageId = Guid.NewGuid();

        await store.MarkAsProcessedAsync(messageId);
        var act = async () => await store.MarkAsProcessedAsync(messageId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsProcessed_returns_false_after_window_expiry()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var store = CreateStore(dataSource, window: TimeSpan.FromMilliseconds(50));
        var messageId = Guid.NewGuid();

        await store.MarkAsProcessedAsync(messageId);
        await Task.Delay(200);

        var result = await store.IsProcessedAsync(messageId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteExpired_removes_only_entries_outside_window()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var shortWindow = CreateStore(dataSource, window: TimeSpan.FromMilliseconds(50),
            configure: o => o.Table = "cleanup_test");
        var expired = Guid.NewGuid();
        await shortWindow.MarkAsProcessedAsync(expired);
        await Task.Delay(200);

        var fresh = Guid.NewGuid();
        await shortWindow.MarkAsProcessedAsync(fresh);

        var deleted = await shortWindow.DeleteExpiredAsync();

        deleted.Should().Be(1);
        (await shortWindow.IsProcessedAsync(fresh)).Should().BeTrue();
    }

    [Fact]
    public async Task Custom_schema_and_table_are_created_and_used()
    {
        await using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
        var store = CreateStore(dataSource, configure: o =>
        {
            o.Schema = "custom_schema";
            o.Table = "custom_dedup";
        });
        var messageId = Guid.NewGuid();

        await store.MarkAsProcessedAsync(messageId);

        (await store.IsProcessedAsync(messageId)).Should().BeTrue();
    }

    [Theory]
    [InlineData("1bad")]
    [InlineData("bad-name")]
    [InlineData("Bad\"; DROP TABLE users; --")]
    [InlineData("")]
    public void Invalid_identifiers_are_rejected_at_construction(string bad)
    {
        var act = () =>
        {
            using var dataSource = Npgsql.NpgsqlDataSource.Create(_fixture.Container.GetConnectionString());
            _ = CreateStore(dataSource, configure: o => o.Table = bad);
        };

        act.Should().Throw<ArgumentException>();
    }
}
