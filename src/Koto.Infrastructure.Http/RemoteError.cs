using Koto.Application;
using Koto.Domain;

namespace Koto.Infrastructure.Http;

/// <summary>
/// An error returned by another Koto service inside its <see cref="Envelope{T}"/>. Carries the remote
/// code, message, field and details as data; the calling service's handler enriches it like any other error.
/// A remote code that does not follow <see cref="ErrorCode.Pattern"/> becomes
/// <c>general.remote.unknown-error</c> with the original code in <c>details.remoteCode</c>.
/// </summary>
public sealed record RemoteError : Error
{
    /// <summary>Code used when the remote code is malformed.</summary>
    public const string UnknownRemoteCode = "general.remote.unknown-error";

    /// <summary>Creates a remote error from its wire parts.</summary>
    public RemoteError(string code, string message, string? field, IReadOnlyDictionary<string, object?> details)
        : base(ErrorCode.IsValid(code) ? code : UnknownRemoteCode, message)
    {
        ArgumentNullException.ThrowIfNull(details);
        Field = field;
        Details = ErrorCode.IsValid(code)
            ? details
            : new Dictionary<string, object?>(details, StringComparer.Ordinal) { ["remoteCode"] = code };
    }

    /// <summary>The field the remote service bound the error to, if any.</summary>
    public string? Field { get; }

    /// <summary>The remote <c>details</c> (values are <see cref="System.Text.Json.JsonElement"/>s when read from JSON).</summary>
    public IReadOnlyDictionary<string, object?> Details { get; }

    /// <summary>Maps a wire error to a <see cref="RemoteError"/>.</summary>
    public static RemoteError From(EnvelopeError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RemoteError(error.Code, error.Message, error.Field, error.Details);
    }
}
