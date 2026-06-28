# Koto.Application — Plan

**Phase:** 1 | **Status:** NOT STARTED
**Depends on:** Koto.Domain only

---

## Цель

Интерфейсы и абстракции без инфраструктурных зависимостей. Реализации — в других пакетах.

## Checklist

### Local CQRS
- [ ] `ICommand` — marker interface
- [ ] `ICommand<TResult>` — marker interface
- [ ] `IQuery<TResult>` — marker interface
- [ ] `ICommandHandler<TCommand>` where TCommand : ICommand — `Task<Result<Unit>> HandleAsync(TCommand, CancellationToken)`
- [ ] `ICommandHandler<TCommand, TResult>` — `Task<Result<TResult>> HandleAsync(TCommand, CancellationToken)`
- [ ] `IQueryHandler<TQuery, TResult>` where TQuery : IQuery<TResult> — `Task<Result<TResult>> HandleAsync(TQuery, CancellationToken)`
- [ ] `Unit` — empty struct (замена void в Result<Unit>)

### CQRS Dispatcher
- [ ] `ICqrsDispatcher`:
  - `Task<Result<Unit>> SendAsync(ICommand, CancellationToken)`
  - `Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult>, CancellationToken)`
  - `Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult>, CancellationToken)`
- [ ] `CqrsDispatcher` — реализация через `IServiceProvider`:
  - Resolves handler из DI
  - Прогоняет через pipeline behaviors в порядке регистрации
  - ~100 строк, без рефлексии в hot path (source generation или кэш типов)

### Pipeline Behaviors
- [ ] `IPipelineBehavior<TRequest, TResponse>` — `Task<TResponse> HandleAsync(TRequest, Func<Task<TResponse>> next, CancellationToken)`
- [ ] `ValidationBehavior<TRequest, TResponse>` — запускает все `IValidator<TRequest>` (FluentValidation v7); при ошибках возвращает `Result.Failure` со всеми ошибками
- [ ] `LoggingBehavior<TRequest, TResponse>` — structured logging: имя запроса, время выполнения, успех/провал
- [ ] `TransactionBehavior<TRequest, TResponse>` — оборачивает command в DB транзакцию (только для ICommand, не для IQuery)
  - **Семантика транзакции:** commit при успешном `Result`; **rollback** при `Result.Failure` (по маркеру `IResultBase.IsFailure`) и при брошенном исключении. То есть handler, который мутировал tracked-состояние, а затем вернул `Result.Failure`, **не** закоммитит изменения. Тем не менее предпочтительно валидировать до мутаций.

### Cross-service abstractions
- [ ] `IIntegrationEvent` — `Guid EventId`, `DateTime OccurredAt`, `string? CorrelationId`
- [ ] `IntegrationEvent` — base record реализующий IIntegrationEvent
- [ ] `IIntegrationCommand` — marker interface (fire-and-forget)
- [ ] `IIntegrationCommand<TResult>` — marker interface (request/reply)
- [ ] `IIntegrationEventPublisher` — `Task PublishAsync(IIntegrationEvent, CancellationToken)`
- [ ] `IIntegrationCommandDispatcher`:
  - `Task SendAsync(IIntegrationCommand, CancellationToken)`
  - `Task<TResult> SendAsync<TResult>(IIntegrationCommand<TResult>, CancellationToken)`

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoApplication(services, assemblies[])` — сканирует assemblies и регистрирует все handlers + validators

## Тесты (Koto.Application.Tests)
- [ ] CqrsDispatcher: правильно resolves handler
- [ ] CqrsDispatcher: behaviors выполняются в правильном порядке
- [ ] ValidationBehavior: возвращает Failure при ошибках валидации, собирает все ошибки
- [ ] LoggingBehavior: логирует start/end с timing
- [ ] TransactionBehavior: не применяется к queries
- [x] TransactionBehavior: commit при успехе, rollback при `Result.Failure`, rollback + rethrow при исключении
