using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Infrastructure.EFCore.Tests;

/// <summary>Behaviour of the default unit of work when Wolverine's outbox is not registered.</summary>
public sealed class EfCoreUnitOfWorkTests
{
    private static ParcelDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ParcelDbContext>()
            .UseNpgsql("Host=localhost;Database=koto_never_opened;Username=u;Password=p")
            .Options);

    [Fact]
    public async Task CommitAsync_throws_when_events_were_raised_but_no_outbox_is_registered()
    {
        await using var context = NewContext();
        context.Parcels.Add(Parcel.Ship());                       // raises ParcelShippedDomainEvent
        var unitOfWork = new EfCoreUnitOfWork<ParcelDbContext>(context, new ServiceCollection().BuildServiceProvider());

        var act = () => unitOfWork.CommitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IDbContextOutbox*")
            .WithMessage($"*{nameof(ParcelShippedDomainEvent)}*")
            .WithMessage("*IUnitOfWork*");
    }

    [Fact]
    public async Task CommitAsync_without_events_and_without_outbox_does_not_throw()
    {
        await using var context = NewContext();
        var unitOfWork = new EfCoreUnitOfWork<ParcelDbContext>(context, new ServiceCollection().BuildServiceProvider());

        var act = () => unitOfWork.CommitAsync();

        await act.Should().NotThrowAsync();                         // nothing tracked: no connection is opened
    }
}
