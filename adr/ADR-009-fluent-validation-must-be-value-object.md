# ADR-009: FluentValidation v7 зафиксирован + паттерн MustBeValueObject

**Статус:** ✅ Принято  
**Дата:** 2026-05-10  

---

## 1. Контекст

В проектах Koto применяется подход **«Always Valid Domain Model»**: вся логика валидации инкапсулирована в фабричных методах доменных объектов (`Email.Create`, `OrderId.Create` и т. д.). Метод фабрики возвращает `Result<T, Error>` — либо корректно созданный объект-значение, либо структурированную ошибку.

Параллельно на уровне API использовалась библиотека **FluentValidation** для валидации входящих DTO. Это порождало дублирование: правила валидации прописывались дважды — в доменном фабричном методе и в валидаторе FluentValidation.

Важно уточнить лицензионный факт: **FluentValidation v8+ также распространяется под Apache 2.0**. В рамках Koto версия v7.x остаётся зафиксированной по соображениям стабильности API и предсказуемости обновлений.

Данный ADR фиксирует двухчастное решение: **зафиксировать FluentValidation на версии v7.x** и **внедрить паттерн `MustBeValueObject`** в пакете `Koto.Validation` как мост между валидатором API и доменной логикой.

---

## 2. Требования

### Функциональные

| Категория | Требование |
|---|---|
| Единая точка истины | Логика валидации должна быть описана ровно один раз — в доменном фабричном методе |
| Интеграция с FluentValidation | Паттерн `MustBeValueObject` должен вызывать фабричный метод и преобразовывать `Error` в сообщение об ошибке FluentValidation |
| Поддержка объектов-значений | Метод `MustBeValueObject` должен работать с любым фабричным методом сигнатуры `Func<string, Result<T, Error>>` |
| Поддержка сущностей | Метод `MustBeEntity` должен поддерживать фабрики с несколькими полями для построения сущностей |
| Коллекции | Метод `ListMustContainNumberOfItems(min, max)` должен проверять количество элементов с использованием `Errors.General` для консистентных сообщений |
| Перегрузки с `Errors.General` | Перегрузки `NotEmpty()` и `Length()` должны сериализовывать ошибки через `Errors.General` для единообразного формата |

### Нефункциональные

| Категория | Требование | Критичность |
|---|---|---|
| Лицензионная чистота | Используемая версия FluentValidation должна быть под Apache 2.0 | Обязательно |
| Устранение дублирования | При изменении доменной валидации API-валидация должна обновляться автоматически | Обязательно |
| Обратная совместимость | Существующие валидаторы не должны требовать немедленного рефакторинга | Высокая |
| Минимализм зависимостей | `Koto.Validation` должен зависеть только от `Koto.Domain` и `FluentValidation` v7.x | Высокая |
| Читаемость | Синтаксис `MustBeValueObject` должен быть таким же лаконичным, как встроенные методы FluentValidation | Средняя |

---

## 3. Решение

### Описание

**Зафиксировать FluentValidation на версии 7.x (Apache 2.0) и реализовать в `Koto.Validation` паттерн `MustBeValueObject`, делегирующий валидацию доменным фабричным методам.**

#### Фиксация версии

В файле проекта `Koto.Validation.csproj` версия FluentValidation явно ограничена диапазоном `[7.*, 8.0)`:

```xml
<PackageReference Include="FluentValidation" Version="[7.*,8.0)" />
```

Это исключает случайное обновление до v8+ — ни через `dotnet update`, ни через Dependabot.

#### Паттерн MustBeValueObject

Вместо дублирования правил в валидаторе FluentValidation напрямую вызывается доменный фабричный метод:

```csharp
// Раньше: логика валидации дублировалась
RuleFor(x => x.Email)
    .NotEmpty()
    .MaximumLength(150)
    .EmailAddress();  // эти правила уже есть в Email.Create — ДУБЛИРОВАНИЕ

// Теперь: один вызов фабрики — единая точка истины
RuleFor(x => x.Email)
    .MustBeValueObject(Email.Create);  // вызывает доменную фабрику, Error → сообщение об ошибке
```

#### Реализация в Koto.Validation

