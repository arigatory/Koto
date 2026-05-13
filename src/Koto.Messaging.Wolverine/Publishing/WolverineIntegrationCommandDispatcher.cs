using Koto.Application;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Koto.Messaging.Wolverine.Publishing;

/// <summary>
/// Dispatches integration commands via the Wolverine message bus.
/// Fire-and-forget commands are routed to the configured Kafka topic.
/// Request/reply commands use Wolverine's local invoke; configure the reply endpoint in WolverineOptions.
/// </summary>
public sealed class WolverineIntegrationCommandDispatcher : IIntegrationCommandDispatcher
{
    private readonly IMessageBus _bus;
    private readonly KotoWolverineOptions _options;

    /// <summary>Initializes a new <see cref="WolverineIntegrationCommandDispatcher"/>.</summary>
    public WolverineIntegrationCommandDispatcher(IMessageBus bus, IOptions<KotoWolverineOptions> options)
    {
        _bus = bus;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task SendAsync(IIntegrationCommand command, CancellationToken ct = default)
        => await _bus.SendAsync(command);

    /// <inheritdoc/>
    public Task<TResult> SendAsync<TResult>(IIntegrationCommand<TResult> command, CancellationToken ct = default)
        => _bus.InvokeAsync<TResult>(command, ct, _options.RequestReplyTimeout);
}
