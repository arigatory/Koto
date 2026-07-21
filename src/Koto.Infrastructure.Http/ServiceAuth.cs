namespace Koto.Infrastructure.Http;

/// <summary>Опции типизированного клиента межсервисных вызовов.</summary>
public sealed class ServiceHttpClientOptions
{
    /// <summary>Имя заголовка s2s-ключа. По умолчанию <c>X-Service-Key</c>.</summary>
    public string ApiKeyHeaderName { get; set; } = ServiceAuthDefaults.HeaderName;

    /// <summary>
    /// Симметричный s2s-ключ. Если задан — добавляется к каждому запросу клиента;
    /// принимающая сторона проверяет его схемой <c>ServiceKey</c>
    /// (<c>AddServiceKeyAuthentication</c> из Koto.Api.AspNetCore).
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>Общие константы s2s-аутентификации (клиент и сервер используют одни значения).</summary>
public static class ServiceAuthDefaults
{
    /// <summary>Имя заголовка по умолчанию.</summary>
    public const string HeaderName = "X-Service-Key";

    /// <summary>Имя схемы аутентификации на принимающей стороне.</summary>
    public const string Scheme = "ServiceKey";
}

/// <summary>Добавляет s2s-ключ к каждому исходящему запросу типизированного клиента.</summary>
public sealed class ServiceKeyHandler(string headerName, string apiKey) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(headerName);
        request.Headers.Add(headerName, apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
