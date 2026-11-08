using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class ErrorCodeTests
{
    [Theory]
    [InlineData("orders.order.not-found")]
    [InlineData("general.value.is-required")]
    [InlineData("a.b.c.d")]
    [InlineData("a1.b2.c3")]
    public void Accepts_three_or_more_kebab_segments(string code)
    {
        ErrorCode.IsValid(code).Should().BeTrue();
        ErrorCode.Validate(code).Should().Be(code);
    }

    [Theory]
    [InlineData("general.not-found")]
    [InlineData("Orders.Order.X")]
    [InlineData("orders..x")]
    [InlineData("orders.order.x-")]
    [InlineData("orders.order.-x")]
    [InlineData("orders order.x")]
    [InlineData("orders.order.not_found")]
    [InlineData("")]
    public void Rejects_malformed_codes(string code)
    {
        ErrorCode.IsValid(code).Should().BeFalse();

        var act = () => ErrorCode.Validate(code);
        act.Should().Throw<ArgumentException>().WithMessage($"*{code}*");
    }

    [Fact]
    public void Validate_rejects_null()
    {
        var act = () => ErrorCode.Validate(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
