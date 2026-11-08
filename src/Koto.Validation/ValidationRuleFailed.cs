using Koto.Domain;

namespace Koto.Validation;

/// <summary>
/// A FluentValidation rule that is not backed by a domain factory failed (e.g. <c>NotEmpty()</c>,
/// <c>Length()</c>, <c>MustAsync()</c>). <see cref="Rule"/> carries the rule's error code
/// (FluentValidation's <c>ErrorCode</c>) and travels as the <c>rule</c> detail.
/// </summary>
public sealed record ValidationRuleFailed : Error
{
    /// <summary>Creates the error for <paramref name="rule"/> with the rule's own <paramref name="message"/>.</summary>
    public ValidationRuleFailed(string rule, string message)
        : base("validation.rule.failed", message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        Rule = rule;
    }

    /// <summary>The failed rule's code, e.g. <c>NotEmptyValidator</c> or a custom <c>WithErrorCode</c> value.</summary>
    public string Rule { get; }
}
