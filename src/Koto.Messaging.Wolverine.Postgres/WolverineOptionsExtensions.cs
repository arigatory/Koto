using Koto.Domain;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace Koto.Messaging.Wolverine.Postgres;

/// <summary>Durable outbox по конвенциям Koto (PostgreSQL + EF Core).</summary>
public static class WolverineOptionsExtensions
{
    /// <summary>
    /// Полная настройка durable outbox: конверты в Postgres сервиса, durable-политика
    /// на всех исходящих, интеграция с EF Core-транзакциями и авто-скрейпинг доменных
    /// событий из агрегатов при сохранениях внутри Wolverine-хендлеров.
    /// (Сохранения из обычного кода публикуют события через <c>EfCoreUnitOfWork</c> —
    /// см. Koto.Infrastructure.EFCore, ADR-022.)
    /// </summary>
    /// <param name="options">Опции Wolverine.</param>
    /// <param name="postgresConnectionString">Строка подключения к БД сервиса.</param>
    public static WolverineOptions UseKotoDurableOutbox(
        this WolverineOptions options,
        string postgresConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnectionString);

        options.PersistMessagesWithPostgresql(postgresConnectionString);
        options.Policies.UseDurableOutboxOnAllSendingEndpoints();
        options.UseEntityFrameworkCoreTransactions();
        options.PublishDomainEventsFromEntityFrameworkCore<IHasDomainEvents, IDomainEvent>(
            e => e.DomainEvents);
        return options;
    }
}