```csharp
// Основной метод: строка → объект-значение через фабрику
public static IRuleBuilderOptions<T, string> MustBeValueObject<T, TValueObject>(
    this IRuleBuilder<T, string> ruleBuilder,
    Func<string, Result<TValueObject, Error>> factory)
    where TValueObject : class
{
    return (IRuleBuilderOptions<T, string>)ruleBuilder.Custom((value, context) =>
    {
        var result = factory(value);
        if (result.IsFailure)
            context.AddFailure(result.Error.Serialize());
    });
}

// Для сущностей с несколькими полями
public static IRuleBuilderOptions<T, TElement> MustBeEntity<T, TElement, TEntity>(
    this IRuleBuilder<T, TElement> ruleBuilder,
    Func<TElement, Result<TEntity, Error>> factory)
    where TEntity : class
{
    return (IRuleBuilderOptions<T, TElement>)ruleBuilder.Custom((value, context) =>
    {
        var result = factory(value);
        if (result.IsFailure)
            context.AddFailure(result.Error.Serialize());
    });
}

// Для коллекций: проверка количества элементов с Errors.General
public static IRuleBuilderOptions<T, IList<TElement>> ListMustContainNumberOfItems<T, TElement>(
    this IRuleBuilder<T, IList<TElement>> ruleBuilder,
    int? min = null, int? max = null)
{
    return (IRuleBuilderOptions<T, IList<TElement>>)ruleBuilder.Custom((list, context) =>
    {
        if (min.HasValue && list.Count < min.Value)
            context.AddFailure(Errors.General.InvalidLength(min.Value, list.Count).Serialize());
        if (max.HasValue && list.Count > max.Value)
            context.AddFailure(Errors.General.InvalidLength(max.Value, list.Count).Serialize());
    });
}

// Перегрузки с Errors.General для консистентного сериализованного формата ошибок
public static IRuleBuilderOptions<T, string> NotEmpty<T>(
    this IRuleBuilder<T, string> ruleBuilder)
{
    return (IRuleBuilderOptions<T, string>)ruleBuilder.Custom((value, context) =>
    {
        if (string.IsNullOrWhiteSpace(value))
            context.AddFailure(Errors.General.ValueIsRequired().Serialize());
    });
}

public static IRuleBuilderOptions<T, string> Length<T>(
    this IRuleBuilder<T, string> ruleBuilder, int min, int max)
{
    return (IRuleBuilderOptions<T, string>)ruleBuilder.Custom((value, context) =>
    {
        if (value is not null && (value.Length < min || value.Length > max))
            context.AddFailure(Errors.General.InvalidLength(min, max).Serialize());
    });
}
```

#### Принцип единой точки истины

При изменении доменного правила — например, ограничения длины `Email` — обновляется только `Email.Create`. Валидатор FluentValidation получает это изменение автоматически, так как сам ничего не знает о конкретных правилах:

```
До:  API валидатор → дублирует правила  ┐
     Доменная фабрика → содержит правила ┘  (две точки истины)

После: API валидатор → вызывает фабрику → фабрика содержит правила  (одна точка истины)
```

### Аргументация

| Критерий | Обоснование |
|---|---|
| Лицензионная безопасность | FluentValidation v7.x и v8+ распространяются под Apache 2.0; фиксация на v7 в Koto нужна для стабильности API и контролируемого апгрейда |
| Устранение дублирования | `MustBeValueObject` делегирует валидацию фабричному методу, который уже является единственным авторитетным источником правил для данного объекта-значения |
| Автоматическая согласованность | Любое изменение доменной валидации немедленно отражается на уровне API без ручного обновления валидаторов |
| Стабильность и знакомость | FluentValidation v7 — зрелая, хорошо документированная библиотека, которую команда знает в деталях. Миграция на альтернативу не даёт сопоставимого выигрыша |
| Минимальные изменения | Существующие валидаторы продолжают работать. `MustBeValueObject` — аддитивное расширение, а не замена всего API |
| Консистентность ошибок | Перегрузки `NotEmpty()` и `Length()` используют `Errors.General`, что обеспечивает одинаковый формат сериализованных ошибок по всей системе |

#### Последствия

**Положительные:**
- Логика валидации живёт ровно в одном месте — в доменном фабричном методе
- API-валидация автоматически отражает доменные инварианты без ручной синхронизации
- FluentValidation в используемом диапазоне версий остаётся под Apache 2.0 — никаких лицензионных рисков
- Команда сохраняет знакомый синтаксис и экосистему FluentValidation
- Ошибки сериализуются через `Errors.General` — единый формат по всей системе

