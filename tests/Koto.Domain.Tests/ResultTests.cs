using Koto.Domain;
using AwesomeAssertions;

namespace Koto.Domain.Tests;

public class ResultTests
{
    private static readonly Error SomeError = new TestError("test.result.error", "Something went wrong.");

    [Fact]
    public void Success_result_is_success()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_result_is_failure()
    {
        var result = Result<int>.Failure(SomeError);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Value_throws_on_failure()
    {
        var result = Result<int>.Failure(SomeError);

        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FirstError_throws_on_success()
    {
        var result = Result<int>.Success(1);

        var act = () => _ = result.FirstError;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Implicit_conversion_from_error()
    {
        Result<string> result = SomeError;

        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Map_transforms_value_on_success()
    {
        var result = Result<int>.Success(5).Map(x => x * 2);

        result.Value.Should().Be(10);
    }

    [Fact]
    public void Map_propagates_error_on_failure()
    {
        var result = Result<int>.Failure(SomeError).Map(x => x * 2);

        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Bind_chains_on_success()
    {
        var result = Result<int>.Success(5)
            .Bind(x => Result<string>.Success($"value:{x}"));

        result.Value.Should().Be("value:5");
    }

    [Fact]
    public void Bind_propagates_error_on_failure()
    {
        var result = Result<int>.Failure(SomeError)
            .Bind(x => Result<string>.Success($"value:{x}"));

        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Tap_executes_action_on_success()
    {
        var tapped = 0;
        Result<int>.Success(7).Tap(x => tapped = x);

        tapped.Should().Be(7);
    }

    [Fact]
    public void Tap_does_not_execute_on_failure()
    {
        var tapped = false;
        Result<int>.Failure(SomeError).Tap(_ => tapped = true);

        tapped.Should().BeFalse();
    }

    [Fact]
    public void Match_calls_onSuccess()
    {
        var output = Result<int>.Success(3).Match(v => $"ok:{v}", errors => $"err:{errors[0].Code}");

        output.Should().Be("ok:3");
    }

    [Fact]
    public void Match_calls_onFailure()
    {
        var output = Result<int>.Failure(SomeError).Match(v => $"ok:{v}", errors => $"err:{errors[0].Code}");

        output.Should().Be("err:test.result.error");
    }

    [Fact]
    public void Ensure_keeps_success_when_predicate_passes()
    {
        var result = Result<int>.Success(10).Ensure(x => x > 0, SomeError);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Ensure_converts_to_failure_when_predicate_fails()
    {
        var result = Result<int>.Success(-1).Ensure(x => x > 0, SomeError);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Ensure_passes_through_existing_failure()
    {
        var originalError = new TestError("test.result.original", "Original error.");
        var result = Result<int>.Failure(originalError).Ensure(x => x > 0, SomeError);

        result.FirstError.Should().Be(originalError);
    }

    [Fact]
    public async Task MapAsync_transforms_on_success()
    {
        var result = await Result<int>.Success(3).MapAsync(x => Task.FromResult(x * 10));

        result.Value.Should().Be(30);
    }

    [Fact]
    public async Task MapAsync_propagates_error_on_failure()
    {
        var result = await Result<int>.Failure(SomeError).MapAsync(x => Task.FromResult(x * 10));

        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public async Task BindAsync_chains_on_success()
    {
        var result = await Result<int>.Success(4)
            .BindAsync(x => Task.FromResult(Result<string>.Success($"v:{x}")));

        result.Value.Should().Be("v:4");
    }

    [Fact]
    public async Task BindAsync_propagates_error_on_failure()
    {
        var result = await Result<int>.Failure(SomeError)
            .BindAsync(x => Task.FromResult(Result<string>.Success($"v:{x}")));

        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public async Task TapAsync_executes_on_success()
    {
        var tapped = 0;
        await Result<int>.Success(9).TapAsync(x =>
        {
            tapped = x;
            return Task.CompletedTask;
        });

        tapped.Should().Be(9);
    }

    [Fact]
    public async Task TapAsync_does_not_execute_on_failure()
    {
        var executed = false;
        await Result<int>.Failure(SomeError).TapAsync(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        executed.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureAsync_keeps_success_when_predicate_passes()
    {
        var result = await Result<int>.Success(5)
            .EnsureAsync(x => Task.FromResult(x > 0), SomeError);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureAsync_converts_to_failure_when_predicate_fails()
    {
        var result = await Result<int>.Success(-5)
            .EnsureAsync(x => Task.FromResult(x > 0), SomeError);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Should().Be(SomeError);
    }

    [Fact]
    public void Errors_convert_implicitly_to_another_result_type()
    {
        var failed = Result<string>.Failure([SomeError, new TestError("test.result.second", "Second.")]);

        Result<int> propagated = failed.Errors;

        propagated.IsFailure.Should().BeTrue();
        propagated.Errors.Should().HaveCount(2);
        propagated.Errors.Should().ContainInOrder(failed.Errors[0], failed.Errors[1]);
    }

    [Fact]
    public void Errors_is_a_typed_error_list()
    {
        Result<int>.Failure(SomeError).Errors.Should().BeOfType<ErrorList<Error>>();
    }
}
