# Koto.Validation — Plan

**Phase:** 1 | **Status:** NOT STARTED
**Depends on:** Koto.Domain + FluentValidation v7 (pinned, Apache 2.0)

---

## Цель

Мост между FluentValidation и паттерном `Result<T, Error>`. Валидационная логика живёт один раз — в фабричных методах домена. FluentValidation просто вызывает их.

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
- [ ] `MustBeValueObject<T, TValueObject>(Func<string, Result<TValueObject, Error>> factory)` — вызывает factory, при IsFailure добавляет `result.Error.Serialize()` как validation failure
- [ ] `MustBeEntity<T, TElement, TEntity>(Func<TElement, Result<TEntity, Error>> factory)` — то же для составных объектов
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
