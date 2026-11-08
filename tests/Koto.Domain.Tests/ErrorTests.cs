using AwesomeAssertions;
using Koto.Domain;

namespace Koto.Domain.Tests;

public class ErrorTests
{
    private sealed record BadCode() : Error("bad", "Bad code.");

    private sealed record OtpTooManyAttempts(int RetryAfterSeconds)
        : Error("identity.otp.too-many-attempts", "Too many attempts.");

    private sealed record OtherWithSameCode() : Error("test.error.same", "Same.");

    private sealed record AnotherWithSameCode() : Error("test.error.same", "Same.");

    [Fact]
    public void Derived_record_carries_code_and_message()
    {
        var error = new Errors.General.NotFound("order", 7);

        error.Code.Should().Be("general.entity.not-found");
        error.Message.Should().Contain("order").And.Contain("7");
    }

    [Fact]
    public void Constructing_error_with_invalid_code_throws()
    {
        var act = () => new BadCode();

        act.Should().Throw<ArgumentException>().WithMessage("*bad*");
    }

    [Fact]
    public void Records_with_same_code_and_data_are_equal()
    {
        new OtpTooManyAttempts(30).Should().Be(new OtpTooManyAttempts(30));
    }

    [Fact]
    public void Records_with_different_data_are_not_equal()
    {
        new OtpTooManyAttempts(30).Should().NotBe(new OtpTooManyAttempts(60));
    }

    [Fact]
    public void Records_of_different_type_are_not_equal()
    {
        new OtherWithSameCode().Should().NotBe(new AnotherWithSameCode());
    }

    [Fact]
    public void Message_can_be_replaced_with_expression_keeping_the_type()
    {
        Error error = new OtpTooManyAttempts(1) with { Message = "Row 3: too many attempts." };

        error.Should().BeOfType<OtpTooManyAttempts>();
        error.Message.Should().Be("Row 3: too many attempts.");
        error.Code.Should().Be("identity.otp.too-many-attempts");
    }

    [Fact]
    public void General_errors_use_three_segment_codes()
    {
        new Errors.General.ValueIsRequired().Code.Should().Be("general.value.is-required");
        new Errors.General.InvalidLength(3, 10).Code.Should().Be("general.value.invalid-length");
        new Errors.General.NotFound("Order").Code.Should().Be("general.entity.not-found");
        new Errors.General.CollectionIsTooSmall(2, 1).Code.Should().Be("general.collection.too-small");
        new Errors.General.CollectionIsTooLarge(5, 10).Code.Should().Be("general.collection.too-large");
    }

    [Fact]
    public void General_NotFound_without_id_mentions_entity_only()
    {
        var error = new Errors.General.NotFound("Order");

        error.Message.Should().Be("'Order' was not found.");
        error.Id.Should().BeNull();
    }

    [Fact]
    public void General_errors_expose_their_data_as_properties()
    {
        var error = new Errors.General.InvalidLength(3, 10);

        error.Min.Should().Be(3);
        error.Max.Should().Be(10);
        error.Message.Should().Be("Length must be between 3 and 10.");
    }
}
