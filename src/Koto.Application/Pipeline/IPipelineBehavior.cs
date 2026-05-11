namespace Koto.Application;

/// <summary>
/// Defines a middleware step in the CQRS pipeline. Behaviors are executed in registration
/// order (outermost first). Register as open generic:
/// <c>services.AddTransient(typeof(IPipelineBehavior&lt;,&gt;), typeof(LoggingBehavior&lt;,&gt;))</c>
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The response type (typically <c>Result&lt;T&gt;</c>).</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
{
    /// <summary>Processes <paramref name="request"/> and invokes <paramref name="next"/> to continue the pipeline.</summary>
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}
