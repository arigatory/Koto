# ADR-019: Anti-Corruption Layer для HTTP-вызовов между сервисами в Koto.Infrastructure.Http

**Статус:** ✅ Принято  
**Дата:** 2026-06-29  

---

## 1. Контекст

Не всякое взаимодействие между сервисами идёт через шину сообщений. Часть сценариев требует **синхронного** ответа здесь и сейчас (например, проверка лимита, расчёт стоимости, запрос статуса). Для них в Koto используется HTTP, но прямое использование `HttpClient` в прикладном коде создаёт проблемы:

- бизнес-логика начинает зависеть от деталей HTTP (статус-коды, сериализация, заголовки);
- обработка ошибок неконсистентна: одни сервисы бросают исключения, другие проверяют `IsSuccessStatusCode`;
- теряется `CorrelationId` — разрывается сквозная трассировка запроса между сервисами;
- устойчивость (retry, circuit breaker, timeout) настраивается по-разному или отсутствует.

В [ADR-010](ADR-010-command-taxonomy.md) уже зафиксировано: синхронные HTTP-вызовы оформляются интерфейсами Anti-Corruption Layer (ACL) в слое Application/Domain (например, `IPaymentService`), а не через `IIntegrationCommand` (который предназначен только для шины сообщений). Настоящий ADR описывает пакет, который реализует эти ACL-интерфейсы единообразно.

---

## 2. Требования

### Функциональные

| Категория | Требование |
|---|---|
| ACL-граница | Прикладной интерфейс (`IPaymentService`) не содержит деталей HTTP; HTTP живёт только в реализации |
| Маппинг ошибок в Result | HTTP-ответы преобразуются в `Result<T>` с доменными кодами ошибок, без исключений на ожидаемых ветках |
| Проброс CorrelationId | Заголовок `X-Correlation-ID` автоматически добавляется в каждый исходящий запрос |
| Стандартная устойчивость | Retry с экспоненциальной задержкой, circuit breaker и timeout применяются единообразно |
| Простая регистрация | Один вызов регистрирует типизированный клиент с базовым URL и устойчивостью |
| Расширяемость маппинга | Сервис может добавить собственные коды ошибок поверх стандартных |

### Нефункциональные

| Категория | Требование | Критичность |
|---|---|---|
| Лицензия | MIT/Apache 2.0 для всех зависимостей | Обязательно |
| Изоляция зависимости | Детали HTTP/resilience скрыты в `Koto.Infrastructure.Http`; домен их не видит | Обязательно |
| Минимум зависимостей | Зависит только от `Koto.Domain` и официального resilience-пакета | Высокая |
| Современный стек устойчивости | Использовать `Microsoft.Extensions.Http.Resilience` (а не устаревший `Http.Polly`) | Высокая |
| Консистентность с messaging | Та же модель `Result<T>` и `CorrelationId`, что и в остальном Koto | Средняя |

---

## 3. Решение

### Описание

**Koto.Infrastructure.Http** предоставляет базовый класс `ServiceHttpClient` для типизированных HTTP-клиентов, маппинг HTTP-ошибок в `Result<T>`, проброс `CorrelationId` и стандартный pipeline устойчивости поверх **`Microsoft.Extensions.Http.Resilience`**.

Состав:

| Тип | Назначение |
|---|---|
| `ServiceHttpClient` | Абстрактный базовый класс типизированного клиента; маппит HTTP-ошибки в `Result<T>` (`ReadResultAsync<T>`) |
| `ICorrelationIdAccessor` | Интерфейс-источник текущего `CorrelationId` (обычно оборачивает `IHttpContextAccessor`) |
| `CorrelationIdHandler` | `DelegatingHandler`, добавляющий `X-Correlation-ID` в исходящий запрос |
| `AddServiceHttpClient<TInterface, TImplementation>(name, baseUrl)` | Регистрация со стандартной устойчивостью (retry + circuit breaker + timeout) |

Паттерн использования в три шага — интерфейс в Application, реализация в Infrastructure, регистрация в DI:

```csharp
// 1 — Application: ACL-интерфейс без HTTP
public interface IPaymentService
{
    Task<Result<PaymentId>> ChargeAsync(Money amount, CancellationToken ct);
}

// 2 — Infrastructure: реализация поверх ServiceHttpClient
public class PaymentServiceHttpClient : ServiceHttpClient, IPaymentService
{
    public PaymentServiceHttpClient(HttpClient http) : base(http) { }

    public async Task<Result<PaymentId>> ChargeAsync(Money amount, CancellationToken ct)
    {
        var response = await Http.PostAsJsonAsync("/charges",
            new { Amount = amount.Amount, Currency = amount.Currency }, ct);
        return await ReadResultAsync<PaymentId>(response, ct);
    }
}

// 3 — Регистрация (CorrelationId обычно из IHttpContextAccessor)
services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
services.AddServiceHttpClient<IPaymentService, PaymentServiceHttpClient>(
    name: "payment-service",
    baseUrl: config["Services:Payment:BaseUrl"]!);
```

