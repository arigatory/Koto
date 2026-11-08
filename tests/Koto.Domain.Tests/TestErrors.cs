namespace Koto.Domain.Tests;

/// <summary>Test-only error with a dynamic code; production code uses catalog records.</summary>
public sealed record TestError(string Code, string Message) : Error(Code, Message);
