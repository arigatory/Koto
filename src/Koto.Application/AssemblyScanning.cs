using System.Reflection;

namespace Koto.Application;

/// <summary>Helpers for safe assembly scanning during DI registration.</summary>
public static class AssemblyScanning
{
    /// <summary>
    /// Returns all types that can be loaded from <paramref name="assembly"/>.
    /// Unlike <see cref="Assembly.GetTypes"/>, does not throw when some types fail to
    /// load (<see cref="ReflectionTypeLoadException"/>) — the loadable subset is returned.
    /// </summary>
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
