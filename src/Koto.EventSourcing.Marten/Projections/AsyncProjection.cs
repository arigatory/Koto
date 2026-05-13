using Marten.Events.Aggregation;

namespace Koto.EventSourcing.Marten.Projections;

/// <summary>
/// Base for single-stream projections processed by the Marten Async Daemon.
/// <typeparamref name="TId"/> is the stream identity type (e.g. <see cref="Guid"/>).
/// Register with:
/// <code>opts.Projections.Add&lt;TProjection&gt;(ProjectionLifecycle.Async);</code>
/// </summary>
public abstract class AsyncProjection<TReadModel, TId> : SingleStreamProjection<TReadModel, TId>
    where TReadModel : class, new()
    where TId : notnull
{
}
