# Koto.Api.FastEndpoints

FastEndpoints integration for Koto DDD building blocks.

## What's included

| Type | Purpose |
|---|---|
| `CommandEndpoint<TCommand>` | Dispatches void command → 204 on success, Problem Details on failure |
| `CommandEndpoint<TCommand, TResult>` | Dispatches result command → 200 on success |
| `QueryEndpoint<TQuery, TResult>` | Dispatches query → 200 / 404 / 400 |
| `KotoProblemDetails` | RFC 7807 factory from `Error` with `errorCode` + `correlationId` extensions |
| `CorrelationIdMiddleware` | Reads/generates `X-Correlation-ID`, echoes in response |
| `ICorrelationIdAccessor` | Access current correlation ID from anywhere in the request scope |
| `GlobalExceptionHandler` | Catches unhandled exceptions → 500 Problem Details |

## Setup

```csharp
// Program.cs
builder.Services.AddKotoApi();
builder.Services.AddFastEndpoints();
// ... other services

var app = builder.Build();
app.UseKotoApi(); // CorrelationId middleware + exception handler
app.UseFastEndpoints();
app.Run();
```

## Implementing an endpoint

```csharp
// Void command (204 on success)
public class DeleteOrderEndpoint : CommandEndpoint<DeleteOrderCommand>
{
    public override void Configure()
    {
        Delete("/orders/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeleteOrderCommand req, CancellationToken ct)
        => await SendCommandAsync(req, ct);
}

// Command with result (200 on success)
public class PlaceOrderEndpoint : CommandEndpoint<PlaceOrderCommand, OrderId>
{
    public override void Configure()
    {
        Post("/orders");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlaceOrderCommand req, CancellationToken ct)
        => await SendCommandAsync(req, ct);
}

// Query (GET, 200/404)
public class GetOrderEndpoint : QueryEndpoint<GetOrderQuery, OrderDto>
{
    public override void Configure()
    {
        Get("/orders/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetOrderQuery req, CancellationToken ct)
        => await SendQueryAsync(req, ct);
}
```

## Error code → HTTP status mapping

| Error code pattern | Status |
|---|---|
| `*.not-found` | 404 Not Found |
| `*.already-*` | 409 Conflict |
| `general.value.*` | 400 Bad Request |
| anything else | 500 Internal Server Error |
