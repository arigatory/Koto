using System.Reflection;

namespace Koto.Application;

/// <summary>
/// Конвенция «тип ↔ топик 1:1»: каждый интеграционный контракт объявляет
/// <c>public const string Topic = "service.event-name";</c>. Единая точка резолва —
/// её используют и публикация/подписка (Koto.Messaging.Wolverine), и тестовые
/// продюсеры (Koto.Testing.Integration).
/// </summary>
public static class IntegrationEventTopics
{
    /// <summary>Топик контракта <typeparamref name="T"/> из его константы <c>Topic</c>.</summary>
    public static string For<T>()
        where T : IIntegrationEvent => For(typeof(T));

    /// <summary>Топик контракта из его константы <c>Topic</c>; отсутствие константы — ошибка (fail fast).</summary>
    public static string For(Type eventType)
    {
        var field = eventType.GetField(
            "Topic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field?.GetRawConstantValue() is not string topic || string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException(
                $"Integration event '{eventType.FullName}' must declare " +
                "'public const string Topic = \"service.event-name\";'");
        }

        return topic;
    }
}
