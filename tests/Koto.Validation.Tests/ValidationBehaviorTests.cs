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

    private sealed record CreateUserCmd(string Email) : ICommand<string>;

    private sealed class CreateUserHandler : ICommandHandler<CreateUserCmd, string>
    {
        public Task<Result<string>> HandleAsync(CreateUserCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success($"created:{cmd.Email}"));
    }

    // Regression for the marker-type resolution defect: a validator keyed by the
    // CONCRETE command type must be discovered by the pipeline.
    private sealed class CreateUserValidator : AbstractValidator<CreateUserCmd>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.Email).NotEmptyWithKotoError();
        }
    }

    private sealed record DeleteUserCmd(string Email) : ICommand;

    private sealed class DeleteUserHandler : ICommandHandler<DeleteUserCmd>
    {
        public Task<Result<Unit>> HandleAsync(DeleteUserCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class DeleteUserValidator : AbstractValidator<DeleteUserCmd>
    {
        public DeleteUserValidator()
        {
            RuleFor(x => x.Email).NotEmptyWithKotoError();
        }
    }

    private sealed record GetUserQuery(string Email) : IQuery<string>;

    private sealed class GetUserHandler : IQueryHandler<GetUserQuery, string>
    {
        public Task<Result<string>> HandleAsync(GetUserQuery query, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success($"user:{query.Email}"));
    }

    private sealed class GetUserValidator : AbstractValidator<GetUserQuery>
    {
        public GetUserValidator()
        {
            RuleFor(x => x.Email).NotEmptyWithKotoError();
        }
    }

    private sealed record MultiFieldCmd(string Email, string Name) : ICommand<string>;

    private sealed class MultiFieldHandler : ICommandHandler<MultiFieldCmd, string>
    {
        public Task<Result<string>> HandleAsync(MultiFieldCmd cmd, CancellationToken ct = default) =>
            Task.FromResult(Result<string>.Success("ok"));
    }

    private sealed class MultiFieldValidator : AbstractValidator<MultiFieldCmd>
    {
        public MultiFieldValidator()
        {
            RuleFor(x => x.Email).NotEmptyWithKotoError();
            RuleFor(x => x.Name).NotEmptyWithKotoError();
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
                .WithErrorCode("users.email.async-check-failed");
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ICqrsDispatcher Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();
        configure(services);
        return services.BuildServiceProvider().GetRequiredService<ICqrsDispatcher>();
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidationBehavior_passes_through_when_no_validators()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd("a@b.com"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_keyed_by_concrete_command_type_is_discovered()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<CreateUserCmd>, CreateUserValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(Errors.General.ValueIsRequired().Code);
        result.Error.Field.Should().Be("Email");
    }

    [Fact]
    public async Task Validator_for_void_command_is_discovered()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<DeleteUserCmd>, DeleteUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<DeleteUserCmd>, DeleteUserValidator>();
        });

        var result = await dispatcher.SendAsync(new DeleteUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(Errors.General.ValueIsRequired().Code);
    }

    [Fact]
    public async Task Validator_for_query_is_discovered()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<IQueryHandler<GetUserQuery, string>, GetUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<GetUserQuery>, GetUserValidator>();
        });

        var result = await dispatcher.QueryAsync<string>(new GetUserQuery(""));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(Errors.General.ValueIsRequired().Code);
    }

    [Fact]
    public async Task ValidationBehavior_does_not_call_handler_when_invalid()
    {
        var called = false;
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>>(_ =>
                new TrackingHandler(() => called = true));
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<CreateUserCmd>, CreateUserValidator>();
        });

        await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        called.Should().BeFalse();
    }

    [Fact]
    public async Task All_failures_are_preserved_as_structured_errors()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<MultiFieldCmd, string>, MultiFieldHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<MultiFieldCmd>, MultiFieldValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new MultiFieldCmd("", ""));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Select(e => e.Field).Should().BeEquivalentTo(["Email", "Name"]);
        result.Errors.Should().OnlyContain(e => e.Code == Errors.General.ValueIsRequired().Code);
    }

    [Fact]
    public async Task Async_validation_rules_are_supported()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<CreateUserCmd>, AsyncRuleValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("users.email.async-check-failed");
    }

    private sealed class TrackingHandler(Action onHandle) : ICommandHandler<CreateUserCmd, string>
    {
        public Task<Result<string>> HandleAsync(CreateUserCmd cmd, CancellationToken ct = default)
        {
            onHandle();
            return Task.FromResult(Result<string>.Success("ok"));
        }
    }
}
