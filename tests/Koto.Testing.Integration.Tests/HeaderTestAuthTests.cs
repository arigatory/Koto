using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using Koto.Testing.Integration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Koto.Testing.Integration.Tests;

public sealed class HeaderTestAuthTests : IAsyncLifetime
{
    private IHost _host = null!;

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddHeaderTestAuthentication();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/whoami", (ClaimsPrincipal user) =>
                            Results.Ok(new
                            {
                                Id = user.FindFirstValue(ClaimTypes.NameIdentifier),
                                Role = user.FindFirstValue(ClaimTypes.Role),
                            })).RequireAuthorization();
                        endpoints.MapGet("/admin", () => Results.Ok("ok"))
                            .RequireAuthorization(p => p.RequireRole("Admin"));
                    });
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
    public async Task Header_user_is_authenticated_with_claims()
    {
        var userId = Guid.NewGuid();
        _client.WithTestUser(userId, "Admin");

        var response = await _client.GetAsync("/whoami");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(userId.ToString()).And.Contain("Admin");
    }

    [Fact]
    public async Task Missing_header_is_challenged()
    {
        var response = await _client.GetAsync("/whoami");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Role_requirement_enforced()
    {
        _client.WithTestUser(Guid.NewGuid());
        (await _client.GetAsync("/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.WithTestUser(Guid.NewGuid(), "Admin");
        (await _client.GetAsync("/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithTestUser_replaces_previous_user_and_role()
    {
        _client.WithTestUser(Guid.NewGuid(), "Admin");
        var second = Guid.NewGuid();
        _client.WithTestUser(second); // роль должна сброситься

        var response = await _client.GetAsync("/whoami");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(second.ToString()).And.NotContain("Admin");
    }
}
