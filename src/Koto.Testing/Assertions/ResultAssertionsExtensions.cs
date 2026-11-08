using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Koto.Application;
using Koto.Domain;

namespace Koto.Testing.Assertions;

/// <summary>Extension method entry points for <see cref="Result{T}"/> and <see cref="AppResult{T}"/> assertions.</summary>
public static class ResultAssertionsExtensions
{
    /// <summary>Returns an assertions object for a domain <paramref name="result"/>.</summary>
    public static ResultAssertions<T> Should<T>(this Result<T> result) =>
        new(result, AssertionChain.GetOrCreate());

    /// <summary>Returns an assertions object for an application <paramref name="result"/>.</summary>
    public static AppResultAssertions<T> Should<T>(this AppResult<T> result) =>
        new(result, AssertionChain.GetOrCreate());
}

/// <summary>AwesomeAssertions assertions for <see cref="Result{T}"/>.</summary>
/// <typeparam name="T">The result value type.</typeparam>
public sealed class ResultAssertions<T> : ReferenceTypeAssertions<Result<T>, ResultAssertions<T>>
{
    /// <inheritdoc/>
    public ResultAssertions(Result<T> subject, AssertionChain assertionChain)
        : base(subject, assertionChain) { }

    /// <inheritdoc/>
    protected override string Identifier => "result";

