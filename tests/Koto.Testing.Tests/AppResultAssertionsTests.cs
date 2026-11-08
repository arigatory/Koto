using AwesomeAssertions;
using Koto.Application;
using Koto.Domain;
using Koto.Testing.Assertions;

namespace Koto.Testing.Tests;

public class AppResultAssertionsTests
{
    private static readonly EnrichedError NotFound = new TestError("orders.order.not-found", "Not found").Enrich();
    private static readonly EnrichedError EmailRequired = new Errors.General.ValueIsRequired().ForField("Email");

    [Fact]
    public void BeSuccess_and_HaveValue_pass_on_success()
    {
        AppResult<int>.Success(42).Should().BeSuccess().And.HaveValue(42);
    }

    [Fact]
    public void BeSuccess_fails_on_failure()
    {
        var act = () => AppResult<int>.Failure(NotFound).Should().BeSuccess();

        act.Should().Throw<Exception>().WithMessage("*orders.order.not-found*");
    }

    [Fact]
    public void BeFailureWith_checks_the_first_error_code()
    {
        AppResult<int>.Failure([NotFound, EmailRequired]).Should().BeFailureWith("orders.order.not-found");
    }

    [Fact]
    public void HaveErrors_checks_all_codes_in_order()
    {
        AppResult<int>.Failure([NotFound, EmailRequired]).Should().HaveErrors("orders.order.not-found", "general.value.is-required");
    }

    [Fact]
    public void HaveErrorForField_passes_when_an_error_is_bound_to_the_field()
    {
        AppResult<int>.Failure([NotFound, EmailRequired]).Should().HaveErrorForField("Email");
    }

    [Fact]
    public void HaveErrorForField_fails_when_no_error_is_bound_to_the_field()
    {
        var act = () => AppResult<int>.Failure(NotFound).Should().HaveErrorForField("Email");

        act.Should().Throw<Exception>().WithMessage("*Email*");
    }
}
