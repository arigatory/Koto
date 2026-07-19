using System.Text.RegularExpressions;

namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>Validates configurable SQL identifiers (schema/table names) against injection.</summary>
internal static partial class PostgresIdentifier
{
    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex Pattern();

    /// <summary>Throws <see cref="ArgumentException"/> when <paramref name="value"/> is not a safe identifier.</summary>
    public static void Validate(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 63 || !Pattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid PostgreSQL identifier. " +
                "Use lowercase letters, digits, and underscores; must not start with a digit (max 63 chars).",
                paramName);
        }
    }
}
