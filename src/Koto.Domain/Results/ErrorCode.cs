using System.Text.RegularExpressions;

namespace Koto.Domain;

/// <summary>
/// Validates error codes. A code has at least three kebab-case segments separated by dots:
/// <c>context.entity.problem</c>, e.g. <c>orders.order.not-found</c> or <c>general.value.is-required</c>.
/// </summary>
public static partial class ErrorCode
{
    /// <summary>The accepted format: lowercase ASCII letters, digits and single hyphens; three or more dot-separated segments.</summary>
    public const string Pattern = @"^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*){2,}$";

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex Format();

    /// <summary>Returns <c>true</c> when <paramref name="code"/> matches <see cref="Pattern"/>.</summary>
    public static bool IsValid(string? code) => code is not null && Format().IsMatch(code);

    /// <summary>Returns <paramref name="code"/> when it matches <see cref="Pattern"/>; throws otherwise.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> does not match <see cref="Pattern"/>.</exception>
    public static string Validate(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (!Format().IsMatch(code))
        {
            throw new ArgumentException(
                $"Error code '{code}' must have at least three kebab-case segments separated by dots, " +
                "e.g. 'orders.order.not-found'.",
                nameof(code));
        }

        return code;
    }
}
