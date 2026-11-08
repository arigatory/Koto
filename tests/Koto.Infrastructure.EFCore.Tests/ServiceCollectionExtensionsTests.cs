using AwesomeAssertions;
using Koto.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Infrastructure.EFCore.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKotoEFCore_makes_generic_IRepository_resolvable()
    {
        var services = new ServiceCollection();
        services.AddKotoEFCore<TestDbContext>(o => o.UseNpgsql("Host=localhost;Database=koto;Username=u;Password=p"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRepository<TestAggregate, TestId>>();

        act.Should().NotThrow();
    }
}
