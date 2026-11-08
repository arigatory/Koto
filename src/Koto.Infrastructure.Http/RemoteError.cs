using Koto.Application;
using Koto.Domain;

namespace Koto.Infrastructure.Http;

/// <summary>The remote error itself: the code and message another Koto service sent.</summary>
/// <param name="Code">The remote error code.</param>
/// <param name="Message">The remote message.</param>
public sealed record RemoteFailure(string Code, string Message) : Error(Code, Message);

/// <summary>
/// An error returned by another Koto service inside its <see cref="Envelope{T}"/>, already enriched:
/// the remote <c>field</c> and <c>details</c> travel as <see cref="EnrichedError.Field"/> and
/// <see cref="EnrichedError.Details"/>, so <c>Enrich()</c> passes them through unchanged and the inner
/// <see cref="RemoteFailure"/> keeps the remote code. A remote code that does not follow
/// <see cref="ErrorCode.Pattern"/> becomes <c>general.remote.unknown-error</c> with the original code in
/// <c>details.remoteCode</c>.
/// </summary>
public sealed record RemoteError : EnrichedError
{
    /// <summary>Code used when the remote code is malformed.</summary>
    public const string UnknownRemoteCode = "general.remote.unknown-error";

    /// <summary>Creates a remote error from its wire parts.</summary>
    public RemoteError(string code, string message, string? field, IReadOnlyDictionary<string, object?>? details)
        : base(new RemoteFailure(ErrorCode.IsValid(code) ? code : UnknownRemoteCode, message))
    {
        var remoteDetails = details ?? new Dictionary<string, object?>(0, StringComparer.Ordinal);
        Field = field;
        Details = ErrorCode.IsValid(code)
            ? remoteDetails
            : new Dictionary<string, object?>(remoteDetails, StringComparer.Ordinal) { ["remoteCode"] = code };
    }

    /// <summary>Maps a wire error to a <see cref="RemoteError"/>.</summary>
    public static RemoteError From(EnvelopeError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RemoteError(error.Code, error.Message, error.Field, error.Details);
    }
}
