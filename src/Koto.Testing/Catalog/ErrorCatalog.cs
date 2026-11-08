using System.Reflection;
using Koto.Application;
using Koto.Domain;

namespace Koto.Testing.Catalog;

/// <summary>One error record found in the catalog: its type, code, a sample message and the keys it contributes to <c>details</c>.</summary>
/// <param name="Type">The error record type.</param>
/// <param name="Code">Its error code.</param>
/// <param name="Message">A sample message (built from stub constructor arguments).</param>
/// <param name="DetailKeys">The camelCase keys of its details, i.e. the data contract clients can rely on.</param>
public sealed record ErrorCatalogEntry(Type Type, string Code, string Message, IReadOnlyList<string> DetailKeys);

/// <summary>
/// Reads the error catalog of your assemblies — every concrete record deriving from <see cref="Error"/> —
/// by instantiating each one with stub constructor arguments. Use <see cref="ShouldBeValid"/> as a unit
/// test: codes follow the format, are unique across types, and every record can be constructed.
/// </summary>
public static class ErrorCatalog
{
    /// <summary>Scans <paramref name="assemblies"/> and returns one entry per concrete error record.</summary>
    public static IReadOnlyList<ErrorCatalogEntry> Scan(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var entries = new List<ErrorCatalogEntry>();
        foreach (var type in ErrorTypes(assemblies))
        {
            var instance = Instantiate(type);
            entries.Add(new ErrorCatalogEntry(type, instance.Code, instance.Message, ErrorDetails.Of(instance).Keys.ToArray()));
        }

        return entries;
    }

    /// <summary>
    /// Fails with one <see cref="InvalidOperationException"/> listing every problem: a record whose
    /// constructor throws, a code that does not match <see cref="ErrorCode.Pattern"/>, or a code shared
    /// by several record types.
    /// </summary>
    public static void ShouldBeValid(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var problems = new List<string>();
        var byCode = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

        foreach (var type in ErrorTypes(assemblies))
        {
            Error instance;
            try
            {
                instance = Instantiate(type);
            }
            catch (InvalidOperationException ex)
            {
                problems.Add($"{type.FullName}: cannot be constructed ({ex.Message})");
                continue;
            }

            if (!ErrorCode.IsValid(instance.Code))
                problems.Add($"{type.FullName}: code '{instance.Code}' does not match {ErrorCode.Pattern}");

            if (!byCode.TryGetValue(instance.Code, out var types))
                byCode[instance.Code] = types = [];
            types.Add(type);
        }

        foreach (var (code, types) in byCode.Where(kv => kv.Value.Count > 1))
            problems.Add($"code '{code}' is defined by {types.Count} types: {string.Join(", ", types.Select(t => t.Name))}");

        if (problems.Count > 0)
            throw new InvalidOperationException("Error catalog problems:\n - " + string.Join("\n - ", problems));
    }

    /// <summary>Stand-in for abstract error parameters (e.g. the <c>Inner</c> of an enriched record).</summary>
    private sealed record StubError() : Error("koto.catalog.stub", "Stub error.");

    // The generic EnrichedError wrapper is not a catalog entry (its code comes from the inner error);
    // typed records deriving from it are.
    private static IEnumerable<Type> ErrorTypes(IEnumerable<Assembly> assemblies) =>
        assemblies
            .SelectMany(AssemblyScanning.GetLoadableTypes)
            .Where(t => typeof(Error).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                        && t != typeof(EnrichedError) && t != typeof(StubError))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static Error Instantiate(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => !c.IsPrivate || type.IsNested)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault(c => !c.GetParameters().Any(p => p.ParameterType == type))
            ?? throw new InvalidOperationException($"{type.FullName} has no usable constructor.");
        var arguments = constructor.GetParameters().Select(p => Stub(p.ParameterType, p.Name)).ToArray();
        try
        {
            return (Error)constructor.Invoke(arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"{type.FullName} could not be constructed with stub arguments: {ex.InnerException.Message}", ex.InnerException);
        }
    }

    private static object? Stub(Type type, string? parameterName)
    {
        // A parameter literally named "code" on an error record is its error code: give it a valid one.
        if (type == typeof(string))
            return string.Equals(parameterName, "code", StringComparison.OrdinalIgnoreCase) ? "stub.error.code" : "x";
        if (type == typeof(Error)) return new StubError();
        if (type == typeof(EnrichedError)) return new EnrichedError(new StubError());
        if (type == typeof(bool)) return false;
        if (type == typeof(Guid)) return Guid.Empty;
        if (type == typeof(DateTime)) return DateTime.UnixEpoch;
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.UnixEpoch;
        if (type == typeof(TimeSpan)) return TimeSpan.Zero;
        if (type.IsEnum) return Enum.GetValues(type).Cast<object>().FirstOrDefault() ?? Activator.CreateInstance(type);
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(decimal) || type == typeof(double) || type == typeof(float))
        {
            return Convert.ChangeType(1, type, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (typeof(Error).IsAssignableFrom(type) && !type.IsAbstract) return Instantiate(type);
        if (Nullable.GetUnderlyingType(type) is not null || !type.IsValueType) return null;
        return Activator.CreateInstance(type);
    }
}