**Негативные:**
- `Koto.Validation` вводит внешнюю зависимость от FluentValidation v7 (принято осознанно — только на уровне инфраструктуры API, не в `Koto.Domain`)
- Если FluentValidation v7 обнаружит критическую уязвимость без патча в v7-ветке — потребуется миграция на альтернативу
- Разработчики, привыкшие к `v8+` или Validot, должны адаптироваться к API v7

**Зависимости:**
- `Koto.Validation` зависит от `Koto.Domain` (для типов `Result<T, Error>` и `Errors.General`)
- Все API-проекты, использующие FluentValidation, должны ссылаться на `Koto.Validation`, а не напрямую на FluentValidation
- Будущие решения об изменении механизма валидации на API-границе требуют нового ADR

---

### 4. Альтернативы

| Вариант | Плюсы | Минусы | Почему отклонён |
|---|---|---|---|
| **FluentValidation v8+ (Apache 2.0)** | Актуальная версия, новые возможности, долгосрочная поддержка | Требует плановой миграции API и ретестов валидаторов | Отложено: v7 полностью закрывает текущие потребности, а паттерн `MustBeValueObject` уже стабилизирован на v7 |
| **Validot (MIT)** | Отличная производительность (аллокации близки к нулю), лицензия MIT навсегда | Меньшее сообщество, иной API — нужна переподготовка команды, меньше готовых примеров и интеграций | Стоимость миграции и переобучения не оправдана; выигрыш в производительности незначителен на уровне API-валидации |
| **Только Data Annotations** | Встроено в .NET, нет внешних зависимостей | Недостаточно выразителен для сложных доменных правил; невозможно делегировать фабричному методу | Не поддерживает паттерн `MustBeValueObject`; слишком ограничен для реальных сценариев |
| **Кастомный пайплайн без FluentValidation** | Полный контроль, нет внешних зависимостей | Значительный объём кода для реализации; нет готовой интеграции с ASP.NET Core; потеря знакомого DSL | Объём поддерживаемого кода неоправданно вырастет; FluentValidation v7 уже решает эту задачу |
| **Только доменная валидация (без FV)** | Максимально простая архитектура; единая точка истины без дополнительных слоёв | Сложнее пробросить ошибки до API-слоя с правильным маппингом на конкретные поля DTO | Маппинг ошибок домена на HTTP-поля требует собственной инфраструктуры; FluentValidation уже делает это из коробки |

---

### 5. Риски

1. **Критическая уязвимость безопасности в FluentValidation v7 без патча**  
   *Меры:* Мониторить GitHub Advisory Database для `FluentValidation`. При обнаружении незакрытой уязвимости в v7 — немедленно выпустить ADR о миграции на Validot или иную альтернативу. Оценку проводить раз в квартал.

2. **Случайное обновление до v8+ через автоматические инструменты (Dependabot, Renovate)**  
   *Меры:* В `Koto.Validation.csproj` явно задан диапазон `[7.*, 8.0)`. Добавить проверку в CI: если разрешённая версия FluentValidation выходит за этот диапазон — сборка падает с понятным сообщением.

3. **Несогласованное применение паттерна: часть валидаторов дублирует правила, часть использует MustBeValueObject**  
   *Меры:* Зафиксировать правило в CONTRIBUTING.md и шаблоне ревью PR: любой `RuleFor`, где поле соответствует объекту-значению, обязан использовать `MustBeValueObject`. Добавить Roslyn-анализатор или ревью-чеклист.

4. **Доменная фабрика возвращает недостаточно детализированный Error, неудобный для пользователя API**  
   *Меры:* Требование к фабричным методам: `Error.Code` должен однозначно идентифицировать поле и тип нарушения. Стандарт сериализации `Errors.General` закрывает большинство случаев; специфичные ошибки документируются в ADR соответствующего объекта-значения.

5. **Рост числа перегрузок в Koto.Validation без контроля**  
   *Меры:* Новые методы расширения в `Koto.Validation` добавляются только при наличии не менее трёх реальных use-case в кодовой базе. Каждое добавление фиксируется в CHANGELOG пакета. Размытие ответственности пакета требует нового ADR.
