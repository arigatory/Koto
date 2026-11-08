using AwesomeAssertions;
using FastEndpoints;
using FluentValidation.Results;
using Koto.Api.FastEndpoints.Middleware;
using Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Koto.Api.FastEndpoints.Tests;

public class KotoFastEndpointsConfigTests
{
    // FastEndpoints' ErrorOptions are write-only; the effect of ApplyKotoEnvelope is verified end-to-end
    // in EnvelopeEndpointTests.Binding_validation_failure_uses_envelope.
    [Fact]
    public void ApplyKotoEnvelope_returns_the_same_config_for_chaining()
    {
        var config = new Config();

        config.ApplyKotoEnvelope().Should().BeSameAs(config);
    }

    [Fact]
    public void BuildErrorEnvelope_maps_binding_failures_to_validation_rule_failed_with_field()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedTimeProvider.Default));
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        CorrelationContext.Current.Value = "corr-fe";
        var failures = new List<ValidationFailure>
        {
            new("Name", "'Name' must not be empty.") { ErrorCode = "NotEmptyValidator" },
            new("Email", "Required") { CustomState = new Errors.General.ValueIsRequired(), ErrorCode = "general.value.is-required" },
        };

        var envelope = KotoFastEndpointsConfig.BuildErrorEnvelope(failures, ctx, 400);

        envelope.Success.Should().BeFalse();
        envelope.Result.Should().BeNull();
        envelope.Errors.Should().HaveCount(2);
        envelope.Errors[0].Code.Should().Be("validation.rule.failed");
        envelope.Errors[0].Field.Should().Be("name");   // the envelope camelCases wire fields
        envelope.Errors[0].Details["rule"].Should().Be("NotEmptyValidator");
        envelope.Errors[1].Code.Should().Be("general.value.is-required");
        envelope.Errors[1].Field.Should().Be("email");
        envelope.CorrelationId.Should().Be("corr-fe");
        envelope.Timestamp.Should().Be(FixedTimeProvider.Default);
    }
}
