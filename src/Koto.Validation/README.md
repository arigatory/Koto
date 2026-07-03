# Koto.Validation

FluentValidation extensions and a CQRS pipeline behavior for the [Koto](https://github.com/arigatory/Koto) suite.

> Uses **FluentValidation v12** (Apache 2.0 on all versions), pinned to `[12.0.0,13.0.0)`.

## Install

```bash
dotnet add package Koto.Validation
```

## What's included

| Type | Purpose |
|---|---|
| `KotoValidators.MustBeValueObject<T, TSource, TValueObject>` | Validates a property by calling a domain factory (`Func<TSource, Result<TVO>>`) — any source type: `string`, `int`, `Guid`, … |
| `KotoValidators.MustBeEntity<T, TElement, TEntity>` | Same contract for entity factories (reads naturally for entities) |
| `KotoValidators.ListMustContainNumberOfItems` | Min/max count rule for collections |
| `KotoValidators.NotEmptyWithKotoError` / `LengthWithKotoError` | Built-in rules carrying `Errors.General` codes |
| `ValidationBehavior<TRequest, TResponse>` | Pipeline middleware — runs all validators (async-capable) before the handler |
| `ServiceCollectionExtensions.AddKotoValidation` | Registers the behavior and scans assemblies for validators |

## Usage

### Register

```csharp
builder.Services.AddKotoValidation(typeof(Program).Assembly);
// Registers ValidationBehavior<,> as an open generic and scans for IValidator<T> —
// no extra registration needed. Recommended behavior order: Logging → Validation → Transaction.
```

### Write a validator — one source of truth

The validation logic lives in the domain factory; the validator only calls it:

```csharp
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Email).MustBeValueObject(Email.Create);      // string source
        RuleFor(x => x.Quantity).MustBeValueObject(Quantity.Create); // int source — any type works
        RuleFor(x => x.Items).ListMustContainNumberOfItems(1, 100);
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
```

Validators are keyed by the **concrete** command type and discovered by the pipeline
automatically (the dispatcher closes `ValidationBehavior<,>` over `PlaceOrderCommand`).

### Structured errors

Each domain `Error` travels as structured state — `ValidationFailure.CustomState` +
`ErrorCode` — not as a serialized message string. On failure, `ValidationBehavior`
returns a `Result` carrying **one `Error` per validation failure**, each with its real
`Code`, `Message`, and `Field` (the property name). Nothing is collapsed into a single
string; HTTP layers can render RFC 7807 validation problem details per field
(see `Koto.Api.AspNetCore`).

Async rules (`MustAsync`, `CustomAsync`) are fully supported — the behavior calls `ValidateAsync`.
