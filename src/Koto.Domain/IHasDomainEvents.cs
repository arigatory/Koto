namespace Koto.Domain;

/// <summary>
/// Allows infrastructure components (e.g. EF Core DbContext) to collect and clear
/// domain events without knowing the concrete aggregate type parameter.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Domain events raised since the last <see cref="ClearDomainEvents"/> call.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Removes all collected domain events.</summary>
    void ClearDomainEvents();
}
