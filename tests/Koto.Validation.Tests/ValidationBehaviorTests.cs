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

    private sealed class CreateUserValidator : AbstractValidator<ICommand<string>>
    {
        public CreateUserValidator()
        {
            RuleFor(x => ((CreateUserCmd)x).Email)
                .NotEmpty()
                .WithMessage(Errors.General.ValueIsRequired().Serialize());
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
    public async Task ValidationBehavior_returns_failure_when_invalid()
    {
        var dispatcher = Build(s =>
        {
            s.AddTransient<ICommandHandler<CreateUserCmd, string>, CreateUserHandler>();
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            s.AddTransient<IValidator<ICommand<string>>, CreateUserValidator>();
        });

        var result = await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation.failed");
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
            s.AddTransient<IValidator<ICommand<string>>, CreateUserValidator>();
        });

        await dispatcher.SendAsync<string>(new CreateUserCmd(""));

        called.Should().BeFalse();
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
