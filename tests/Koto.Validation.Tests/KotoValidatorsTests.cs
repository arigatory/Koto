using AwesomeAssertions;
using FluentValidation;
using Koto.Domain;
using Koto.Validation;

namespace Koto.Validation.Tests;

public class KotoValidatorsTests
{
    // ── Value Object fake ──────────────────────────────────────────────────────

    private sealed class Email : ValueObject
    {
        public string Value { get; }
        private Email(string v) => Value = v;

        public static Result<Email> Create(string v) =>
            string.IsNullOrWhiteSpace(v) ? Errors.General.ValueIsRequired() :
            v.Length > 50               ? Errors.General.InvalidLength(1, 50) :
                                          new Email(v);

        protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    }

    private sealed class Request { public string Email { get; init; } = ""; }

    private sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator() =>
            RuleFor(x => x.Email).MustBeValueObject(Email.Create);
    }

    // ── MustBeValueObject ──────────────────────────────────────────────────────

    [Fact]
    public void MustBeValueObject_passes_for_valid_value()
    {
        var result = new RequestValidator().Validate(new Request { Email = "a@b.com" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MustBeValueObject_fails_and_message_is_Error_Serialize()
    {
        var result = new RequestValidator().Validate(new Request { Email = "" });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Be(Errors.General.ValueIsRequired().Serialize());
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
        var result = new ListValidator().Validate(new ListRequest { Tags = [] });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Be(Errors.General.CollectionIsTooSmall(1, 0).Serialize());
    }

    [Fact]
    public void ListMustContainNumberOfItems_fails_when_too_large()
    {
        var result = new ListValidator().Validate(new ListRequest { Tags = ["a", "b", "c", "d"] });

        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Be(Errors.General.CollectionIsTooLarge(3, 4).Serialize());
    }

    // ── NotEmptyWithKotoError / LengthWithKotoError ────────────────────────────

    private sealed class NameValidator : AbstractValidator<Request>
    {
        public NameValidator()
        {
            RuleFor(x => x.Email).NotEmptyWithKotoError();
            RuleFor(x => x.Email).LengthWithKotoError(2, 10);
        }
    }

    [Fact]
    public void NotEmptyWithKotoError_uses_general_error_message()
    {
        var result = new NameValidator().Validate(new Request { Email = "" });

        result.Errors.Should().Contain(e =>
            e.ErrorMessage == Errors.General.ValueIsRequired().Serialize());
    }

    [Fact]
    public void LengthWithKotoError_uses_general_error_message()
    {
        var result = new NameValidator().Validate(new Request { Email = "x" });

        result.Errors.Should().Contain(e =>
            e.ErrorMessage == Errors.General.InvalidLength(2, 10).Serialize());
    }
}
