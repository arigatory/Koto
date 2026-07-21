using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koto.Testing.Integration;

/// <summary>
/// Тестовая схема аутентификации: клеймы приходят в заголовках
/// <c>X-Test-UserId</c> / <c>X-Test-Role</c> — реальный IdP в интеграционных
/// тестах не поднимается. Регистрация: <see cref="HeaderTestAuthExtensions.AddHeaderTestAuthentication"/>.
/// </summary>
public sealed class HeaderTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Имя схемы аутентификации.</summary>
    public const string SchemeName = "Test";

    /// <summary>Заголовок с id пользователя (Guid).</summary>
    public const string UserIdHeader = "X-Test-UserId";

    /// <summary>Заголовок с ролью (опционально).</summary>
    public const string RoleHeader = "X-Test-Role";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        if (Request.Headers.TryGetValue(RoleHeader, out var role))
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }
}

/// <summary>Регистрация тестовой схемы и хелперы клиента.</summary>
public static class HeaderTestAuthExtensions
{
    /// <summary>
    /// Подменяет аутентификацию хоста тестовой схемой (вызывать в
    /// <c>WithWebHostBuilder(b =&gt; b.ConfigureServices(...))</c>): схема регистрируется
    /// и назначается дефолтной поверх настроек продакшен-кода.
    /// </summary>
    public static IServiceCollection AddHeaderTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(HeaderTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderTestAuthHandler>(
                HeaderTestAuthHandler.SchemeName, _ => { });
        services.PostConfigureAll<AuthenticationOptions>(o =>
        {
            o.DefaultAuthenticateScheme = HeaderTestAuthHandler.SchemeName;
            o.DefaultChallengeScheme = HeaderTestAuthHandler.SchemeName;
        });
        return services;
    }

    /// <summary>Проставляет клиенту тестового пользователя (и опционально роль).</summary>
    public static HttpClient WithTestUser(this HttpClient client, Guid userId, string? role = null)
    {
        client.DefaultRequestHeaders.Remove(HeaderTestAuthHandler.UserIdHeader);
        client.DefaultRequestHeaders.Add(HeaderTestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Remove(HeaderTestAuthHandler.RoleHeader);
        if (role is not null)
            client.DefaultRequestHeaders.Add(HeaderTestAuthHandler.RoleHeader, role);
        return client;
    }
}
