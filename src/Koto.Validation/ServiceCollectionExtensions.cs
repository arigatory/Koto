using System.Reflection;
using FluentValidation;
using Koto.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Validation;

/// <summary>DI registration helpers for <c>Koto.Validation</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ValidationBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> and scans <paramref name="assemblies"/>
    /// for all <see cref="IValidator{T}"/> implementations.
    /// </summary>
    public static IServiceCollection AddKotoValidation(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
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
