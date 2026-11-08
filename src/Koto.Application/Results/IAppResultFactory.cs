namespace Koto.Application;

/// <summary>
/// Compile-time factory contract for application results: lets generic pipeline behaviors
/// (e.g. validation) construct a failed <typeparamref name="TSelf"/> without reflection.
/// </summary>
/// <typeparam name="TSelf">The implementing result type.</typeparam>
public interface IAppResultFactory<TSelf>
    where TSelf : Koto.Domain.IResultBase, IAppResultFactory<TSelf>
{
    /// <summary>Creates a failed instance carrying the given <paramref name="errors"/>.</summary>
    static abstract TSelf FromErrors(IReadOnlyList<EnrichedError> errors);
}
