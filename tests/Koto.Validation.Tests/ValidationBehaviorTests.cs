using AwesomeAssertions;
using FluentValidation;
using Koto.Application;
using Koto.Domain;
using Koto.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Validation.Tests;

public class ValidationBehaviorTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed record Email
    {
        public string Value { get; }
        private Email(string value) => Value = value;

        public static Result<Email> Create(string value) =>
            string.IsNullOrWhiteSpace(value) ? new Errors.General.ValueIsRequired() : new Email(value);
    }

    private sealed record CreateUserCmd(string Email) : ICommand<string>;

    private sealed class CreateUserHandler : ICommandHandler<CreateUserCmd, string>
    {
        public Task<AppResult<string>> HandleAsync(CreateUserCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(AppResult<string>.Success($"created:{cmd.Email}"));
    }

    // Validators are keyed by the CONCRETE command type and must be discovered by the pipeline.
    private sealed class CreateUserValidator : AbstractValidator<CreateUserCmd>
    {
        public CreateUserValidator() => RuleFor(x => x.Email).MustBeValueObject(Email.Create);
    }

    private sealed record DeleteUserCmd(string Email) : ICommand;

    private sealed class DeleteUserHandler : ICommandHandler<DeleteUserCmd>
    {
        public Task<AppResult<Unit>> HandleAsync(DeleteUserCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(AppResult.Success());
    }

    private sealed class DeleteUserValidator : AbstractValidator<DeleteUserCmd>
    {
        public DeleteUserValidator() => RuleFor(x => x.Email).MustBeValueObject(Email.Create);
    }

    private sealed record GetUserQuery(string Email) : IQuery<string>;

    private sealed class GetUserHandler : IQueryHandler<GetUserQuery, string>
    {
        public Task<AppResult<string>> HandleAsync(GetUserQuery query, CancellationToken ct = default) =>
            Task.FromResult(AppResult<string>.Success($"user:{query.Email}"));
    }

    private sealed class GetUserValidator : AbstractValidator<GetUserQuery>
    {
        public GetUserValidator() => RuleFor(x => x.Email).MustBeValueObject(Email.Create);
    }

    private sealed record MultiFieldCmd(string Email, string Name) : ICommand<string>;

    private sealed class MultiFieldHandler : ICommandHandler<MultiFieldCmd, string>
    {
        public Task<AppResult<string>> HandleAsync(MultiFieldCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(AppResult<string>.Success("ok"));
    }

    private sealed class MultiFieldValidator : AbstractValidator<MultiFieldCmd>
    {
        public MultiFieldValidator()
        {
            RuleFor(x => x.Email).MustBeValueObject(Email.Create);
            RuleFor(x => x.Name).MustBeValueObject(Email.Create);
        }
    }

    private sealed class AsyncRuleValidator : AbstractValidator<CreateUserCmd>
    {
        public AsyncRuleValidator()
        {
            RuleFor(x => x.Email)
                .MustAsync(async (email, _) =>
                {
                    await Task.Yield();
                    return !string.IsNullOrEmpty(email);
                })
                .WithErrorCode("users.email.async-check-failed")
                .WithMessage("Email failed the async check.");
        }
    }

    private sealed class Journal
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class SlowValidatorA(Journal journal) : AbstractValidator<CreateUserCmd>
    {
        public override async Task<FluentValidation.Results.ValidationResult> ValidateAsync(
            ValidationContext<CreateUserCmd> context, CancellationToken cancellation = default)
        {
            journal.Entries.Add("A:start");
            await Task.Delay(20, cancellation);
            journal.Entries.Add("A:end");
            return await base.ValidateAsync(context, cancellation);
        }
    }

    private sealed class SlowValidatorB(Journal journal) : AbstractValidator<CreateUserCmd>
    {
        public override async Task<FluentValidation.Results.ValidationResult> ValidateAsync(
            ValidationContext<CreateUserCmd> context, CancellationToken cancellation = default)
        {
            journal.Entries.Add("B:start");
            await Task.Delay(20, cancellation);
            journal.Entries.Add("B:end");
            return await base.ValidateAsync(context, cancellation);
        }
    }

    private sealed class TrackingHandler(Action onHandle) : ICommandHandler<CreateUserCmd, string>
    {
        public Task<AppResult<string>> HandleAsync(CreateUserCmd cmd, CancellationToken ct = default)
        {
            onHandle();
            return Task.FromResult(AppResult<string>.Success("ok"));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ICqrsDispatcher Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidationBehavior_passes_through_when_no_validators()
    {
        var dispatcher = Build(s => s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>());

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd("a@b.com"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Domain_factory_error_keeps_its_code_and_gets_the_field()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient<IValidator<CreateUserCmd>, CreateUserValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("general.value.is-required");
        result.FirstError.Field.Should().Be("Email");
        result.FirstError.Inner.Should().BeOfType<Errors.General.ValueIsRequired>();
    }

    [Fact]
    public async Task Validator_for_void_command_is_discovered()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<DeleteUserCmd>, DeleteUserHandler>();
            s.AddTransient<IValidator<DeleteUserCmd>, DeleteUserValidator>();
        });

        var result = await dispatcher.SendAsync(new DeleteUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("general.value.is-required");
    }

    [Fact]
    public async Task Validator_for_query_is_discovered()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<IQueryHandler<GetUserQuery, string>, GetUserHandler>();
            s.AddTransient<IValidator<GetUserQuery>, GetUserValidator>();
        });

        var result = await dispatcher.QueryAsync<string>(new GetUserQuery(""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("general.value.is-required");
    }

    [Fact]
    public async Task ValidationBehavior_does_not_call_handler_when_invalid()
    {
        var called = false;
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>>(_ => new TrackingHandler(() => called = true));
            s.AddTransient<IValidator<CreateUserCmd>, CreateUserValidator>();
        });

        await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        called.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_one_enriched_error_per_failure_with_field()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<MultiFieldCmd, string>, MultiFieldHandler>();
            s.AddTransient<IValidator<MultiFieldCmd>, MultiFieldValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new MultiFieldCmd("", ""));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["Email", "Name"]);
        result.Errors.Should().OnlyContain(e => e.Code == "general.value.is-required");
    }

    [Fact]
    public async Task Raw_rule_failure_maps_to_validation_rule_failed_with_rule_detail()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient<IValidator<CreateUserCmd>, AsyncRuleValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("validation.rule.failed");
        result.FirstError.Message.Should().Be("Email failed the async check.");
        result.FirstError.Field.Should().Be("Email");
        result.FirstError.Inner.Should().BeOfType<ValidationRuleFailed>().Which.Rule.Should().Be("users.email.async-check-failed");
        ErrorDetails.Of(result.FirstError)["rule"].Should().Be("users.email.async-check-failed");
    }

    [Fact]
    public async Task Validators_run_sequentially()
    {
        var journal = new Journal();
        var dispatcher = Build(s =>
        {
            s.AddSingleton(journal);
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient<IValidator<CreateUserCmd>, SlowValidatorA>();
            s.AddTransient<IValidator<CreateUserCmd>, SlowValidatorB>();
        });

        await dispatcher.SendAsync<string>(new CreateUserCmd("a@b.com"));

        journal.Entries.Should().Equal("A:start", "A:end", "B:start", "B:end");
    }
}
