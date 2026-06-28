using System.Security.Claims;

namespace Koto.Api.FastEndpoints.Extensions;

/// <summary>Convenience accessors for common claims on the current <see cref="ClaimsPrincipal"/>.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the user id from the <see cref="ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The claim is absent or its value is not a valid <see cref="Guid"/>.
    /// </exception>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No NameIdentifier claim on the current principal.");
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"NameIdentifier claim '{value}' is not a valid GUID.");
    }

    /// <summary>
    /// Attempts to read the user id from the <see cref="ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    /// <param name="user">The principal to inspect.</param>
    /// <param name="userId">The parsed user id when present and valid; otherwise <see cref="Guid.Empty"/>.</param>
    /// <returns><c>true</c> when the claim is present and a valid <see cref="Guid"/>.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
