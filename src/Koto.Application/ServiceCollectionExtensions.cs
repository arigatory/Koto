using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application;

/// <summary>
/// Configures which <see cref="IPipelineBehavior{TRequest,TResponse}"/>s wrap every
/// command/query. Behaviors execute in the order they are added: the first added
/// behavior is outermost. Recommended order: Logging → Validation → Transaction.
/// </summary>
public sealed class KotoApplicationOptions
{
    internal List<Type> BehaviorTypes { get; } = [];

    /// <summary>
    /// When <c>true</c> (default), <c>AddKotoApplication</c> fails fast if a command or query in the
    /// scanned assemblies has no handler there, or has more than one. Disable only when handlers are
    /// registered by hand or live in assemblies that are not scanned.
    /// </summary>
    public bool ValidateHandlers { get; set; } = true;

    /// <summary>
    /// Adds an open-generic pipeline behavior, e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>.
    /// The container closes it over each concrete command/query type at dispatch time.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="openGenericBehaviorType"/> does not implement <see cref="IPipelineBehavior{TRequest,TResponse}"/>.</exception>
    public KotoApplicationOptions AddBehavior(Type openGenericBehaviorType)
    {
        ArgumentNullException.ThrowIfNull(openGenericBehaviorType);
        var implementsBehavior = openGenericBehaviorType
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));
        if (!implementsBehavior)
            throw new ArgumentException(
                $"{openGenericBehaviorType.Name} must implement IPipelineBehavior<,>.",
                nameof(openGenericBehaviorType));
        BehaviorTypes.Add(openGenericBehaviorType);
        return this;
    }

    /// <summary>Adds <see cref="LoggingBehavior{TRequest,TResponse}"/> (request name, elapsed time, outcome).</summary>
    public KotoApplicationOptions AddLoggingBehavior() => AddBehavior(typeof(LoggingBehavior<,>));

    /// <summary>
    /// Adds <see cref="TransactionBehavior{TRequest,TResponse}"/> (wraps commands in an
    /// <see cref="IUnitOfWork"/> transaction; rolls back on failure results).
    /// Requires an <see cref="IUnitOfWork"/> registration.
    /// </summary>
    public KotoApplicationOptions AddTransactionBehavior() => AddBehavior(typeof(TransactionBehavior<,>));
}

/// <summary>DI registration helpers for <c>Koto.Application</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CqrsDispatcher"/> as <see cref="ICqrsDispatcher"/>, scans
    /// <paramref name="assemblies"/> for all command and query handlers and fails fast when a
    /// command or query has no handler. No pipeline behaviors are registered — use the
    /// <see cref="AddKotoApplication(IServiceCollection, Action{KotoApplicationOptions}, Assembly[])"/>
    /// overload to opt in.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for handler implementations.</param>
    public static IServiceCollection AddKotoApplication(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.AddKotoApplication(_ => { }, assemblies);

    /// <summary>
    /// Registers <see cref="CqrsDispatcher"/>, scans <paramref name="assemblies"/> for handlers,
    /// registers the pipeline behaviors configured via <paramref name="configure"/> and, unless
    /// <see cref="KotoApplicationOptions.ValidateHandlers"/> is disabled, fails fast when a command or
    /// query in those assemblies has no handler or more than one.
    /// Behaviors execute in the order added (first = outermost); they are registered as
    /// open generics and close over the concrete command/query type at dispatch time.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures pipeline behaviors, e.g. <c>o =&gt; o.AddLoggingBehavior().AddTransactionBehavior()</c>.</param>
    /// <param name="assemblies">Assemblies to scan for handler implementations.</param>
    /// <exception cref="InvalidOperationException">A scanned command or query has no handler, or several.</exception>
    public static IServiceCollection AddKotoApplication(
        this IServiceCollection services,
        Action<KotoApplicationOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(assemblies);

        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();
        RegisterAssemblies(services, assemblies);

        var options = new KotoApplicationOptions();
        configure(options);
        foreach (var behaviorType in options.BehaviorTypes)
            services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);

        var scannedTypes = assemblies
            .SelectMany(AssemblyScanning.GetLoadableTypes)
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToArray();

        foreach (var type in scannedTypes)
        {
            foreach (var iface in type.GetInterfaces().Where(IsHandlerInterface))
                services.AddTransient(iface, type);
        }

        if (options.ValidateHandlers)
        {
            var problems = HandlerRegistrationValidator.FindProblems(scannedTypes);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "Koto: every command and query needs exactly one handler in the scanned assemblies:\n - " +
                    string.Join("\n - ", problems) +
                    "\nRegister the missing handlers, or set KotoApplicationOptions.ValidateHandlers = false " +
                    "if they are registered by hand.");
            }
        }

        return services;
    }

    private static void RegisterAssemblies(IServiceCollection services, Assembly[] assemblies)
    {
        var registry = services
            .FirstOrDefault(d => d.ServiceType == typeof(KotoApplicationAssemblies))
            ?.ImplementationInstance as KotoApplicationAssemblies;
        if (registry is null)
        {
            registry = new KotoApplicationAssemblies();
            services.AddSingleton(registry);
        }

        registry.Add(assemblies);
    }

    private static bool IsHandlerInterface(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() is { } def &&
        (def == typeof(ICommandHandler<>) ||
         def == typeof(ICommandHandler<,>) ||
         def == typeof(IQueryHandler<,>));
}
