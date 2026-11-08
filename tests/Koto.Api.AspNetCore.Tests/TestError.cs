using Koto.Domain;

namespace Koto.Api.AspNetCore.Tests;

/// <summary>Test-only error with a dynamic code; production code uses catalog records.</summary>
public sealed record TestError(string Code, string Message) : Error(Code, Message);

/// <summary>Deterministic clock for envelope timestamps.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 11, 8, 2, 11, 3, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => now;
}
