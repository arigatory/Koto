namespace Koto.Application;

/// <summary>
/// Marker for fire-and-forget commands sent to another service via Kafka.
/// No response is expected.
/// </summary>
public interface IIntegrationCommand { }

/// <summary>
/// Marker for request/reply commands sent to another service.
/// A response of type <typeparamref name="TResult"/> is expected.
/// </summary>
/// <typeparam name="TResult">The expected response type.</typeparam>
public interface IIntegrationCommand<TResult> { }
