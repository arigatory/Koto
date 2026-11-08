using Koto.Application;

namespace Koto.Validation;

/// <summary>Registers the validation pipeline behavior at an explicit position in the behavior chain.</summary>
public static class KotoApplicationOptionsExtensions
{
    /// <summary>
    /// Adds <see cref="ValidationBehavior{TRequest,TResponse}"/> at this position:
    /// <c>o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior()</c>.
    /// Pair with <c>AddKotoValidation(assemblies)</c>, which then only scans validators.
    /// </summary>
    public static KotoApplicationOptions AddValidationBehavior(this KotoApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.AddBehavior(typeof(ValidationBehavior<,>));
    }
}
