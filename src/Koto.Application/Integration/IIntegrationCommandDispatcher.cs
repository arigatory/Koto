namespace Koto.Application;

/// <summary>
/// Sends integration commands to other services (fire-and-forget or request/reply).
/// Implemented by <c>Koto.Messaging.Wolverine</c>.
/// </summary>
public interface IIntegrationCommandDispatcher
{
    /// <summary>Sends a fire-and-forget command. No response is awaited.</summary>
    Task SendAsync(IIntegrationCommand command, CancellationToken ct = default);

    /// <summary>Sends a request/reply command and awaits the response.</summary>
    Task<TResult> SendAsync<TResult>(IIntegrationCommand<TResult> command, CancellationToken ct = default);
}
