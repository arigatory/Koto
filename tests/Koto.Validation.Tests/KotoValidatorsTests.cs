using AwesomeAssertions;
using FluentValidation;
using Koto.Domain;
using Koto.Validation;

namespace Koto.Validation.Tests;

public class KotoValidatorsTests
{
    // ── Value Object fakes ─────────────────────────────────────────────────────

    private sealed class Email : ValueObject
    {
        public string Value { get; }
        private Email(string v) => Value = v;

        public static Result<Email> Create(string v) =>
            string.IsNullOrWhiteSpace(v) ? new Errors.General.ValueIsRequired() :
            v.Length > 50               ? new Errors.General.InvalidLength(1, 50) :
                                          new Email(v);

        protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    }

    private sealed record QuantityMustBePositive(int Actual)
        : Error("test.quantity.must-be-positive", "Quantity must be positive.");

    // Non-string source: value object created from an int.
    private sealed class Quantity : ValueObject
    {
        public int Value { get; }
        private Quantity(int v) => Value = v;

        public static Result<Quantity> Create(int v) =>
            v <= 0 ? new QuantityMustBePositive(v) : new Quantity(v);

        protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    }

    private sealed class Request { public string Email { get; init; } = ""; }

    private sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() =>
            RuleFor(x => x.Email).MustBeValueObject(Email.Create);
    }

    private sealed class OrderLine { public int Quantity { get; init; } }

    private sealed class OrderLineValidator : AbstractValidator<OrderLine>
    {
        public OrderLineValidator() =>
            RuleFor(x => x.Quantity).MustBeValueObject(Quantity.Create);
    }

    // ── MustBeValueObject ──────────────────────────────────────────────────────

    [Fact]
    public void MustBeValueObject_passes_for_valid_value()
    {
        var result = new RequestValidator().Validate(new Request { Email = "a@b.com" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MustBeValueObject_carries_structured_error_on_failure()
    {
        var expected = new Errors.General.ValueIsRequired();

        var result = new RequestValidator().Validate(new Request { Email = "" });

        result.IsValid.Should().BeFalse();
        var failure = result.Errors[0];
        failure.ErrorMessage.Should().Be(expected.Message);
        failure.ErrorCode.Should().Be(expected.Code);
        failure.CustomState.Should().BeOfType<Errors.General.ValueIsRequired>();
        failure.PropertyName.Should().Be("Email");
    }

    [Fact]
    public void MustBeValueObject_works_for_non_string_source()
    {
        var result = new OrderLineValidator().Validate(new OrderLine { Quantity = -1 });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorCode.Should().Be("test.quantity.must-be-positive");
        result.Errors[0].CustomState.Should().BeOfType<QuantityMustBePositive>().Which.Actual.Should().Be(-1);
    }

    // ── ListMustContainNumberOfItems ───────────────────────────────────────────

    private sealed class ListRequest { public List<string> Tags { get; init; } = []; }

    private sealed class ListValidator : AbstractValidator<ListRequest>
    {
        public ListValidator() =>
            RuleFor(x => x.Tags).ListMustContainNumberOfItems(min: 1, max: 3);
    }

    [Fact]
    public void ListMustContainNumberOfItems_passes_for_valid_count()
    {
        var result = new ListValidator().Validate(new ListRequest { Tags = ["a", "b"] });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ListMustContainNumberOfItems_fails_when_too_small()
    {
        var expected = new Errors.General.CollectionIsTooSmall(1, 0);

        var result = new ListValidator().Validate(new ListRequest { Tags = [] });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Be(expected.Message);
        result.Errors[0].ErrorCode.Should().Be(expected.Code);
        result.Errors[0].CustomState.Should().Be(expected);
    }

    [Fact]
    public void ListMustContainNumberOfItems_fails_when_too_large()
    {
        var expected = new Errors.General.CollectionIsTooLarge(3, 4);

        var result = new ListValidator().Validate(new ListRequest { Tags = ["a", "b", "c", "d"] });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Be(expected.Message);
        result.Errors[0].ErrorCode.Should().Be(expected.Code);
    }
}
