using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class StronglyTypedIdTests
{
    private sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value);
    private sealed record CustomerId(Guid Value) : StronglyTypedId<Guid>(Value);
    private sealed record LineNumber(int Value) : StronglyTypedId<int>(Value);

    [Fact]
    public void Equality_is_type_and_value_based()
    {
        var guid = Guid.NewGuid();

        new OrderId(guid).Should().Be(new OrderId(guid));
        new OrderId(guid).Equals(new CustomerId(guid)).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_orders_by_underlying_value_within_one_type()
    {
        var ids = new List<LineNumber> { new(3), new(1), new(2) };

        ids.Sort();

        ids.Select(x => x.Value).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void CompareTo_between_different_id_types_throws()
    {
        var guid = Guid.NewGuid();
        var act = () => new OrderId(guid).CompareTo(new CustomerId(guid));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*OrderId*CustomerId*");
    }

    [Fact]
    public void CompareTo_null_returns_positive()
    {
        new LineNumber(1).CompareTo(null).Should().BePositive();
    }
}
