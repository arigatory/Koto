# Koto.Testing.Integration

Building blocks for integration tests of Koto microservices (WebApplicationFactory + Testcontainers style). Extracted from real service suites by the rule of three: test auth, async-effect assertions, and a convention-driven Kafka test producer kept getting copy-pasted between test projects.

## Header test authentication

Replaces the host's real authentication (JWKS, JWT) with a scheme driven by request headers `X-Test-UserId` / `X-Test-Role`:

```csharp
Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
{
    builder.ConfigureServices(services => services.AddHeaderTestAuthentication());
});

using var client = Factory.CreateClient().WithTestUser(userId);          // participant
using var admin  = Factory.CreateClient().WithTestUser(adminId, "Admin"); // role check
```

## Eventually

Polling assertion for asynchronous effects (Kafka consumers, projections, sagas). The description lands in the timeout message:

```csharp
await Eventually.AssertAsync(async () =>
{
    var detail = await client.GetFromJsonAsync<JsonElement>($"/meetups/{id}");
    return detail.GetProperty("status").GetString() == "Settled";
}, TimeSpan.FromSeconds(45), "meetup becomes Settled");
```

## RawJsonKafkaProducer

Publishes plain web-cased JSON — exactly what `PublishIntegrationEvents` (Koto.Messaging.Wolverine) produces — so a test can simulate an upstream service or a redelivery. The topic comes from the contract's `public const string Topic` (shared convention via `IntegrationEventTopics` in Koto.Application):

```csharp
using var kafka = new RawJsonKafkaProducer(kafkaContainer.GetBootstrapAddress());
await kafka.PublishAsync(new UserRegisteredV1(userId, null, "Ivan", ["ru"]));   // topic resolved from the type
await kafka.PublishAsync("explicit.topic", payload);                            // or explicit
```

No Testcontainers dependency: container lifecycle stays in your fixture — these pieces compose with any host/container layout (single service, multi-service saga, in-memory TestServer).
