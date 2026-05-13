namespace Koto.Infrastructure.Http;

/// <summary>
/// Delegating handler that appends the current correlation ID as an
/// <c>X-Correlation-ID</c> request header.
/// </summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _accessor;

    /// <summary>Initializes a new <see cref="CorrelationIdHandler"/>.</summary>
    public CorrelationIdHandler(ICorrelationIdAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_accessor.GetCorrelationId() is { } correlationId)
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
