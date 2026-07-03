using Koto.Domain;
using Koto.Infrastructure.EFCore.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Koto.Infrastructure.EFCore;

/// <summary>
/// Abstract EF Core DbContext base that:
/// <list type="bullet">
///   <item>Applies <see cref="StronglyTypedIdConvention"/> automatically.</item>
///   <item>Clears domain events from all tracked aggregates after each successful save.</item>
/// </list>
/// Domain event publishing is handled by Wolverine's built-in scraping. Configure it once in
/// your <c>WolverineOptions</c>:
/// <code>
/// opts.PublishDomainEventsFromEntityFrameworkCore&lt;IHasDomainEvents, IDomainEvent&gt;(
///     e => e.DomainEvents);
/// </code>
/// </summary>
public abstract class KotoDbContext : DbContext
{
    /// <summary>Initializes a new <see cref="KotoDbContext"/>.</summary>
    protected KotoDbContext(DbContextOptions options) : base(options) { }

    /// <inheritdoc/>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Wolverine has already scraped domain events into the outbox before this call.
        // Clear them to avoid re-publishing on subsequent saves.
        foreach (var entry in ChangeTracker.Entries<IHasDomainEvents>())
            entry.Entity.ClearDomainEvents();

        return result;
    }

    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Conventions.Add(_ => new StronglyTypedIdConvention());
    }
}
