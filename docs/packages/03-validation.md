# Koto.Validation — Plan

**Phase:** 1 | **Status:** DONE
**Depends on:** Koto.Domain + Koto.Application + FluentValidation v12 (`[12.0.0,13.0.0)`, Apache 2.0)

> **Обновление v0.3.0-preview.1:** FluentValidation 7 → 12 (cast-хаки убраны);
> `MustBeValueObject<T,TSource,TVO>` generic по типу источника (не только `string`);
> доменный `Error` едет через `ValidationFailure.CustomState` + `ErrorCode` (не
> `Serialize()`-строкой); `ValidationBehavior` — `ValidateAsync`, constraint
> `IResultFactory<TResponse>` (без рефлексии), N ошибок → N структурных `Error` с `Field`.
> См. ревизию ADR-009. Ниже — исторический план; актуальное API см. README пакета.

---

## Цель

Мост между FluentValidation и паттерном `Result<T>`. Валидационная логика живёт один раз — в фабричных методах домена. FluentValidation просто вызывает их.

## Пример использования

```csharp
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .MustBeValueObject(Email.Create);

        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(1, 200);

        RuleFor(x => x.Tags)
            .ListMustContainNumberOfItems(min: 1, max: 10);

        RuleFor(x => x.Address)
            .MustBeEntity(dto => Address.Create(dto.Street, dto.City, dto.ZipCode));
    }
}
```

## Checklist

### FluentValidation Extensions (static class `KotoValidators`)
- [ ] `MustBeValueObject<T, TValueObject>(Func<string, Result<TValueObject>> factory)` — вызывает factory, при IsFailure добавляет `result.Error.Serialize()` как validation failure
- [ ] `MustBeEntity<T, TElement, TEntity>(Func<TElement, Result<TEntity>> factory)` — то же для составных объектов
- [ ] `ListMustContainNumberOfItems<T, TElement>(int? min, int? max)` — использует `Errors.General.CollectionIsTooSmall/Large`

### Overloads стандартных правил (для единообразия сообщений)
- [ ] `NotEmpty<T>()` — override: использует `Errors.General.ValueIsRequired().Serialize()`
- [ ] `Length<T>(int min, int max)` — override: использует `Errors.General.InvalidLength(min, max).Serialize()`

### DI Registration
- [ ] `ServiceCollectionExtensions.AddKotoValidation(services, assemblies[])` — регистрирует все `IValidator<T>` из указанных assembly

## Тесты (Koto.Validation.Tests)
- [ ] MustBeValueObject: Success path — validator passes
- [ ] MustBeValueObject: Failure path — validation error содержит `Error.Serialize()`
- [ ] MustBeEntity: Success и Failure
- [ ] ListMustContainNumberOfItems: min boundary, max boundary, valid range
- [ ] NotEmpty и Length overloads используют General errors