    /// <summary>Asserts that the result is a success.</summary>
    public AndConstraint<ResultAssertions<T>> BeSuccess(string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeSuccess(CurrentAssertionChain, Subject, Subject.IsFailure ? Subject.FirstError.Code : null, because, becauseArgs);
        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure.</summary>
    public AndConstraint<ResultAssertions<T>> BeFailure(string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeFailure(CurrentAssertionChain, Subject, Subject.IsSuccess ? Subject.Value : default, because, becauseArgs);
        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure whose first error has <paramref name="errorCode"/>.</summary>
    public AndConstraint<ResultAssertions<T>> BeFailureWith(string errorCode, string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeFailureWith(CurrentAssertionChain, Subject, errorCode, because, becauseArgs);
        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure carrying exactly the specified error codes, in order.</summary>
    public AndConstraint<ResultAssertions<T>> HaveErrors(params string[] errorCodes)
    {
        ResultAssertionCore.HaveErrors(CurrentAssertionChain, Subject, errorCodes);
        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a success with the specified value.</summary>
    public AndConstraint<ResultAssertions<T>> HaveValue(T expected, string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.HaveValue(CurrentAssertionChain, Subject, Subject.IsSuccess ? Subject.Value : default, expected, because, becauseArgs);
        return new AndConstraint<ResultAssertions<T>>(this);
    }
}

/// <summary>AwesomeAssertions assertions for <see cref="AppResult{T}"/>.</summary>
/// <typeparam name="T">The result value type.</typeparam>
public sealed class AppResultAssertions<T> : ReferenceTypeAssertions<AppResult<T>, AppResultAssertions<T>>
{
    /// <inheritdoc/>
    public AppResultAssertions(AppResult<T> subject, AssertionChain assertionChain)
        : base(subject, assertionChain) { }

    /// <inheritdoc/>
    protected override string Identifier => "result";

    /// <summary>Asserts that the result is a success.</summary>
    public AndConstraint<AppResultAssertions<T>> BeSuccess(string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeSuccess(CurrentAssertionChain, Subject, Subject.IsFailure ? Subject.FirstError.Code : null, because, becauseArgs);
        return new AndConstraint<AppResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure.</summary>
    public AndConstraint<AppResultAssertions<T>> BeFailure(string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeFailure(CurrentAssertionChain, Subject, Subject.IsSuccess ? Subject.Value : default, because, becauseArgs);
        return new AndConstraint<AppResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure whose first error has <paramref name="errorCode"/>.</summary>
    public AndConstraint<AppResultAssertions<T>> BeFailureWith(string errorCode, string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.BeFailureWith(CurrentAssertionChain, Subject, errorCode, because, becauseArgs);
        return new AndConstraint<AppResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure carrying exactly the specified error codes, in order.</summary>
    public AndConstraint<AppResultAssertions<T>> HaveErrors(params string[] errorCodes)
    {
        ResultAssertionCore.HaveErrors(CurrentAssertionChain, Subject, errorCodes);
        return new AndConstraint<AppResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a success with the specified value.</summary>
    public AndConstraint<AppResultAssertions<T>> HaveValue(T expected, string because = "", params object[] becauseArgs)
    {
        ResultAssertionCore.HaveValue(CurrentAssertionChain, Subject, Subject.IsSuccess ? Subject.Value : default, expected, because, becauseArgs);
        return new AndConstraint<AppResultAssertions<T>>(this);
    }

    /// <summary>
    /// Asserts that the result is a failure with at least one error bound to <paramref name="field"/>.
    /// The comparison ignores case, so the wire name (<c>"email"</c>) and <c>nameof(cmd.Email)</c> both match.
    /// </summary>
    public AndConstraint<AppResultAssertions<T>> HaveErrorForField(string field, string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure)
            .FailWith("Expected result to have an error for field {0}{reason}, but it succeeded.", field)
            .Then
            .ForCondition(Subject.IsFailure && Subject.Errors.Any(e => string.Equals(e.Field, field, StringComparison.OrdinalIgnoreCase)))
            .FailWith("Expected result to have an error for field {0}{reason}, but found fields {1}.",
                field, Subject.Errors.Select(e => e.Field ?? "(none)").ToArray());

        return new AndConstraint<AppResultAssertions<T>>(this);
    }
}

/// <summary>Shared assertion logic over <see cref="IResultBase"/>.</summary>
internal static class ResultAssertionCore
{
    public static void BeSuccess(AssertionChain chain, IResultBase subject, string? firstErrorCode, string because, object[] becauseArgs) =>
        chain.BecauseOf(because, becauseArgs)
            .ForCondition(subject.IsSuccess)
            .FailWith("Expected result to be a success{reason}, but it failed with error {0}.", firstErrorCode ?? "(none)");

    public static void BeFailure(AssertionChain chain, IResultBase subject, object? value, string because, object[] becauseArgs) =>
        chain.BecauseOf(because, becauseArgs)
            .ForCondition(subject.IsFailure)
            .FailWith("Expected result to be a failure{reason}, but it succeeded with value {0}.", value);

    public static void BeFailureWith(AssertionChain chain, IResultBase subject, string errorCode, string because, object[] becauseArgs)
    {
        var actual = subject.IsFailure ? subject.Errors[0].Code : "(none)";
        chain.BecauseOf(because, becauseArgs)
            .ForCondition(subject.IsFailure)
            .FailWith("Expected result to be a failure with code {0}{reason}, but it succeeded.", errorCode)
            .Then
            .ForCondition(actual == errorCode)
            .FailWith("Expected result error code to be {0}{reason}, but found {1}.", errorCode, actual);
    }

    public static void HaveErrors(AssertionChain chain, IResultBase subject, string[] errorCodes)
    {
        var actual = subject.Errors.Select(e => e.Code).ToArray();
        chain.ForCondition(subject.IsFailure)
            .FailWith("Expected result to be a failure with codes {0}, but it succeeded.", errorCodes)
            .Then
            .ForCondition(actual.SequenceEqual(errorCodes))
            .FailWith("Expected result error codes to be {0}, but found {1}.", errorCodes, actual);
    }

    public static void HaveValue<T>(AssertionChain chain, IResultBase subject, T? actualValue, T expected, string because, object[] becauseArgs)
    {
        var firstErrorCode = subject.IsFailure ? subject.Errors[0].Code : "(none)";
        chain.BecauseOf(because, becauseArgs)
            .ForCondition(subject.IsSuccess)
            .FailWith("Expected result to have value {0}{reason}, but it failed with error {1}.", expected, firstErrorCode)
            .Then
            .ForCondition(Equals(actualValue, expected))
            .FailWith("Expected result value to be {0}{reason}, but found {1}.", expected, actualValue);
    }
}
