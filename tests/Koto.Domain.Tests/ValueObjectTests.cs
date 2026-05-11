using Koto.Domain;
using AwesomeAssertions;

namespace Koto.Domain.Tests;

public class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    [Fact]
    public void Equal_when_components_match()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "USD");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Not_equal_when_components_differ()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "USD");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Not_equal_to_null()
    {
        var a = new Money(10m, "USD");

        a.Equals(null).Should().BeFalse();
        (a == null).Should().BeFalse();
    }

    [Fact]
    public void Not_equal_to_different_type()
    {
        var a = new Money(10m, "USD");

        a.Equals("not a money").Should().BeFalse();
    }
}
