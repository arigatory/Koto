# Koto.Validation

FluentValidation v7 extensions and a CQRS pipeline behavior for the [Koto](https://github.com/arigatory/Koto) suite.

> Uses **FluentValidation v7** (Apache 2.0). v8+ switched to a commercial license and is not used.

## Install

```bash
dotnet add package Koto.Validation
```

## What's included

| Type | Purpose |
|---|---|
| `KotoValidators.MustBeValueObject<T>` | Validates that a string can construct a `ValueObject`-derived type |
| `KotoValidators.MustBeEntity<T>` | Validates a strongly-typed entity reference |
| `KotoValidators.ListMustContainNumberOfItems` | Min/max count rule for collections |
| `ValidationBehavior<TRequest, TResponse>` | Pipeline middleware — runs all validators before the handler |
| `ServiceCollectionExtensions.AddKotoValidation` | Registers the behavior and scans assemblies for validators |

## Usage

### Register

```csharp
builder.Services.AddKotoValidation(typeof(Program).Assembly);
// Also add to your pipeline:
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

### Write a validator

```csharp
public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.Items).ListMustContainNumberOfItems(1, 100);
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
```

If validation fails, `ValidationBehavior` returns a `Result<T>.Failure` with the first validation error — no exceptions thrown.
