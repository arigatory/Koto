using Koto.Domain;
using AwesomeAssertions;

namespace Koto.Domain.Tests;

public class EntityTests
{
    private sealed class Product : Entity<Guid>
    {
        public Product(Guid id) : base(id) { }
    }

    private sealed class Order : Entity<Guid>
    {
        public Order(Guid id) : base(id) { }
    }

    [Fact]
    public void Equal_when_same_id_and_type()
    {
        var id = Guid.NewGuid();
        var a = new Product(id);
        var b = new Product(id);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Not_equal_when_different_id()
    {
        var a = new Product(Guid.NewGuid());
        var b = new Product(Guid.NewGuid());

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Not_equal_when_different_type_same_id()
    {
        var id = Guid.NewGuid();
        var product = new Product(id);
        var order = new Order(id);

        product.Equals(order).Should().BeFalse();
    }

    [Fact]
    public void Equal_to_self()
    {
        var product = new Product(Guid.NewGuid());

        product.Equals(product).Should().BeTrue();
    }
}
