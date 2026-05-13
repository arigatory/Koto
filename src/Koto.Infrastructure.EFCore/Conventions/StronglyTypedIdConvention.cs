using Koto.Domain;
using Koto.Infrastructure.EFCore.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Koto.Infrastructure.EFCore.Conventions;

/// <summary>
/// EF Core convention that automatically applies
/// <see cref="StronglyTypedIdValueConverter{TId,TRaw}"/> to any entity property
/// whose type derives from <see cref="StronglyTypedId{T}"/>.
/// </summary>
public sealed class StronglyTypedIdConvention : IModelFinalizingConvention
{
    /// <inheritdoc/>
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (!TryGetRawType(property.ClrType, out var rawType)) continue;

            var converterType = typeof(StronglyTypedIdValueConverter<,>)
                .MakeGenericType(property.ClrType, rawType);

            property.SetValueConverter(
                (ValueConverter)Activator.CreateInstance(converterType)!);
        }
    }

    private static bool TryGetRawType(Type type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? rawType)
    {
        rawType = null;
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition() == typeof(StronglyTypedId<>))
            {
                rawType = baseType.GetGenericArguments()[0];
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }
}
