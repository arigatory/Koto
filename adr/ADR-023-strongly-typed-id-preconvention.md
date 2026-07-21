# ADR-023: Авто-регистрация конвертеров StronglyTypedId (pre-convention)

**Статус:** ✅ Принято · **Дата:** 2026-07-21

## Context

`StronglyTypedIdConvention` (finalizing) конвертировала только свойства, уже распознанные EF как
скалярные — фактически только первичные ключи. Не-ключевые ссылки (`RefreshToken.UserId`,
`Meetup.GameId`) EF discovery принимал за навигации к сущностям ДО срабатывания finalizing-конвенции,
и потребители (RBG) были вынуждены вручную регистрировать pre-convention конвертеры в каждом контексте.

## Decision

`KotoDbContext.ConfigureConventions` сканирует сборки доменных сущностей (по `DbSet<>`-свойствам
конкретного контекста), находит все типы, унаследованные от `StronglyTypedId<T>`, и регистрирует
`configurationBuilder.Properties(idType).HaveConversion(StronglyTypedIdValueConverter<idType, T>)`.
Рефлексия — однократная, на построении модели (не hot path). Finalizing-конвенция сохранена
для id-типов из сборок вне доменных (edge case).

## Consequences

- Потребители не пишут НИ ОДНОЙ строки конфигурации для id-типов (ключи, ссылки, owned-ключи).
- Сканирование по сборке шире, чем нужно контексту — безвредно (лишние Properties-регистрации
  не влияют на типы, отсутствующие в модели).
