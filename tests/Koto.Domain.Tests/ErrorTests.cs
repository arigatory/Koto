using Koto.Domain;
using AwesomeAssertions;

namespace Koto.Domain.Tests;

public class ErrorTests
{
    [Fact]
    public void Field_is_optional_and_preserved_by_with_expression()
    {
        var error = new Error("general.not-found", "Item was not found.");
        error.Field.Should().BeNull();

        var withField = error with { Field = "OrderId" };
        withField.Field.Should().Be("OrderId");
        withField.Code.Should().Be("general.not-found");
    }

    [Fact]
    public void General_ValueIsRequired_has_correct_code()
    {
        Errors.General.ValueIsRequired().Code.Should().Be("general.value.is-required");
    }

    [Fact]
    public void General_NotFound_without_id()
    {
        var error = Errors.General.NotFound("Order");

        error.Code.Should().Be("general.not-found");
        error.Message.Should().Contain("Order");
    }

    [Fact]
    public void General_NotFound_with_id()
    {
        var id = Guid.NewGuid();
        var error = Errors.General.NotFound("Order", id);

        error.Message.Should().Contain(id.ToString());
    }

    [Fact]
    public void General_CollectionIsTooSmall_has_correct_code()
    {
        Errors.General.CollectionIsTooSmall(2, 1).Code
            .Should().Be("general.collection-is-too-small");
    }

    [Fact]
    public void General_CollectionIsTooLarge_has_correct_code()
    {
        Errors.General.CollectionIsTooLarge(5, 10).Code
            .Should().Be("general.collection-is-too-large");
    }

    [Fact]
    public void General_InvalidLength_has_correct_code()
    {
        Errors.General.InvalidLength(3, 10).Code.Should().Be("general.invalid-length");
    }
}
