using System.Reflection;
using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Application.Tests;

public class AppResultTests
{
    private static readonly Error A = new TestError("test.app.a", "A");
    private static readonly Error B = new TestError("test.app.b", "B");
    private static readonly EnrichedError EA = A.Enrich();
    private static readonly EnrichedError EB = B.Enrich();

    [Fact]
    public void Success_exposes_value_and_no_errors()
    {
        var result = AppResult<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_exposes_all_errors_and_first()
    {
        var result = AppResult<int>.Failure([EA, EB]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainInOrder(EA, EB);
        result.FirstError.Should().Be(EA);
    }

    [Fact]
    public void Value_throws_on_failure_and_FirstError_throws_on_success()
    {
        var failed = AppResult<int>.Failure(EA);
        var ok = AppResult<int>.Success(1);

        ((Func<int>)(() => failed.Value)).Should().Throw<InvalidOperationException>();
        ((Func<EnrichedError>)(() => ok.FirstError)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Guards_reject_null_value_and_empty_errors()
    {
        ((Action)(() => AppResult<string>.Success(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => AppResult<string>.Failure(Array.Empty<EnrichedError>()))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Implicit_from_value_enriched_error_and_enriched_list()
    {
        AppResult<string> fromValue = "x";
        AppResult<string> fromError = EA;
        AppResult<string> fromList = new ErrorList<EnrichedError>([EA, EB]);

        fromValue.Value.Should().Be("x");
        fromError.FirstError.Should().Be(EA);
        fromList.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void No_implicit_conversion_from_plain_error_or_domain_result()
    {
        var parameterTypes = typeof(AppResult<int>)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "op_Implicit")
            .Select(m => m.GetParameters()[0].ParameterType);

        parameterTypes.Should().BeEquivalentTo([typeof(int), typeof(EnrichedError), typeof(ErrorList<EnrichedError>)]);
    }

    [Fact]
    public void Enrich_converts_domain_failure_keeping_all_errors()
    {
        var app = Result<int>.Failure([A, B]).Enrich();

        app.IsFailure.Should().BeTrue();
        app.Errors.Should().HaveCount(2);
        app.Errors[0].Inner.Should().BeSameAs(A);
        app.Errors[1].Inner.Should().BeSameAs(B);
    }

    [Fact]
    public void Enrich_keeps_success_value_and_already_enriched_errors()
    {
        Result<int>.Success(7).Enrich().Value.Should().Be(7);
        Result<int>.Failure(new EnrichedError(A) { Field = "f" }).Enrich().FirstError.Field.Should().Be("f");
    }

    [Fact]
    public void Map_transforms_value_and_propagates_errors()
    {
        AppResult<int>.Success(2).Map(x => x * 3).Value.Should().Be(6);
        AppResult<int>.Failure([EA, EB]).Map(x => x * 3).Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Match_and_MatchAsync_receive_all_errors()
    {
        var failed = AppResult<int>.Failure([EA, EB]);

        failed.Match(_ => 0, errors => errors.Count).Should().Be(2);
        (await failed.MatchAsync(x => Task.FromResult(x), errors => errors.Count)).Should().Be(2);
        (await failed.MatchAsync(x => Task.FromResult(x), errors => Task.FromResult(errors.Count))).Should().Be(2);
    }

    [Fact]
    public void Companion_creates_unit_results()
    {
        AppResult.Success().IsSuccess.Should().BeTrue();
        AppResult.Failure(EA).FirstError.Should().Be(EA);
        AppResult.Failure([EA, EB]).Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Implements_IResultBase_and_IAppResultFactory()
    {
        ((IResultBase)AppResult<int>.Failure(EA)).Errors.Should().ContainSingle();
        CreateFailure<AppResult<int>>([EA, EB]).Errors.Should().HaveCount(2);
    }

    private static TResult CreateFailure<TResult>(IReadOnlyList<EnrichedError> errors)
        where TResult : IResultBase, IAppResultFactory<TResult> => TResult.FromErrors(errors);
}
