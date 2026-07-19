using AwesomeAssertions;
using Koto.Messaging.Wolverine.Consuming;
using Koto.Messaging.Wolverine.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Messaging.Wolverine.Postgres.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    private const string FakeConnectionString = "Host=localhost;Database=test;Username=t;Password=t";

    [Fact]
    public void Durable_store_wins_when_registered_after_AddKotoWolverine()
    {
        var services = new ServiceCollection();
        services.AddKotoWolverine();
        services.AddPostgresProcessedMessageStore(FakeConnectionString);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IProcessedMessageStore>()
            .Should().BeOfType<PostgresProcessedMessageStore>();
    }

    [Fact]
    public void Durable_store_wins_when_registered_before_AddKotoWolverine()
    {
        var services = new ServiceCollection();
        services.AddPostgresProcessedMessageStore(FakeConnectionString);
        services.AddKotoWolverine();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IProcessedMessageStore>()
            .Should().BeOfType<PostgresProcessedMessageStore>();
    }

    [Fact]
    public void Invalid_table_name_fails_fast_at_registration()
    {
        var services = new ServiceCollection();

        var act = () => services.AddPostgresProcessedMessageStore(
            FakeConnectionString, o => o.Table = "bad-name");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_connection_string_is_rejected()
    {
        var services = new ServiceCollection();

        var act = () => services.AddPostgresProcessedMessageStore("  ");

        act.Should().Throw<ArgumentException>();
    }
}