Стандартный pipeline устойчивости применяется автоматически (3 повтора с экспоненциальной задержкой, circuit breaker, timeout 30 с). Маппинг ошибок по умолчанию:

| HTTP-статус | `Error.Code` |
|---|---|
| 404 | `general.not-found` |
| 409 | `general.conflict` |
| 422 | `general.validation` |
| 5xx | `general.unexpected` |

Сервис-специфичные коды добавляются переопределением `MapErrorResponse`.

### Аргументация

| Критерий | Обоснование |
|---|---|
| Чистая ACL-граница | Прикладной код зависит от доменного интерфейса (`IPaymentService`), а не от `HttpClient` — реализация заменяема |
| Единая модель ошибок | HTTP-ответы превращаются в `Result<T>` с доменными кодами — те же, что в командах и хендлерах |
| Сквозная трассировка | `CorrelationIdHandler` пробрасывает `X-Correlation-ID` — запрос виден end-to-end вместе с трейсами из `Koto.Observability` |
| Устойчивость из коробки | Retry/circuit breaker/timeout заданы один раз и единообразны для всех клиентов |
| Современный resilience-стек | `Microsoft.Extensions.Http.Resilience` (на Polly v8) — официальный, не устаревший `Http.Polly` |

#### Последствия

**Положительные:**
- Бизнес-логика тестируется через подмену ACL-интерфейса; HTTP-детали не протекают в домен
- Единый формат ошибок (`Result<T>` + коды) на синхронной и асинхронной границах сервиса
- `CorrelationId` сшивает межсервисные вызовы в трассировке
- Политики устойчивости консистентны и настраиваются централизованно

**Негативные:**
- Ещё один уровень абстракции над `HttpClient` — для тривиальных вызовов выглядит избыточно
- Маппинг ошибок по умолчанию обобщённый; нетривиальные API требуют переопределения `MapErrorResponse`
- Стандартные параметры устойчивости (таймаут/повторы) подходят не всем сценариям — иногда нужна тонкая настройка

**Зависимости:**
- `Koto.Infrastructure.Http` зависит от `Koto.Domain` (тип `Result<T>` и коды ошибок) и `Microsoft.Extensions.Http.Resilience`
- `ICorrelationIdAccessor` обычно реализуется поверх `IHttpContextAccessor`; источник `CorrelationId` согласован с `Koto.Api.FastEndpoints`
- Реализует ACL-интерфейсы, описанные в [ADR-010](ADR-010-command-taxonomy.md); не пересекается с `IIntegrationCommand` (шина сообщений)

---

### 4. Альтернативы

| Вариант | Плюсы | Минусы | Почему отклонён |
|---|---|---|---|
| **Прямой `HttpClient` в коде** | Ноль абстракций; полная гибкость | HTTP-детали протекают в домен; неконсистентная обработка ошибок; легко потерять `CorrelationId` и устойчивость | Нарушает ACL-границу из ADR-010; даёт разнобой между сервисами |
| **Refit / RestEase** | Декларативные клиенты из интерфейса; меньше кода | Своя модель исключений вместо `Result<T>`; ещё одна сторонняя зависимость; маппинг ошибок под наш формат всё равно нужен | Не вписывается в модель `Result<T>`; добавляет зависимость без решения проблемы кодов ошибок |
| **`IIntegrationCommand` поверх Kafka для всего** | Единая модель обмена | Асинхронность не подходит для синхронных запрос-ответ сценариев; лишняя латентность и сложность | ADR-010 явно резервирует `IIntegrationCommand` для шины; синхронные вызовы — это HTTP |
| **`Microsoft.Extensions.Http.Polly`** | Привычный, широко известный | **Устарел**; рекомендована миграция на `Http.Resilience` | Использование устаревшего пакета противоречит требованию современного стека |

---

### 5. Риски

1. **Стандартные параметры устойчивости не подходят конкретному сервису**  
   *Меры:* Сделать параметры pipeline (повторы, таймаут, пороги circuit breaker) настраиваемыми при регистрации; задокументировать значения по умолчанию; для нестандартных случаев — кастомный pipeline.

2. **Обобщённый маппинг ошибок теряет детализацию API**  
   *Меры:* Переопределять `MapErrorResponse` для сервис-специфичных кодов; требовать от вызываемых сервисов структурированных тел ошибок (Problem Details из `Koto.Api.FastEndpoints`).

3. **Retry усиливает нагрузку на деградирующий сервис (retry storm)**  
   *Меры:* Circuit breaker в стандартном pipeline ограничивает шторм; повторять только идемпотентные операции; следить за метриками повторов через `Koto.Observability`.

4. **`CorrelationId` не проброшен из-за отсутствия `ICorrelationIdAccessor`**  
   *Меры:* Регистрировать accessor в шаблоне сервиса по умолчанию; покрыть тестом, что исходящий запрос содержит `X-Correlation-ID`.

5. **Смена лицензии/устаревание resilience-зависимости**  
   *Меры:* Зависимость изолирована в `Koto.Infrastructure.Http`; `Microsoft.Extensions.Http.Resilience` — официальный пакет Microsoft (MIT). Замена pipeline затрагивает только этот пакет.
