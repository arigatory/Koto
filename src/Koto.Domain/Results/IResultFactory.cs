namespace Koto.Domain;

/// <summary>
/// Compile-time factory contract for result types: lets generic infrastructure
/// (e.g. validation pipeline behaviors) construct a failed <typeparamref name="TSelf"/>
/// without reflection.
/// </summary>
/// <typeparam name="TSelf">The implementing result type.</typeparam>
public interface IResultFactory<TSelf> where TSelf : IResultBase, IResultFactory<TSelf>
{
    /// <summary>Creates a failed instance carrying the given <paramref name="errors"/>.</summary>
    static abstract TSelf FromErrors(IReadOnlyList<Error> errors);
}
