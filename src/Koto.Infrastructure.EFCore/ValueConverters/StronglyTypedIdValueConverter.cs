using Koto.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Koto.Infrastructure.EFCore.ValueConverters;

/// <summary>
/// EF Core <see cref="ValueConverter"/> for <see cref="StronglyTypedId{T}"/> subclasses.
/// Uses <c>Activator.CreateInstance</c> to reconstruct the ID from its raw value.
/// </summary>
public sealed class StronglyTypedIdValueConverter<TId, TRaw> : ValueConverter<TId, TRaw>
    where TId : StronglyTypedId<TRaw>
    where TRaw : notnull, IComparable<TRaw>
{
    /// <summary>Initializes a new converter.</summary>
    public StronglyTypedIdValueConverter()
        : base(
            id => id.Value,
            raw => (TId)Activator.CreateInstance(typeof(TId), raw)!)
    {
    }
}
