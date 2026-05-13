using AwesomeAssertions;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Koto.Domain;

namespace Koto.Testing.Assertions;

/// <summary>Extension method entry point for <see cref="Result{T}"/> assertions.</summary>
public static class ResultAssertionsExtensions
{
    /// <summary>Returns an assertions object for <paramref name="result"/>.</summary>
    public static ResultAssertions<T> Should<T>(this Result<T> result) =>
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
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .FailWith("Expected result to be a success{reason}, but it failed with error {0}.",
                Subject.IsFailure ? Subject.Error.Code : "(none)");

        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure.</summary>
    public AndConstraint<ResultAssertions<T>> BeFailure(string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure)
            .FailWith("Expected result to be a failure{reason}, but it succeeded with value {0}.",
                Subject.IsSuccess ? Subject.Value : default);

        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a failure with the specified error code.</summary>
    public AndConstraint<ResultAssertions<T>> BeFailureWith(
        string errorCode, string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsFailure)
            .FailWith("Expected result to be a failure with code {0}{reason}, but it succeeded.", errorCode)
            .Then
            .ForCondition(Subject.IsFailure && Subject.Error.Code == errorCode)
            .FailWith("Expected result error code to be {0}{reason}, but found {1}.",
                errorCode, Subject.IsFailure ? Subject.Error.Code : "(none)");

        return new AndConstraint<ResultAssertions<T>>(this);
    }

    /// <summary>Asserts that the result is a success with the specified value.</summary>
    public AndConstraint<ResultAssertions<T>> HaveValue(
        T expected, string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject.IsSuccess)
            .FailWith("Expected result to have value {0}{reason}, but it failed with error {1}.",
                expected, Subject.IsFailure ? Subject.Error.Code : "(none)")
            .Then
            .ForCondition(Equals(Subject.Value, expected))
            .FailWith("Expected result value to be {0}{reason}, but found {1}.", expected, Subject.Value);

        return new AndConstraint<ResultAssertions<T>>(this);
    }
}
