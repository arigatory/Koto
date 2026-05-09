# ADR-010: Таксономия команд — ICommand (локальные) и IIntegrationCommand (межсервисные)

**Статус:** ✅ Принято  
**Дата:** 2026-05-10  

---

## 1. Контекст

В микросервисной архитектуре команды бывают принципиально двух видов:

1. **Локальные команды** — обрабатываются внутри того же процесса / сервиса в рамках паттерна CQRS.
2. **Межсервисные команды** — отправляются в другой микросервис для координации действий через шину сообщений.

Использование единственного интерфейса для обоих видов создаёт неоднозначность: по типу невозможно определить, пересекает ли команда границу сервиса. Это ведёт к ошибкам маршрутизации, размытой ответственности и нарушению принципа явности в коде.

Данный ADR фиксирует решение о разделении командного пространства на две чётко разграниченные таксономии: `ICommand` / `ICommand<TResult>` / `IQuery<TResult>` для внутреннего использования и `IIntegrationCommand` / `IIntegrationCommand<TResult>` / `IIntegrationEvent` для взаимодействия между сервисами.

---

## 2. Требования

### Функциональные

| Категория | Требование |
|---|---|
| Локальные команды | Система должна предоставлять `ICommand` для локальных CQRS-команд без возвращаемого значения |
| Локальные команды с результатом | Система должна предоставлять `ICommand<TResult>` для локальных CQRS-команд, возвращающих значение |
| Локальные запросы | Система должна предоставлять `IQuery<TResult>` для локальных CQRS-запросов |
| Межсервисные команды | Система должна предоставлять `IIntegrationCommand` для fire-and-forget команд в другой сервис через шину сообщений |
| Межсервисные команды с ответом | Система должна предоставлять `IIntegrationCommand<TResult>` для команд с ожидаемым ответом (HTTP или Kafka request/reply) |
| Интеграционные события | Система должна предоставлять `IIntegrationEvent` для публикации событий в другие сервисы через pub/sub |
| HTTP-вызовы между сервисами | Синхронные HTTP-вызовы в другие сервисы должны реализовываться через интерфейсы Anti-Corruption Layer, а не через `IIntegrationCommand` |

### Нефункциональные

| Категория | Требование | Критичность |
|---|---|---|
| Явность на уровне типов | Принадлежность команды к локальному или межсервисному слою должна быть очевидна из её типа без чтения реализации | Обязательно |
| Единообразие именования | Все межсервисные типы должны содержать суффикс `Integration`; локальные типы — нет | Обязательно |
| Разделение диспетчеризации | Локальные и межсервисные команды должны диспетчеризоваться разными компонентами: `ICqrsDispatcher` и `IIntegrationCommandDispatcher` соответственно | Обязательно |
| Независимость производительности | Локальная обработка команд не должна зависеть от надёжности внешней шины сообщений | Высокая |
| Компиляционная верификация | Компилятор должен препятствовать отправке локальной команды через межсервисный диспетчер и наоборот | Высокая |

---

## 3. Решение

### Описание

Принята двухуровневая таксономия с чётким правилом: **префикс `Integration` означает пересечение границы сервиса**.

```
ВНУТРЕННИЕ (в рамках одного микросервиса):
  ICommand              — локальная CQRS-команда без возвращаемого значения
  ICommand<TResult>     — локальная CQRS-команда с возвращаемым значением
  IQuery<TResult>       — локальный CQRS-запрос

ВНЕШНИЕ (межсервисные):
  IIntegrationCommand              — fire-and-forget в другой сервис (через Kafka)
  IIntegrationCommand<TResult>     — команда в другой сервис с ожидаемым ответом (HTTP или Kafka request/reply)
  IIntegrationEvent                — публикуемое событие для других сервисов (pub/sub)
```

Интерфейсы определяются следующим образом:

