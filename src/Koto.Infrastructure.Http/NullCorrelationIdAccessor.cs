namespace Koto.Infrastructure.Http;

/// <summary>
/// Дефолтная реализация: корреляции нет (заголовок не добавляется).
/// Регистрируется через TryAdd в <c>AddServiceHttpClient</c> — приложение может
/// подменить своей (например мостом к Koto.Api.AspNetCore CorrelationContext).
/// </summary>
public sealed class NullCorrelationIdAccessor : ICorrelationIdAccessor
{
    /// <inheritdoc/>
    public string? GetCorrelationId() => null;
}
