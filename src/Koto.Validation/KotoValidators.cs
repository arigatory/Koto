using FluentValidation;
using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// FluentValidation extension methods that delegate to domain factory methods,
/// keeping validation logic in one place (the domain model).
/// </summary>
public static class KotoValidators
{
    /// <summary>
    /// Validates a <see cref="string"/> property by invoking a value-object factory.
    /// The validation failure message is set to <see cref="Error.Serialize()"/> on failure.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValueObject<T, TValueObject>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<string, Result<TValueObject>> factory)
    {
        // FV v7: Custom() returns IRuleBuilderInitial; the underlying RuleBuilder<T,P>
        // implements both interfaces, so the explicit cast is safe.
        return (IRuleBuilderOptions<T, string>)(object)ruleBuilder.Custom((value, ctx) =>
        {
            var result = factory(value);
            if (result.IsFailure)
                ctx.AddFailure(result.Error.Serialize());
        });
    }

    /// <summary>
    /// Validates a property of type <typeparamref name="TElement"/> by invoking an entity factory.
    /// The validation failure message is set to <see cref="Error.Serialize()"/> on failure.
    /// </summary>
    public static IRuleBuilderOptions<T, TElement> MustBeEntity<T, TElement, TEntity>(
        this IRuleBuilder<T, TElement> ruleBuilder,
        Func<TElement, Result<TEntity>> factory)
    {
        return (IRuleBuilderOptions<T, TElement>)(object)ruleBuilder.Custom((value, ctx) =>
        {
            var result = factory(value);
            if (result.IsFailure)
                ctx.AddFailure(result.Error.Serialize());
        });
    }

    /// <summary>
    /// Validates that a collection contains between <paramref name="min"/> and <paramref name="max"/> items.
    /// Uses <see cref="Errors.General.CollectionIsTooSmall"/> and <see cref="Errors.General.CollectionIsTooLarge"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, IEnumerable<TElement>> ListMustContainNumberOfItems<T, TElement>(
        this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder,
        int? min = null,
        int? max = null)
    {
        return (IRuleBuilderOptions<T, IEnumerable<TElement>>)(object)ruleBuilder.Custom((value, ctx) =>
        {
            var list = value as IList<TElement> ?? value.ToList();
            if (min.HasValue && list.Count < min.Value)
                ctx.AddFailure(Errors.General.CollectionIsTooSmall(min.Value, list.Count).Serialize());
            else if (max.HasValue && list.Count > max.Value)
                ctx.AddFailure(Errors.General.CollectionIsTooLarge(max.Value, list.Count).Serialize());
        });
    }

    /// <summary>
    /// Overrides the default "not empty" message with <see cref="Errors.General.ValueIsRequired()"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string> NotEmptyWithKotoError<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage(Errors.General.ValueIsRequired().Serialize());
    }

    /// <summary>
    /// Overrides the default length message with <see cref="Errors.General.InvalidLength"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string> LengthWithKotoError<T>(
        this IRuleBuilder<T, string> ruleBuilder, int min, int max)
    {
        return ruleBuilder
            .Length(min, max)
            .WithMessage(Errors.General.InvalidLength(min, max).Serialize());
    }
}
