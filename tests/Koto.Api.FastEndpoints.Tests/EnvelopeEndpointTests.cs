using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FastEndpoints;
using FluentValidation;
using Koto.Api.FastEndpoints.Endpoints;
using Koto.Application;
using App = Koto.Application;
using Koto.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Koto.Api.FastEndpoints.Tests;

// Endpoints are public top-level types so FastEndpoints discovery finds them.
public sealed record PingCommand(string Name) : App.ICommand<string>;

public sealed class PingValidator : Validator<PingCommand>
{
    public PingValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class PingEndpoint : CommandEndpoint<PingCommand, string>
{
    public override void Configure()
    {
        Post("/ping");
        AllowAnonymous();
    }
}

public sealed record DropCommand(Guid Id) : App.ICommand;

public sealed class DropEndpoint : CommandEndpoint<DropCommand>
{
    public override void Configure()
    {
        Post("/drop");
        AllowAnonymous();
    }
}

public sealed record BoomQuery : IQuery<int>;

public sealed class BoomEndpoint : QueryEndpoint<BoomQuery, int>
{
    public override void Configure()
    {
        Get("/boom");
        AllowAnonymous();
    }
}

public class EnvelopeEndpointTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<IHost> StartAsync(ICqrsDispatcher dispatcher)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedTimeProvider.Default));
                    services.AddRouting();
                    services.AddKotoApi();
                    services.AddFastEndpoints(o => o.Assemblies = [typeof(EnvelopeEndpointTests).Assembly]);
                    services.AddScoped(_ => dispatcher);
                })
                .Configure(app =>
                {
                    app.UseKotoApi();
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKotoFastEndpoints());
                }))
            .StartAsync();
        return host;
    }

    [Fact]
    public async Task Command_success_returns_200_envelope()
    {
        var dispatcher = Substitute.For<ICqrsDispatcher>();
        dispatcher.SendAsync<string>(Arg.Any<App.ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(AppResult<string>.Success("pong"));
        using var host = await StartAsync(dispatcher);
        var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/ping", new { name = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<string>>(Web);
        envelope!.Success.Should().BeTrue();
        envelope.Result.Should().Be("pong");
        envelope.Errors.Should().BeEmpty();
        envelope.CorrelationId.Should().NotBeNullOrEmpty();
        envelope.Timestamp.Should().Be(FixedTimeProvider.Default);
    }

    [Fact]
    public async Task Command_failure_returns_classified_status_and_envelope()
    {
        var dispatcher = Substitute.For<ICqrsDispatcher>();
        dispatcher.SendAsync<string>(Arg.Any<App.ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(AppResult<string>.Failure(new TestError("things.thing.not-found", "nope").Enrich()));
        using var host = await StartAsync(dispatcher);
        var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/ping", new { name = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<string>>(Web);
        envelope!.Success.Should().BeFalse();
        envelope.Result.Should().BeNull();
        envelope.Errors.Should().ContainSingle().Which.Code.Should().Be("things.thing.not-found");
    }

    [Fact]
    public async Task Void_command_success_returns_200_envelope_with_null_result()
    {
        var dispatcher = Substitute.For<ICqrsDispatcher>();
        dispatcher.SendAsync(Arg.Any<App.ICommand>(), Arg.Any<CancellationToken>()).Returns(AppResult.Success());
        using var host = await StartAsync(dispatcher);
        var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/drop", new { id = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true").And.Contain("\"result\":null");
    }

    [Fact]
    public async Task Binding_validation_failure_uses_envelope()
    {
        var dispatcher = Substitute.For<ICqrsDispatcher>();
        using var host = await StartAsync(dispatcher);
        var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/ping", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<object?>>(Web);
        envelope!.Success.Should().BeFalse();
        var error = envelope.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("validation.rule.failed");
        error.Field.Should().Be("name");   // FastEndpoints camelCases property names; the envelope does too
        await dispatcher.DidNotReceive().SendAsync<string>(Arg.Any<App.ICommand<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_envelope()
    {
        var dispatcher = Substitute.For<ICqrsDispatcher>();
        dispatcher.QueryAsync<int>(Arg.Any<IQuery<int>>(), Arg.Any<CancellationToken>())
            .Returns<AppResult<int>>(_ => throw new InvalidOperationException("boom"));
        using var host = await StartAsync(dispatcher);
        var client = host.GetTestClient();

        var response = await client.GetAsync("/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<object?>>(Web);
        envelope!.Success.Should().BeFalse();
        envelope.Errors.Should().ContainSingle().Which.Code.Should().Be("general.server.internal-error");
        envelope.CorrelationId.Should().NotBeNullOrEmpty();
    }
}
