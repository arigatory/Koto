using Koto.Domain;

namespace Koto.Api.AspNetCore;

/// <summary>The error sent for an unhandled exception (HTTP 500). Details are never exposed to clients.</summary>
public sealed record ServerError() : Error("general.server.internal-error", "An unexpected error occurred.");
