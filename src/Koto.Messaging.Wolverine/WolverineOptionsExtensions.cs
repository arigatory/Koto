using System.Reflection;
using Koto.Application;
using Koto.Messaging.Wolverine.Middleware;
using Confluent.Kafka;
using Wolverine;
using Wolverine.Kafka;

namespace Koto.Messaging.Wolverine;

/// <summary>Однострочная настройка Wolverine по конвенциям Koto.</summary>
public static class WolverineOptionsExtensions
{
    /// <summary>
    /// Kafka-транспорт с авто-созданием топиков, явной consumer group сервиса,
    /// корреляцией на каждом хендлере и discovery хендлеров в перечисленных сборках
    /// (Wolverine сам сканирует только entry assembly; классы должны называться
    /// <c>*Handler</c>/<c>*Consumer</c>).
    /// <para>
    /// Consumer group ОБЯЗАТЕЛЬНА и должна быть уникальна per-service (конвенция:
    /// имя сервиса): без неё два сервиса могут попасть в одну группу с разными
    /// подписками — Kafka перестаёт назначать партиции, события молча не доставляются
    /// (проявляется в multi-host тестах, где entry assembly общая).
    /// <c>AutoOffsetReset.Earliest</c> — новый сервис дочитывает опубликованное до его старта.
    /// </para>
    /// </summary>
    /// <param name="options">Опции Wolverine.</param>
    /// <param name="kafkaConnectionString">Kafka bootstrap servers.</param>
    /// <param name="consumerGroup">Уникальная consumer group сервиса (обычно его имя).</param>
    /// <param name="handlerAssemblies">Дополнительные сборки с хендлерами (Application и т.п.).</param>
    public static WolverineOptions UseKotoKafka(
        this WolverineOptions options,
        string kafkaConnectionString,
        string consumerGroup,
        params Assembly[] handlerAssemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kafkaConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);

        foreach (var assembly in handlerAssemblies)
            options.Discovery.IncludeAssembly(assembly);

        options.UseKafka(kafkaConnectionString)
            .ConfigureConsumers(consumer =>
            {
                consumer.GroupId = consumerGroup;
                consumer.AutoOffsetReset = AutoOffsetReset.Earliest;
            })
            .AutoProvision();
        options.Policies.AddMiddleware<CorrelationIdMiddleware>();
        return options;
    }

    /// <summary>
    /// Конвенционный роутинг исходящих интеграционных событий: каждый неабстрактный
    /// <see cref="IIntegrationEvent"/> из перечисленных сборок публикуется в Kafka-топик
    /// из его константы <c>public const string Topic</c>. Тип без константы — ошибка
    /// на старте (fail fast, а не молчаливо не доставленное событие).
    /// </summary>
    /// <param name="options">Опции Wolverine.</param>
    /// <param name="contractAssemblies">Сборки контрактов (обычно проекты Contracts.*).</param>
    public static WolverineOptions PublishIntegrationEvents(
        this WolverineOptions options,
        params Assembly[] contractAssemblies)
    {
        var publishMethod = typeof(WolverineOptions).GetMethods()
            .Single(m => m.Name == nameof(WolverineOptions.PublishMessage)
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 0);

        foreach (var assembly in contractAssemblies)
        {
            foreach (var eventType in assembly.GetTypes())
            {
                if (eventType.IsAbstract || !typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                    continue;

                var topicField = eventType.GetField(
                    "Topic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (topicField?.GetRawConstantValue() is not string topic || string.IsNullOrWhiteSpace(topic))
                {
                    throw new InvalidOperationException(
                        $"Integration event '{eventType.FullName}' must declare " +
                        "'public const string Topic = \"service.event-name\";' to use convention routing");
                }

                // Startup-only reflection (конвенция «no reflection on hot paths» не нарушена).
                var expression = publishMethod.MakeGenericMethod(eventType).Invoke(options, null)!;
                var toKafkaTopic = typeof(KafkaTransportExtensions).GetMethod(
                    nameof(KafkaTransportExtensions.ToKafkaTopic))!;
                toKafkaTopic.Invoke(null, [expression, topic]);
            }
        }

        return options;
    }
}
