using Koto.Application;
using Koto.Messaging.Wolverine.Middleware;
using Microsoft.Extensions.Logging;

namespace Koto.Messaging.Wolverine.Consuming;

/// <summary>
/// Base class for Kafka integration command consumers.
/// Provides structured logging and DLQ routing via exception re-throw.
/// </summary>
/// <typeparam name="TCommand">The integration command type to handle.</typeparam>
public abstract class IntegrationCommandConsumerBase<TCommand>
    where TCommand : IIntegrationCommand
{
    private readonly ILogger _logger;

    /// <summary>Initializes a new consumer with the required logger.</summary>
    protected IntegrationCommandConsumerBase(ILogger<IntegrationCommandConsumerBase<TCommand>> logger)
        => _logger = logger;

    /// <summary>
    /// Wolverine-discovered handler method.
    /// Delegates to <see cref="ExecuteAsync"/>; unhandled exceptions are re-thrown for DLQ routing.
    /// </summary>
    public async Task HandleAsync(TCommand command, CancellationToken ct)
    {
        var correlationId = CorrelationContext.CorrelationId.Value;

        try
        {
            _logger.LogInformation(
                "Handling command {CommandType} (CorrelationId: {CorrelationId})",
                typeof(TCommand).Name, correlationId);

            await ExecuteAsync(command, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle command {CommandType} (CorrelationId: {CorrelationId})",
                typeof(TCommand).Name, correlationId);
            throw;
        }
    }

    /// <summary>Implement domain-specific handling for <paramref name="command"/>.</summary>
    protected abstract Task ExecuteAsync(TCommand command, CancellationToken ct);
}
