using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koto.Api.AspNetCore;

/// <summary>Опции схемы <c>ServiceKey</c> (s2s-аутентификация по симметричному ключу).</summary>
public sealed class ServiceKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Имя заголовка с ключом. По умолчанию <c>X-Service-Key</c>.</summary>
    public string HeaderName { get; set; } = "X-Service-Key";

    /// <summary>Ожидаемый ключ (симметричный секрет окружения).</summary>
    public string ApiKey { get; set; } = "";
}

/// <summary>
/// Схема аутентификации <c>ServiceKey</c>: внутренние (s2s) эндпоинты защищаются
/// симметричным ключом из заголовка. Клиентскую сторону настраивает
/// <c>AddServiceHttpClient(..., o => o.ApiKey = ...)</c> из Koto.Infrastructure.Http.
/// Principal получает клейм роли <c>Service</c> — используйте <c>Roles("Service")</c>.
/// </summary>
public sealed class ServiceKeyAuthenticationHandler(
    IOptionsMonitor<ServiceKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ServiceKeyAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>Имя схемы.</summary>
    public const string SchemeName = "ServiceKey";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrEmpty(Options.ApiKey))
            return Task.FromResult(AuthenticateResult.Fail("ServiceKey is not configured"));

        if (!Request.Headers.TryGetValue(Options.HeaderName, out var provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expected = Encoding.UTF8.GetBytes(Options.ApiKey);
        var actual = Encoding.UTF8.GetBytes(provided.ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return Task.FromResult(AuthenticateResult.Fail("Invalid service key"));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "service"), new Claim(ClaimTypes.Role, "Service")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Регистрация схемы <c>ServiceKey</c>.</summary>
public static class ServiceKeyAuthenticationExtensions
{
    /// <summary>Добавляет схему <c>ServiceKey</c> к существующей аутентификации.</summary>
    public static AuthenticationBuilder AddServiceKey(
        this AuthenticationBuilder builder,
        string apiKey,
        string headerName = "X-Service-Key") =>
        builder.AddScheme<ServiceKeyAuthenticationOptions, ServiceKeyAuthenticationHandler>(
            ServiceKeyAuthenticationHandler.SchemeName,
            o =>
            {
                o.ApiKey = apiKey;
                o.HeaderName = headerName;
            });
}
