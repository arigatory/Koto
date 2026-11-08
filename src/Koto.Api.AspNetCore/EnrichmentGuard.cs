using System.Reflection;
using Koto.Application;
using Koto.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore;

/// <summary>
/// Second-level guarantee for error contracts. The compiler guarantees the data of the error type a
/// handler constructs; this guard checks, at the API boundary, that a handler did not forget the
/// fuller type: when the scanned assemblies contain a record deriving from <see cref="EnrichedError"/>
/// whose constructor takes an error of type <c>X</c> (e.g. <c>MeetupFullWithAlternative(MeetupFull Cause, …)</c>),
/// a bare <c>X</c> reaching the response is a violation. Throws in Development, warns elsewhere
/// (<see cref="KotoApiOptions.EnrichmentGuard"/> overrides).
/// <para>
/// The catalog is built from constructor parameters: <em>every</em> <see cref="Error"/>-typed parameter
/// of a record deriving from <see cref="EnrichedError"/> marks that parameter type as "incomplete on the
/// wire". Several fuller records may share a cause; any of them satisfies the guard. Avoid giving such
/// records error-typed parameters that are not the wrapped cause.
/// </para>
/// </summary>
public sealed class EnrichmentGuard
{
    private readonly Lazy<Dictionary<Type, List<Type>>> _fullerTypes;
    private readonly ILogger<EnrichmentGuard> _logger;

    /// <summary>Creates the guard over the assemblies registered by <c>AddKotoApplication</c>.</summary>
    public EnrichmentGuard(
        IOptions<KotoApiOptions> options,
        KotoApplicationAssemblies? assemblies = null,
        IHostEnvironment? environment = null,
        ILogger<EnrichmentGuard>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? NullLogger<EnrichmentGuard>.Instance;
        Mode = assemblies is null
            ? EnrichmentGuardMode.Off
            : options.Value.EnrichmentGuard
              ?? (environment?.IsDevelopment() == true ? EnrichmentGuardMode.Throw : EnrichmentGuardMode.Warn);
        _fullerTypes = new Lazy<Dictionary<Type, List<Type>>>(() => Build(assemblies?.Assemblies ?? []));
    }

    /// <summary>The effective mode.</summary>
    public EnrichmentGuardMode Mode { get; }

    /// <summary>Checks every error; see the class remarks for what counts as a violation.</summary>
    /// <exception cref="InvalidOperationException">A violation in <see cref="EnrichmentGuardMode.Throw"/> mode.</exception>
    public void Check(IReadOnlyList<EnrichedError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (Mode == EnrichmentGuardMode.Off || _fullerTypes.Value.Count == 0)
            return;

        foreach (var error in errors)
        {
            var fullerTypes = FindFullerTypes(error.Inner.GetType());
            if (fullerTypes is null || fullerTypes.Any(t => t.IsInstanceOfType(error)))
                continue;

            var names = string.Join(" / ", fullerTypes.Select(t => t.Name));
            var message =
                $"Error '{error.Inner.GetType().Name}' ({error.Code}) reached the API without enrichment: " +
                $"'{names}' adds data clients rely on. Enrich it in the handler, e.g. " +
                $"errors.Enrich(e => e is {error.Inner.GetType().Name} x ? new {fullerTypes[0].Name}(x, …) : e.Enrich()).";
            if (Mode == EnrichmentGuardMode.Throw)
                throw new InvalidOperationException(message);
            _logger.LogWarning("{Message}", message);
        }
    }

    private List<Type>? FindFullerTypes(Type errorType)
    {
        for (var type = errorType; type is not null && type != typeof(Error); type = type.BaseType)
        {
            if (_fullerTypes.Value.TryGetValue(type, out var fuller))
                return fuller;
        }

        return null;
    }

    private static Dictionary<Type, List<Type>> Build(IEnumerable<Assembly> assemblies)
    {
        var map = new Dictionary<Type, List<Type>>();
        foreach (var type in assemblies.SelectMany(AssemblyScanning.GetLoadableTypes))
        {
            if (type.IsAbstract || type == typeof(EnrichedError) || !typeof(EnrichedError).IsAssignableFrom(type))
                continue;

            foreach (var parameter in type.GetConstructors().SelectMany(c => c.GetParameters()))
            {
                var parameterType = parameter.ParameterType;
                if (parameterType != typeof(Error) && parameterType != typeof(EnrichedError)
                    && typeof(Error).IsAssignableFrom(parameterType))
                {
                    if (!map.TryGetValue(parameterType, out var list))
                        map[parameterType] = list = [];
                    if (!list.Contains(type))
                        list.Add(type);
                }
            }
        }

        return map;
    }
}
