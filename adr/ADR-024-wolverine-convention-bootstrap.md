# ADR-024: Конвенционный bootstrap Wolverine (UseKotoKafka / PublishIntegrationEvents / UseKotoDurableOutbox)

**Статус:** ✅ Принято · **Дата:** 2026-07-21

## Context

Каждый сервис RBG повторял ~20 строк настройки Wolverine: Kafka + AutoProvision, роутинг каждого
события на топик, correlation middleware, durable-хранилище конвертов, durable-политика, интеграция
EF-транзакций, скрейпинг доменных событий, discovery Application-сборки. Дублирование + два
подводных камня: discovery только entry assembly и молчаливо не отроученные события.

## Decision

- `Koto.Messaging.Wolverine`: `opts.UseKotoKafka(bootstrap, params handlerAssemblies)` — транспорт,
  AutoProvision, корреляция, discovery; `opts.PublishIntegrationEvents(params contractAssemblies)` —
  конвенционный роутинг: каждый неабстрактный `IIntegrationEvent` обязан объявлять
  `public const string Topic` (иначе fail-fast на старте — не молчаливая потеря событий).
  Рефлексия однократная на старте.
- `Koto.Messaging.Wolverine.Postgres`: `opts.UseKotoDurableOutbox(pgConnectionString)` — конверты в
  Postgres, durable-политика, EF-транзакции, скрейпинг доменных событий (зависимости PG/EF живут
  в opt-in пакете, базовый messaging остаётся лёгким — та же логика, что в ADR-021).

## Consequences

- Wolverine-блок сервиса: 3 строки вместо ~20; новые события контрактов роутятся автоматически.
- Константа `Topic` становится частью конвенции контрактов (уже фактический стандарт в RBG).
