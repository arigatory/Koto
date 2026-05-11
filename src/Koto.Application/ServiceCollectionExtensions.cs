using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Application;

/// <summary>DI registration helpers for <c>Koto.Application</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CqrsDispatcher"/> as <see cref="ICqrsDispatcher"/> and scans
    /// <paramref name="assemblies"/> for all command and query handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for handler implementations.</param>
    public static IServiceCollection AddKotoApplication(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
            {
                foreach (var iface in type.GetInterfaces().Where(IsHandlerInterface))
                    services.AddTransient(iface, type);
            }
        }

        return services;
    }

    private static bool IsHandlerInterface(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() is { } def &&
        (def == typeof(ICommandHandler<>) ||
         def == typeof(ICommandHandler<,>) ||
         def == typeof(IQueryHandler<,>));
}
