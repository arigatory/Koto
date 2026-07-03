namespace Koto.Domain;

/// <summary>
/// Non-generic view over a <see cref="Result{T}"/>. Lets pipeline behaviors and other
/// infrastructure inspect success/failure without knowing the value type.
/// </summary>
/// <remarks>
/// Named <c>IResultBase</c> rather than <c>IResult</c> to avoid colliding with
/// <c>Microsoft.AspNetCore.Http.IResult</c> in API-layer packages.
/// </remarks>
public interface IResultBase
{
    /// <summary><c>true</c> when the operation succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary><c>true</c> when the operation failed.</summary>
    bool IsFailure { get; }

    /// <summary>All errors carried by a failed result; empty on success.</summary>
    IReadOnlyList<Error> Errors { get; }
}
