# Koto.Testing.Architecture — Plan

**Phase:** 4 | **Status:** NOT STARTED
**Depends on:** ArchUnitNET (Apache 2.0)

---

## Цель

Fitness Functions как код — автоматические архитектурные тесты, которые запускаются в CI и не дают архитектуре деградировать. Проверяют зависимости между слоями, naming conventions, запрещённые паттерны.

## Что такое Fitness Function

Архитектурный тест, который верифицирует конкретное архитектурное решение:

```csharp
// "Domain не должен знать об EF Core" — запускается в CI как обычный xUnit тест
[Fact]
public void Domain_ShouldNotReference_EntityFrameworkCore()
{
    KotoArchitecture
        .Layer("Domain").DefinedIn(DomainAssembly)
        .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore");
}
```

## Checklist

### Fluent Architecture Rules (обёртка над ArchUnitNET)
- [ ] `KotoArchitecture` — static entry point:
  - `Layer(name).DefinedIn(assembly)` — определяет слой
  - `Classes().That().ResideIn(layer)` — фильтр классов
  - `ShouldNot().HaveDependencyOn(namespace)` — запрет зависимостей
  - `Should().BeSealed()`, `Should().HaveNameEndingWith(suffix)` — naming rules

### Pre-built Rule Sets
- [ ] `KotoLayerRules.ForCleanArchitecture(domainAssembly, appAssembly, infraAssembly, apiAssembly)` — стандартные правила:
  - Domain не зависит ни от чего выше
  - Application зависит только от Domain
  - Infrastructure не зависит от API
  - API не зависит от Infrastructure напрямую

- [ ] `KotoNamingRules.ForKoto(assembly)` — naming conventions:
  - Агрегаты без суффикса
  - Domain events с суффиксом `DomainEvent`
  - Integration events с суффиксом `IntegrationEvent`
  - Handlers с суффиксом `Handler`
  - Validators с суффиксом `Validator`

### Custom Rule Builder
- [ ] `ArchitectureRuleBuilder` — для кастомных правил конкретного проекта

## Пример: полный набор fitness functions

```csharp
public class ArchitectureFitnessTests
{
    private static readonly Architecture Arch =
        new ArchLoader().LoadAssemblies(
            typeof(Order).Assembly,          // Domain
            typeof(PlaceOrderCommand).Assembly, // Application
            typeof(AppDbContext).Assembly,   // Infrastructure
            typeof(PlaceOrderEndpoint).Assembly // Api
        ).Build();

    [Fact]
    public void CleanArchitecture_LayerDependencies_AreRespected() =>
        KotoLayerRules.ForCleanArchitecture(...).Check(Arch);

    [Fact]
    public void DomainEvents_MustEndWith_DomainEvent() =>
        KotoNamingRules.DomainEventsSuffix(typeof(Order).Assembly).Check(Arch);

    [Fact]
    public void Handlers_MustBeInternal_OrSealed() =>
        KotoArchitecture
            .Classes().That().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .Check(Arch);

    [Fact]
    public void Domain_MustNotUse_DateTime_Directly() =>
        KotoArchitecture
            .Layer("Domain").DefinedIn(typeof(Order).Assembly)
            .ShouldNot().HaveDependencyOn("System.DateTime")
            .BecauseOf("use IClock abstraction for testability")
            .Check(Arch);
}
```

## Тесты
- [ ] KotoLayerRules срабатывает при нарушении зависимости
- [ ] KotoNamingRules обнаруживает неправильно именованные классы
- [ ] Custom rule builder работает для произвольных правил
