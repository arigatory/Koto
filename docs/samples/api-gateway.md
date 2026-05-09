# Sample: ApiGateway

**Паттерны:** API Gateway, JWT auth + authz, API Composition, rate limiting, BFF

---

## Что демонстрирует

Единая точка входа для нескольких микросервисов. Аутентификация, авторизация, агрегация ответов (API Composition), rate limiting.

## Архитектура

```
Client (Next.js / mobile)
  │
  ▼
ApiGateway (YARP + FastEndpoints)
  ├── /api/orders/**     → OrderService (proxy)
  ├── /api/payments/**   → PaymentService (proxy)
  ├── /api/dashboard     → API Composition endpoint (агрегирует OrderService + InventoryService)
  └── /api/ws/**         → WebSocket proxy → RealTimeService
```

## Что показывает

**YARP reverse proxy:**
```csharp
// Простой proxy: пробрасывает запрос без изменений
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
```

**JWT аутентификация + авторизация:**
- Проверка JWT на уровне gateway — downstream сервисы доверяют gateway
- Claims-based авторизация: `[Authorize(Policy = "AdminOnly")]`
- Передача `X-User-Id`, `X-User-Roles` в downstream заголовках

**API Composition (паттерн):**
```csharp
// Gateway агрегирует данные из нескольких сервисов в один ответ
public class DashboardEndpoint : QueryEndpoint<GetDashboardQuery, DashboardDto>
{
    // Параллельно вызывает OrderService + InventoryService + PaymentService
    // Собирает в единый DashboardDto
    // Partial response при недоступности одного из сервисов
}
```

**Rate limiting:**
- Per-user rate limiting через `Microsoft.AspNetCore.RateLimiting`
- Sliding window: 100 req/min для обычных пользователей, 1000 для premium

## Koto использует

- `Koto.Infrastructure.Http` — `ServiceHttpClient` для вызовов в downstream сервисы
- `Koto.Api.FastEndpoints` — composition endpoints
- `Koto.Observability` — distributed tracing через все сервисы (CorrelationId)

## Структура

```
samples/ApiGateway/
  src/
    Gateway.Api/
      Program.cs
      appsettings.json           ← YARP routes config
      Endpoints/
        DashboardEndpoint.cs     ← API Composition example
      Middleware/
        JwtValidationMiddleware.cs
        RateLimitingMiddleware.cs
    Gateway.Tests/
  infra/
    docker-compose.yml           ← Gateway + все downstream сервисы
    k8s/
      gateway-deployment.yaml
      ingress.yaml               ← Nginx Ingress → Gateway → сервисы
```
