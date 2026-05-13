using Koto.Application;

namespace Koto.Testing.Fakes;

/// <summary>
/// In-memory implementation of <see cref="IIntegrationEventPublisher"/> for use in tests.
/// Accumulates published events so tests can assert on what was published.
/// </summary>
public sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly List<IIntegrationEvent> _published = [];

    /// <summary>All integration events published so far, in order.</summary>
    public IReadOnlyList<IIntegrationEvent> PublishedEvents => _published;

    /// <inheritdoc/>
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        _published.Add(integrationEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the first published event of type <typeparamref name="T"/>,
    /// or throws <see cref="InvalidOperationException"/> if none was published.
    /// </summary>
    public T GetPublishedEvent<T>() where T : IIntegrationEvent =>
        _published.OfType<T>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No integration event of type {typeof(T).Name} was published. " +
                $"Published events: [{string.Join(", ", _published.Select(e => e.GetType().Name))}]");

    /// <summary>Returns all published events of type <typeparamref name="T"/>.</summary>
    public IReadOnlyList<T> GetPublishedEvents<T>() where T : IIntegrationEvent =>
        _published.OfType<T>().ToList();

    /// <summary>Clears all recorded published events.</summary>
    public void Clear() => _published.Clear();
}
