using System.Reflection;
using FluentValidation;
using Koto.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Koto.Validation;

/// <summary>DI registration helpers for <c>Koto.Validation</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans <paramref name="assemblies"/> for all <see cref="IValidator{T}"/> implementations and
    /// registers <see cref="ValidationBehavior{TRequest,TResponse}"/> unless it was already added
    /// through <see cref="KotoApplicationOptionsExtensions.AddValidationBehavior"/> (which controls
    /// its position in the behavior chain). Call <c>AddKotoApplication</c> first: whichever call comes
    /// first fixes the behavior's position, and the behavior is never registered twice.
    /// </summary>
    public static IServiceCollection AddKotoValidation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        foreach (var assembly in assemblies)
        {
            foreach (var type in AssemblyScanning.GetLoadableTypes(assembly)
                         .Where(t => !t.IsAbstract && !t.IsInterface))
            {
                foreach (var iface in type.GetInterfaces().Where(IsValidatorInterface))
                    services.AddTransient(iface, type);
            }
        }

        return services;
    }

    private static bool IsValidatorInterface(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IValidator<>);
}
