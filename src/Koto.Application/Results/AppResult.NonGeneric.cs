using Koto.Domain;

namespace Koto.Application;

/// <summary>Companion helpers for <see cref="AppResult{T}"/>: void results (<see cref="AppResult{T}"/> of <see cref="Unit"/>).</summary>
public static class AppResult
{
    /// <summary>Creates a successful void result.</summary>
    public static AppResult<Unit> Success() => AppResult<Unit>.Success(Unit.Value);

    /// <summary>Creates a failed void result wrapping <paramref name="error"/>.</summary>
    public static AppResult<Unit> Failure(EnrichedError error) => AppResult<Unit>.Failure(error);

    /// <summary>Creates a failed void result carrying all of <paramref name="errors"/>.</summary>
    public static AppResult<Unit> Failure(IEnumerable<EnrichedError> errors) => AppResult<Unit>.Failure(errors);
}
