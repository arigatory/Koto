# Koto.Api.FastEndpoints

FastEndpoints integration for Koto DDD building blocks.

## What's included

| Type | Purpose |
|---|---|
| `CommandEndpoint<TCommand>` | Dispatches void command → 204 on success, Problem Details on failure |
| `CommandEndpoint<TCommand, TResult>` | Dispatches result command → 200 on success |
| `QueryEndpoint<TQuery, TResult>` | Dispatches query → 200 / 404 / 400 |
| `MappedCommandEndpoint<TRequest, TCommand[, TResult]>` | Maps an HTTP request DTO to a command via `ToCommand` — keeps server-derived fields out of the wire contract |
| `MappedQueryEndpoint<TRequest, TQuery, TResult>` | Maps an HTTP request DTO to a query via `ToQuery` |
| `ClaimsPrincipalExtensions.GetUserId()` | Reads the user id from the `NameIdentifier` claim (`TryGetUserId` for the no-throw path) |
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

## Server-derived fields (claims, route, tenant)

When a command/query carries fields that must come from the server (the caller's user id, tenant,
correlation id) and **must not** be bindable from the request body, use the mapped endpoints. The
request DTO omits those fields; `ToCommand`/`ToQuery` builds the command from the request **and** the
endpoint context (`User`, `Route<T>()`, headers):

```csharp
public sealed record SubmitJudgmentRequest(Guid SubmissionId, int GoeScore); // no JudgeId on the wire
public sealed record SubmitJudgmentCommand(Guid JudgeId, Guid SubmissionId, int GoeScore) : ICommand<Judgment>;

public sealed class SubmitJudgmentEndpoint
    : MappedCommandEndpoint<SubmitJudgmentRequest, SubmitJudgmentCommand, Judgment>
{
    public override void Configure() { Post("/api/v1/judgments"); Policies("IsJudge"); }

    protected override SubmitJudgmentCommand ToCommand(SubmitJudgmentRequest r) =>
        new(JudgeId: User.GetUserId(), r.SubmissionId, r.GoeScore); // JudgeId from claims, not the body
}
```

Prefer the plain `CommandEndpoint`/`QueryEndpoint` when the request **is** the command (no server-derived
fields). `HandleAsync` is sealed on the mapped variants — you only implement `Configure` and `ToCommand`/`ToQuery`.

## Error code → HTTP status mapping

| Error code pattern | Status |
|---|---|
| `*.not-found` | 404 Not Found |
| `*.already-*` | 409 Conflict |
| `general.value.*` | 400 Bad Request |
| anything else | 500 Internal Server Error |
