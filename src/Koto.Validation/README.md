# Koto.Validation

FluentValidation extensions and the validation pipeline behavior for the
[Koto](https://github.com/arigatory/Koto) suite.

> Uses **FluentValidation v12** (Apache 2.0 on all versions), pinned to `[12.0.0,13.0.0)`.

## Install

```bash
dotnet add package Koto.Validation
```

## What's included

| Type | Purpose |
|---|---|
| `KotoValidators.MustBeValueObject<T, TSource, TValueObject>` | Validates a property by calling a domain factory (`Func<TSource, Result<TVO>>`) — any source type |
| `KotoValidators.ListMustContainNumberOfItems` | Min/max count rule with `Errors.General.CollectionIsTooSmall/Large` |
| `ValidationBehavior<TRequest, TResponse>` | Runs all validators for the request **sequentially** before the handler |
| `ValidationRuleFailed` | The error for rules not backed by a domain factory (`validation.rule.failed`, `details.rule` = the rule's code) |
| `AddValidationBehavior()` | Puts the behavior at an explicit position in `KotoApplicationOptions` |
| `AddKotoValidation(assemblies)` | Scans `IValidator<T>` implementations (registers the behavior if it was not added through the options) |

## Register

```csharp
builder.Services.AddKotoApplication(
    o => o.AddLoggingBehavior().AddValidationBehavior().AddTransactionBehavior(),  // Logging → Validation → Transaction
    typeof(PlaceOrderHandler).Assembly);
builder.Services.AddKotoValidation(typeof(PlaceOrderValidator).Assembly);
```

With this order a validation failure never opens a transaction. Calling `AddKotoValidation` alone
still works — the behavior is then appended after the behaviors configured in the options.

## Write a validator — one source of truth

The validation logic lives in the domain factory; the validator only calls it:

```csharp
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Email).MustBeValueObject(Email.Create);      // string source
        RuleFor(x => x.Quantity).MustBeValueObject(Quantity.Create); // int source — any type works
        RuleFor(x => x.Items).ListMustContainNumberOfItems(1, 100);
        RuleFor(x => x.CustomerId).NotEmpty();                       // plain FluentValidation rule
    }
}
```

Validators are keyed by the **concrete** command type and discovered by the pipeline automatically.

## What the handler never sees, and what the client gets

On failure the behavior returns an `AppResult` carrying **one `EnrichedError` per failure**, each
bound to its field:

- a `MustBeValueObject` failure keeps the domain error — `general.value.is-required`,
  `identity.email.invalid`, … — with its record properties as `details`;
- any other rule (`NotEmpty()`, `Length()`, `MustAsync()`) becomes `validation.rule.failed` with
  `details.rule` set to the rule's error code (`NotEmptyValidator` or your `WithErrorCode` value).

Nothing is collapsed into a string; the API layer renders every error inside the response envelope.

Validators run one after another, so async rules may share scoped services such as a DbContext.

## Upgrading from 0.4

0.5 adds `AddValidationBehavior()` to position the behavior explicitly, removes `MustBeEntity`, `NotEmptyWithKotoError` and `LengthWithKotoError`, runs validators sequentially, and reports plain rules as `validation.rule.failed` (`details.rule`) instead of the FluentValidation error code. The step-by-step guide for the whole suite, with before/after code, is
[docs/migrations/0.4-to-0.5.md](https://github.com/arigatory/Koto/blob/main/docs/migrations/0.4-to-0.5.md).
