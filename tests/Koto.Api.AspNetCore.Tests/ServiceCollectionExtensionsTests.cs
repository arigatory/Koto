using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKotoAspNetCore_registers_options_time_provider_and_guard()
    {
        var services = new ServiceCollection();
        services.AddKotoAspNetCore(o => o.FallbackStatusCode = 418);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<KotoApiOptions>>().Value.FallbackStatusCode.Should().Be(418);
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
        provider.GetRequiredService<EnrichmentGuard>().Should().NotBeNull();
    }

    [Fact]
    public void AddKotoAspNetCore_keeps_a_time_provider_registered_before_it()
    {
        var fixedClock = new FixedTimeProvider(FixedTimeProvider.Default);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(fixedClock);
        services.AddKotoAspNetCore();

        services.BuildServiceProvider().GetRequiredService<TimeProvider>().Should().BeSameAs(fixedClock);
    }

    [Fact]
    public void ServerError_has_the_internal_error_code()
    {
        new ServerError().Code.Should().Be("general.server.internal-error");
    }
}
