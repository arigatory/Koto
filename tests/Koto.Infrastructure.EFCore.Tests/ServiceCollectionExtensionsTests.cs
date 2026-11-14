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

    [Fact]
    public void AddKotoEFCoreWithoutOutbox_registers_context_repository_and_unit_of_work()
    {
        var services = new ServiceCollection();
        services.AddKotoEFCoreWithoutOutbox<TestDbContext>(o => o.UseNpgsql("Host=localhost;Database=koto;Username=u;Password=p"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<TestDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<KotoDbContext>()
            .Should().BeSameAs(scope.ServiceProvider.GetRequiredService<TestDbContext>());
        scope.ServiceProvider.GetRequiredService<IRepository<TestAggregate, TestId>>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Should().BeOfType<EfCoreUnitOfWork<TestDbContext>>();
    }

    [Fact]
    public void AddKotoEFCoreWithoutOutbox_keeps_a_unit_of_work_registered_by_the_consumer()
    {
        var services = new ServiceCollection();
        var own = NSubstitute.Substitute.For<IUnitOfWork>();
        services.AddScoped(_ => own);
        services.AddKotoEFCoreWithoutOutbox<TestDbContext>(o => o.UseNpgsql("Host=localhost;Database=koto;Username=u;Password=p"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Should().BeSameAs(own);
    }

    [Fact]
    public void AddKotoEFCoreWithoutOutbox_does_not_register_the_wolverine_outbox()
    {
        var services = new ServiceCollection();
        services.AddKotoEFCoreWithoutOutbox<TestDbContext>(o => o.UseNpgsql("Host=localhost;Database=koto;Username=u;Password=p"));

        services.Should().NotContain(d => d.ServiceType == typeof(Wolverine.EntityFrameworkCore.IDbContextOutbox));
    }
}
