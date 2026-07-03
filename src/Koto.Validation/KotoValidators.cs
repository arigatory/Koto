using FluentValidation;
using FluentValidation.Results;
using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// FluentValidation extension methods that delegate to domain factory methods,
/// keeping validation logic in one place (the domain model). Each domain
/// <see cref="Error"/> travels as structured state (<see cref="ValidationFailure.CustomState"/>
/// + <see cref="ValidationFailure.ErrorCode"/>) so the pipeline can surface real error
/// codes per field instead of concatenated message strings.
/// </summary>
public static class KotoValidators
{
    /// <summary>
    /// Validates a property by invoking a domain value-object factory
    /// (e.g. <c>RuleFor(x =&gt; x.Email).MustBeValueObject(Email.Create)</c>).
    /// The validation logic stays in the domain; the validator only calls it.
    /// Works for any source type: <see cref="string"/>, <see cref="int"/>, <see cref="Guid"/>, …
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, TSource> MustBeValueObject<T, TSource, TValueObject>(
        this IRuleBuilder<T, TSource> ruleBuilder,
        Func<TSource, Result<TValueObject>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return ruleBuilder.Custom((value, ctx) =>
        {
            var result = factory(value);
            if (result.IsSuccess) return;
            foreach (var error in result.Errors)
                ctx.AddFailure(CreateFailure(ctx.PropertyPath, error));
        });
    }

    /// <summary>
    /// Validates a property by invoking a domain entity factory. Same contract as
    /// <see cref="MustBeValueObject{T,TSource,TValueObject}"/> — kept as a separate name
    /// so validators read naturally for entities.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, TElement> MustBeEntity<T, TElement, TEntity>(
        this IRuleBuilder<T, TElement> ruleBuilder,
        Func<TElement, Result<TEntity>> factory)
        => ruleBuilder.MustBeValueObject(factory);

    /// <summary>
    /// Validates that a collection contains between <paramref name="min"/> and <paramref name="max"/> items.
    /// Uses <see cref="Errors.General.CollectionIsTooSmall"/> and <see cref="Errors.General.CollectionIsTooLarge"/>.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, IEnumerable<TElement>> ListMustContainNumberOfItems<T, TElement>(
        this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder,
        int? min = null,
        int? max = null)
    {
        return ruleBuilder.Custom((value, ctx) =>
        {
            var list = value as IList<TElement> ?? value.ToList();
            if (min.HasValue && list.Count < min.Value)
                ctx.AddFailure(CreateFailure(ctx.PropertyPath, Errors.General.CollectionIsTooSmall(min.Value, list.Count)));
            else if (max.HasValue && list.Count > max.Value)
                ctx.AddFailure(CreateFailure(ctx.PropertyPath, Errors.General.CollectionIsTooLarge(max.Value, list.Count)));
        });
    }

    /// <summary>
    /// Applies <c>NotEmpty</c> with the Koto <see cref="Errors.General.ValueIsRequired"/> error
    /// (code and message) instead of the default FluentValidation message.
    /// </summary>
    public static IRuleBuilderOptions<T, string> NotEmptyWithKotoError<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        var error = Errors.General.ValueIsRequired();
        return ruleBuilder
            .NotEmpty()
            .WithErrorCode(error.Code)
            .WithMessage(error.Message)
            .WithState(_ => error);
    }

    /// <summary>
    /// Applies <c>Length</c> with the Koto <see cref="Errors.General.InvalidLength"/> error
    /// (code and message) instead of the default FluentValidation message.
    /// </summary>
    public static IRuleBuilderOptions<T, string> LengthWithKotoError<T>(
        this IRuleBuilder<T, string> ruleBuilder, int min, int max)
    {
        var error = Errors.General.InvalidLength(min, max);
        return ruleBuilder
            .Length(min, max)
            .WithErrorCode(error.Code)
            .WithMessage(error.Message)
            .WithState(_ => error);
    }

    private static ValidationFailure CreateFailure(string propertyPath, Error error) =>
        new(propertyPath, error.Message)
        {
            ErrorCode = error.Code,
            CustomState = error,
        };
}
