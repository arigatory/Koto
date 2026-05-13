using AwesomeAssertions;
using Koto.Domain;
using Koto.Testing.Assertions;

namespace Koto.Testing.Tests;

public class ResultAssertionsTests
{
    [Fact]
    public void BeSuccess_passes_on_successful_result()
    {
        var result = Result<int>.Success(42);
        result.Should().BeSuccess();
    }

    [Fact]
    public void BeSuccess_fails_on_failed_result()
    {
        var result = Result<int>.Failure(new Error("some.error", "msg"));
        var act = () => result.Should().BeSuccess();
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void BeFailure_passes_on_failed_result()
    {
        var result = Result<int>.Failure(new Error("some.error", "msg"));
        result.Should().BeFailure();
    }

    [Fact]
    public void BeFailure_fails_on_successful_result()
    {
        var result = Result<int>.Success(1);
        var act = () => result.Should().BeFailure();
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void BeFailureWith_passes_when_error_code_matches()
    {
        var result = Result<int>.Failure(new Error("orders.order.not-found", "Not found"));
        result.Should().BeFailureWith("orders.order.not-found");
    }

    [Fact]
    public void BeFailureWith_fails_when_error_code_differs()
    {
        var result = Result<int>.Failure(new Error("orders.order.not-found", "Not found"));
        var act = () => result.Should().BeFailureWith("different.code");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void HaveValue_passes_when_value_matches()
    {
        var result = Result<string>.Success("hello");
        result.Should().HaveValue("hello");
    }

    [Fact]
    public void HaveValue_fails_when_value_differs()
    {
        var result = Result<string>.Success("hello");
        var act = () => result.Should().HaveValue("world");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void HaveValue_fails_on_failed_result()
    {
        var result = Result<string>.Failure(new Error("err", "msg"));
        var act = () => result.Should().HaveValue("hello");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Assertions_can_be_chained()
    {
        var result = Result<int>.Success(7);
        result.Should().BeSuccess().And.HaveValue(7);
    }
}