```csharp
// --- Локальный слой ---

// Маркерный интерфейс для локальных команд без результата
public interface ICommand { }

// Маркерный интерфейс для локальных команд с результатом
public interface ICommand<TResult> { }

// Маркерный интерфейс для локальных запросов
public interface IQuery<TResult> { }

// Диспетчер локального CQRS
public interface ICqrsDispatcher
{
    Task ExecuteAsync(ICommand command, CancellationToken ct = default);
    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}

// --- Межсервисный слой ---

// Маркерный интерфейс для fire-and-forget команд в другой сервис
public interface IIntegrationCommand { }

// Маркерный интерфейс для межсервисных команд с ожидаемым ответом
public interface IIntegrationCommand<TResult> { }

// Контракт интеграционного события
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

// Базовый тип для удобства: не нужно дублировать EventId/OccurredAt
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    // Для новых исходящих событий (обычный runtime-путь)
    protected IntegrationEvent() : this(Guid.NewGuid(), DateTimeOffset.UtcNow) { }

    // Для replay/rehydration — значения приходят извне и не генерируются заново
    protected IntegrationEvent(Guid eventId, DateTimeOffset occurredAt)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
    }
}

// Диспетчер межсервисных команд
public interface IIntegrationCommandDispatcher
{
    Task SendAsync(IIntegrationCommand command, CancellationToken ct = default);
    Task<TResult> SendAsync<TResult>(IIntegrationCommand<TResult> command, CancellationToken ct = default);
}

// Издатель интеграционных событий
public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        IIntegrationEvent @event,
        string partitionKey,
        CancellationToken ct = default);
}
```

Для Kafka ключ партиционирования передаётся явно в `PublishAsync(..., partitionKey, ...)`. Базовое правило: использовать стабильный ключ бизнес-сущности (например, `OrderId`), чтобы все сообщения по одной сущности попадали в одну партицию и сохраняли порядок обработки.

Примеры использования таксономии:

```csharp
// Локальная CQRS-команда — диспетчеризуется через ICqrsDispatcher
public record PlaceOrderCommand(CustomerId CustomerId, List<OrderItemDto> Items)
    : ICommand<OrderId>;

// Межсервисная команда — диспетчеризуется через IIntegrationCommandDispatcher → Kafka
public record ShipOrderIntegrationCommand(OrderId OrderId, Address Destination)
    : IIntegrationCommand;

// Межсервисная команда с ответом — через IIntegrationCommandDispatcher → HTTP или Kafka request/reply
public record ChargePaymentIntegrationCommand(OrderId OrderId, Money Amount)
    : IIntegrationCommand<PaymentId>;

// Интеграционное событие — через IIntegrationEventPublisher → Kafka → любой подписчик
public record OrderPlacedIntegrationEvent(Guid OrderId, Guid CustomerId, decimal Total)
    : IntegrationEvent;

// Публикация с ключом партиционирования:
await _integrationEventPublisher.PublishAsync(
    new OrderPlacedIntegrationEvent(
        order.Id.Value,
        order.CustomerId.Value,
        order.Total.Amount),
    partitionKey: order.Id.Value.ToString(),
    ct);
```

HTTP-вызовы между сервисами реализуются через интерфейсы Anti-Corruption Layer в слое Application или Domain, а не через `IIntegrationCommand`:

```csharp
// Anti-Corruption Layer для синхронных HTTP-вызовов
// Определяется в Application или Domain; реализация — в Infrastructure
public interface IPaymentService
{
    Task<Result<PaymentId, Error>> ChargeAsync(Money amount, CancellationToken ct);
}
```

`IIntegrationCommand` предназначен исключительно для взаимодействия через шину сообщений. Синхронные HTTP-вызовы используют интерфейсы сервис-клиентов.

### Аргументация

| Критерий | Обоснование |
|---|---|
| Явность на уровне типов | Граница сервиса видна немедленно: `ShipOrderIntegrationCommand` vs `PlaceOrderCommand` — не нужно изучать реализацию или документацию |
| Терминология DDD | «Integration» — устоявшееся понятие в DDD (Integration Event, Integrations Context); «External» менее точен и может означать «внешний по отношению к домену», а не к сервису |
| Разделение диспетчеризации | Два отдельных диспетчера (`ICqrsDispatcher` и `IIntegrationCommandDispatcher`) исключают случайную маршрутизацию: компилятор не позволит отправить `ICommand` через межсервисный диспетчер |
| Независимость производительности | Локальная обработка через in-process диспетчер не зависит от доступности Kafka или HTTP-эндпоинтов внешних сервисов |
| Компиляционная защита | Принадлежность к таксономии выражается через тип: намеренно отправить локальную команду во внешний диспетчер — компиляционная ошибка |
| Предсказуемость для команды | Единое правило «`Integration` = межсервисное» применяется одинаково ко всем командам и событиям; нет специальных исключений |

#### Последствия

**Положительные:**
- Граница сервиса читается из сигнатуры типа без изучения реализации или инфраструктурных деталей
- Компилятор предотвращает класс ошибок маршрутизации: нельзя случайно обработать `IIntegrationCommand` локальным диспетчером
- Локальная производительность изолирована от внешней надёжности; сбой Kafka не влияет на in-process CQRS
- Новые разработчики усваивают одно правило (`Integration` = пересечение границы), применимое ко всему стеку
- Code review упрощается: любой `IntegrationCommand` или `IntegrationEvent` сигнализирует ревьюеру о необходимости проверить транспорт и контракт

