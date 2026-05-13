using Marten.Events.Aggregation;

namespace Koto.EventSourcing.Marten.Projections;

/// <summary>
/// Base for single-stream projections applied inline during the Marten session commit.
/// <typeparamref name="TId"/> is the stream identity type (e.g. <see cref="Guid"/>).
/// Register with:
/// <code>opts.Projections.Add&lt;TProjection&gt;(ProjectionLifecycle.Inline);</code>
/// </summary>
public abstract class InlineProjection<TReadModel, TId> : SingleStreamProjection<TReadModel, TId>
    where TReadModel : class, new()
    where TId : notnull
{
}
