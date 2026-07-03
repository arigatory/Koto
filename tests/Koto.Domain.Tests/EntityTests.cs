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

    [Fact]
    public void Transient_entities_with_default_id_are_not_equal()
    {
        var a = new Product(Guid.Empty);
        var b = new Product(Guid.Empty);

        a.IsTransient.Should().BeTrue();
        a.Equals(b).Should().BeFalse();
        (a == b).Should().BeFalse();
    }

    [Fact]
    public void Transient_entity_is_still_equal_to_itself()
    {
        var a = new Product(Guid.Empty);

        a.Equals(a).Should().BeTrue();
    }

    [Fact]
    public void Entity_with_assigned_id_is_not_transient()
    {
        new Product(Guid.NewGuid()).IsTransient.Should().BeFalse();
    }
}
