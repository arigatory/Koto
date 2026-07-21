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
        RegisterStronglyTypedIdConversions(configurationBuilder);
    }

    /// <summary>
    /// Pre-convention регистрация конвертеров для ВСЕХ StronglyTypedId-типов из сборок
    /// доменных сущностей контекста. Без этого EF discovery принимает не-ключевые ссылки
    /// (например <c>Order.CustomerId</c>) за навигации к сущностям — finalizing-конвенция
    /// срабатывает слишком поздно (ревизия ADR-023).
    /// </summary>
    private void RegisterStronglyTypedIdConversions(ModelConfigurationBuilder configurationBuilder)
    {
        var entityAssemblies = GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0].Assembly)
            .Distinct();

        foreach (var assembly in entityAssemblies)
        {
            foreach (var idType in SafeGetTypes(assembly))
            {
                if (idType.IsAbstract || !TryGetStronglyTypedIdRawType(idType, out var rawType))
                    continue;

                var converterType = typeof(ValueConverters.StronglyTypedIdValueConverter<,>)
                    .MakeGenericType(idType, rawType);
                configurationBuilder.Properties(idType).HaveConversion(converterType);
            }
        }
    }

    private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static bool TryGetStronglyTypedIdRawType(
        Type type,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? rawType)
    {
        rawType = null;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(StronglyTypedId<>))
            {
                rawType = baseType.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }
}
