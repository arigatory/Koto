using System.Net;
using AwesomeAssertions;
using Koto.Api.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Koto.Api.AspNetCore.Tests;

public sealed class ServiceKeyAuthenticationTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(ServiceKeyAuthenticationHandler.SchemeName)
                        .AddServiceKey("super-secret");
                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet("/internal/ping", () => "pong")
                            .RequireAuthorization(p => p.RequireRole("Service")));
                }))
            .StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task Valid_key_is_authenticated_with_service_role()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/ping");
        request.Headers.Add("X-Service-Key", "super-secret");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Missing_key_is_unauthorized()
    {
        var response = await _client.GetAsync("/internal/ping");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_key_is_unauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/ping");
        request.Headers.Add("X-Service-Key", "wrong");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