**Негативные:**
- Больший объём интерфейсов и диспетчеров: два диспетчера вместо одного требуют поддержки и регистрации в DI
- Разработчики должны принять осознанное решение при создании команды: локальная или межсервисная? Ошибочный выбор придётся исправлять с переименованием
- HTTP-вызовы между сервисами выходят за рамки таксономии `IIntegrationCommand` и требуют понимания отдельного паттерна ACL

**Зависимости:**
- `Koto.Application` фиксирует интерфейсы `ICqrsDispatcher`, `IIntegrationCommandDispatcher`, `IIntegrationEventPublisher` и `IIntegrationEvent`
- Все микросервисы обязаны регистрировать оба диспетчера в DI-контейнере
- Kafka-адаптер реализует `IIntegrationCommandDispatcher` и `IIntegrationEventPublisher` — изменение транспорта не затрагивает код уровня Application
- ACL-интерфейсы для HTTP (например, `IPaymentService`) живут в Application / Domain; их реализации — в Infrastructure и также регистрируются в DI

---

### 4. Альтернативы

| Вариант | Плюсы | Минусы | Почему отклонён |
|---|---|---|---|
| **Единственный `ICommand` для всего** | Простота — один интерфейс, один диспетчер | Нет типовой индикации пересечения границы; маршрутизация определяется регистрацией обработчика — легко ошибиться | Нарушает принцип явности; пересечение границы сервиса — фундаментальное различие, которое обязано быть видно из типа |
| **Префикс «External» вместо «Integration»** | Дескриптивно, самоочевидно | «External» неоднозначен: может означать внешний по отношению к домену или к bounded context; «Integration» — устоявшийся термин в DDD и Enterprise Integration Patterns | «Integration» точнее отражает намерение и соответствует общепринятой терминологии DDD |
| **Без разграничения — документация вместо типов** | Нулевые изменения в коде | Документация не проверяется компилятором; устаревает; игнорируется при code review; граница сервиса остаётся неявной | Документация не исполняется компилятором; тип — первичный, самоисполняющийся механизм документирования |
| **Единая шина для локального и внешнего** | Единая точка диспетчеризации | Связывает локальную производительность с надёжностью внешнего транспорта; сбой Kafka блокирует in-process операции; чрезмерная сложность для простых локальных команд | Неприемлемо смешивает транспортные характеристики; локальный in-process dispatch всегда быстрее и надёжнее, чем сетевой транспорт |

---

### 5. Риски

1. **Ошибочная классификация команды при создании**  
   Разработчик может ошибочно реализовать `IIntegrationCommand` для того, что должно быть локальным `ICommand`, или наоборот.  
   *Меры:* Включить в шаблоны проекта (scaffolding) подсказки о выборе типа; на code review проверять, что `IntegrationCommand` действительно маршрутизируется через шину сообщений, а не обрабатывается в том же сервисе.

2. **Разрастание ACL-интерфейсов для HTTP**  
   Если сервис взаимодействует с десятками внешних сервисов по HTTP, количество ACL-интерфейсов может стать трудно управляемым.  
   *Меры:* Группировать методы одного внешнего сервиса в один интерфейс (`IPaymentService`, `IInventoryService`); не создавать интерфейс на каждый метод. Пересмотреть при превышении ~10 внешних HTTP-зависимостей на сервис.

3. **Смешение транспортов для `IIntegrationCommand<TResult>`**  
   Команды с ожидаемым ответом могут реализовываться как через HTTP, так и через Kafka request/reply, что создаёт неоднородность в рамках одного интерфейса.  
   *Меры:* Документировать транспорт в XML-комментарии к конкретной команде; рассмотреть в будущем атрибут `[Transport(Transport.Kafka)]` для явного указания — зафиксировать отдельным ADR при необходимости.

4. **Неправильная регистрация диспетчеров в DI**  
   Если `IIntegrationCommandDispatcher` не зарегистрирован или зарегистрирован с неверной реализацией (например, заглушкой в продакшне), команды будут молча теряться.  
   *Меры:* Добавить startup-валидацию, проверяющую регистрацию обоих диспетчеров; использовать интеграционные тесты, верифицирующие фактическую доставку сообщений через Kafka в тестовой среде.
