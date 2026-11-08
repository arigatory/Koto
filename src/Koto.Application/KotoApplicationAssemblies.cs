using System.Reflection;

namespace Koto.Application;

/// <summary>
/// The assemblies scanned by <c>AddKotoApplication</c> (handlers, commands, queries and the error
/// catalog live there). Registered as a singleton so other Koto packages — e.g. the API layer's
/// error catalog checks — can inspect the same assemblies without a second configuration point.
/// </summary>
public sealed class KotoApplicationAssemblies
{
    private readonly List<Assembly> _assemblies = [];

    /// <summary>All scanned assemblies, in registration order, without duplicates.</summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    internal void Add(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            if (!_assemblies.Contains(assembly))
                _assemblies.Add(assembly);
        }
    }
}
