using System.Reflection;
using System.Text.Json;
using Koto.Application;
using Koto.Messaging.Wolverine.Middleware;
using Confluent.Kafka;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Kafka;

namespace Koto.Messaging.Wolverine;

/// <summary>Однострочная настройка Wolverine по конвенциям Koto.</summary>
public static class WolverineOptionsExtensions
{
    /// <summary>JSON-настройки контрактов шины (web-casing — как в HTTP API).</summary>
    public static readonly JsonSerializerOptions ContractJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Подписка на интеграционное событие по конвенции: топик — из <c>T.Topic</c>,
    /// payload — чистый JSON (см. <see cref="PublishIntegrationEvents"/>), обработка inline.
    /// </summary>
    public static WolverineOptions ListenToIntegrationEvent<T>(this WolverineOptions options)
        where T : IIntegrationEvent
    {
        var topicField = typeof(T).GetField(
            "Topic", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (topicField?.GetRawConstantValue() is not string topic || string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException(
                $"Integration event '{typeof(T).FullName}' must declare 'public const string Topic'");
        }

        options.ListenToKafkaTopic(topic)
            .ReceiveRawJson<T>(ContractJson)
            .ProcessInline();
        return options;
    }

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

        // Дефолтная политика ошибок консюмеров. Без неё Wolverine после первого
        // исключения сразу уводит сообщение в dead letter — а при подписке на несколько
        // топиков гонка «событие-предпосылка ещё не обработано» является штатной ситуацией:
        // консюмер бросает, и сообщение должно ПОВТОРИТЬСЯ, а не потеряться.
        // Быстрые inline-повторы гасят короткие гонки, отложенные — длинные хвосты
        // (при durable inbox переживают и рестарт), dead letter — только после всех попыток.
        // Более специфичные политики сервисов (по типу исключения) имеют приоритет.
        options.Policies.OnException<Exception>()
            .RetryWithCooldown(
                TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3))
            .Then.ScheduleRetry(
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60))
            .Then.MoveToErrorQueue();

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
                var subscriber = toKafkaTopic.Invoke(null, [expression, topic])!;

                // Schema-прозрачная шина: чистый JSON без Wolverine-заголовков —
                // топик читаем любым инструментом/стеком (тип ↔ топик 1:1).
                var publishRawJson = subscriber.GetType().GetMethod("PublishRawJson")!;
                publishRawJson.Invoke(subscriber, [ContractJson]);
            }
        }

        return options;
    }
}
