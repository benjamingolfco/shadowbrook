# Telnyx Auth via `DelegatingHandler`

## Motivation

Today, `TelnyxSmsSender.Send` constructs the `Authorization: Bearer {ApiKey}` header
on every outgoing request by reading `IOptions<TelnyxOptions>.Value.ApiKey`. This
mixes two concerns inside the sender:

1. **Transport auth** — a cross-cutting concern that every request to the Telnyx
   API needs, regardless of which endpoint is called.
2. **Business payload** — formatting the `from`/`to`/`text` JSON body and
   handling the response.

As we add more Telnyx endpoints (delivery receipts, number provisioning, number
lookups, etc.), repeating the bearer-token wiring in each call site is error
prone — a single missing header gives a surprising 401 in production. It also
couples every Telnyx client to the secret.

The idiomatic ASP.NET Core approach is to attach a `DelegatingHandler` to the
typed `HttpClient` pipeline. The handler sets the Authorization header once for
every outgoing request; call sites no longer know the API key exists.

This aligns with the `IHttpClientFactory` pattern documented by Microsoft
(`AddHttpMessageHandler`) and keeps `TelnyxSmsSender` focused on the message
contract.

Related context: this branch is also moving `Infrastructure/Services/Sms` to
`Infrastructure/Services/Notifications/Sms`. New files land in the new
location.

## Design

### Component diagram

```
TelnyxSmsSender
   │  (calls httpClient.SendAsync)
   ▼
HttpClient (typed, registered via AddHttpClient<TelnyxSmsSender>)
   │
   ▼
TelnyxAuthHandler : DelegatingHandler     ← NEW
   │  sets Authorization: Bearer {ApiKey}
   ▼
Primary HttpMessageHandler (SocketsHttpHandler)
   │
   ▼
https://api.telnyx.com
```

### Why a `DelegatingHandler` (not a request-configuring lambda)

`AddHttpClient` exposes two extension points that could attach a bearer token:

- **`ConfigureHttpClient(client => client.DefaultRequestHeaders.Authorization = ...)`** —
  sets headers on the `HttpClient` itself. This works, but the typed client is
  created once per scope and the headers are captured at construction time.
  Rotating the secret (e.g., when Key Vault reload fires) would require
  rebuilding the client. Also, it does not compose with other handlers if we
  later add retries or logging.
- **`AddHttpMessageHandler<TelnyxAuthHandler>()`** — runs *per request*, reads
  `IOptions<TelnyxOptions>` each call, and composes with Polly/retry/logging
  handlers if we add them later. This is the idiomatic choice.

We pick the delegating handler.

### Lifetime

Delegating handlers registered via `AddHttpMessageHandler<T>()` **must be
transient** — `IHttpClientFactory` manages their lifecycle inside handler
rotation and will throw if the handler is registered as scoped or singleton.
Use `services.AddTransient<TelnyxAuthHandler>()`.

(`IOptions<T>` is singleton in DI, so injecting it into a transient handler is
safe — no captive-dependency concerns.)

### What `TelnyxSmsSender` still needs

`TelnyxSmsSender` still needs `IOptions<TelnyxOptions>` to read `FromNumber`
(the Telnyx sending number is part of the request body, not a header), so the
constructor signature does not change in shape — only the `ApiKey` reference is
removed.

## Files

### Create

- `src/backend/Teeforce.Api/Infrastructure/Services/Notifications/Sms/Http/TelnyxAuthHandler.cs`
  — `DelegatingHandler` subclass that sets the bearer token from
  `IOptions<TelnyxOptions>` on each outgoing request.
- `tests/Teeforce.Api.Tests/Services/Notifications/Sms/Http/TelnyxAuthHandlerTests.cs`
  — unit test (see Test Strategy below).

A subfolder `Http/` is appropriate here: `DelegatingHandler` is not an SMS
sender — it is transport plumbing that happens to serve the SMS feature. Keeping
it under `Sms/Http/` signals it is implementation detail of the Telnyx
client pipeline, not a peer of `TelnyxSmsSender`.

### Modify

- `src/backend/Teeforce.Api/Infrastructure/Services/Notifications/Sms/TelnyxSmsSender.cs`
  — remove the `request.Headers.Authorization = ...` line. Keep the
  `IOptions<TelnyxOptions>` constructor dependency (still used for
  `FromNumber`).
- `tests/Teeforce.Api.Tests/Services/Notifications/Sms/TelnyxSmsSenderTests.cs`
  — remove assertions about the Authorization header. The sender is no longer
  responsible for it, so asserting it here would be testing the wrong unit.
  Post a justification on the PR per the "test integrity" rule: "behavior
  moved — auth is now the delegating handler's responsibility, covered by
  `TelnyxAuthHandlerTests`."
- `src/backend/Teeforce.Api/Program.cs` — register `TelnyxAuthHandler` as
  transient and chain `.AddHttpMessageHandler<TelnyxAuthHandler>()` onto the
  existing `AddHttpClient<TelnyxSmsSender>(...)` call (still inside the
  non-development branch).

## Code Sketches

### `TelnyxAuthHandler.cs` (pseudocode)

```csharp
namespace Teeforce.Api.Infrastructure.Services.Notifications.Sms.Http;

public class TelnyxAuthHandler(IOptions<TelnyxOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            options.Value.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
```

Notes:
- Primary constructor per backend conventions.
- No `Async` suffix on method — but `SendAsync` is an override from the BCL
  `HttpMessageHandler`, so the name is fixed by the base class. That is fine —
  our convention only governs code we author.
- `options.Value` is read each call, so config reload (if ever enabled) is
  picked up automatically.

### `TelnyxSmsSender.cs` diff (pseudocode)

```csharp
public async Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
{
    var payload = new { from = options.Value.FromNumber, to = toPhoneNumber, text = message };

    using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/messages");
    // Authorization header removed — set by TelnyxAuthHandler in the pipeline.
    request.Content = JsonContent.Create(payload);

    var response = await httpClient.SendAsync(request, ct);
    // ... unchanged response handling
}
```

### `Program.cs` (pseudocode)

```csharp
else
{
    builder.Services.Configure<TelnyxOptions>(builder.Configuration.GetSection("Telnyx"));
    builder.Services.AddTransient<TelnyxAuthHandler>();
    builder.Services
        .AddHttpClient<TelnyxSmsSender>(client => client.BaseAddress = new Uri("https://api.telnyx.com"))
        .AddHttpMessageHandler<TelnyxAuthHandler>();
    builder.Services.AddScoped<ISmsSender, TelnyxSmsSender>();
}
```

## Test Strategy

### Existing patterns in the repo

There are currently **no tests for `DelegatingHandler` instances** in this
repo — grep for `DelegatingHandler` returns nothing. The closest prior art is
`TelnyxSmsSenderTests`, which uses a `FakeHandler : HttpMessageHandler` to
stand in for the network, captures the outbound `HttpRequestMessage`, and
asserts on it. We can use that same capture-the-request technique for the
handler unit test.

### Unit test for `TelnyxAuthHandler`

Location: `tests/Teeforce.Api.Tests/Services/Notifications/Sms/Http/TelnyxAuthHandlerTests.cs`

Approach — chain the handler in front of a fake inner handler and invoke it
via `HttpMessageInvoker`:

```csharp
// Arrange
var capturedAuth = default(AuthenticationHeaderValue);
var inner = new CapturingHandler(req =>
{
    capturedAuth = req.Headers.Authorization;
    return new HttpResponseMessage(HttpStatusCode.OK);
});

var options = Options.Create(new TelnyxOptions { ApiKey = "test-api-key" });
var handler = new TelnyxAuthHandler(options) { InnerHandler = inner };
var invoker = new HttpMessageInvoker(handler);

// Act
await invoker.SendAsync(
    new HttpRequestMessage(HttpMethod.Post, "https://api.telnyx.com/v2/messages"),
    CancellationToken.None);

// Assert
Assert.NotNull(capturedAuth);
Assert.Equal("Bearer", capturedAuth!.Scheme);
Assert.Equal("test-api-key", capturedAuth.Parameter);
```

Scenarios to cover:

1. **Sets header when none present** — baseline case above.
2. **Overwrites header when request already has one** — documents that the
   handler is the single source of truth for auth; a caller that set
   Authorization by mistake is silently corrected. (If we prefer to preserve a
   caller-set header, make the opposite assertion and justify it — but the
   whole point of centralizing auth is that callers cannot forget or override
   it, so overwrite is the right default.)
3. **Reads `IOptions.Value` per call** — not strictly a behavior a unit test
   needs to assert, but implicitly covered by constructing `options` from a
   simple `Options.Create(...)` which always returns the current instance.

A `CapturingHandler : HttpMessageHandler` helper mirroring the existing
`FakeHandler` in `TelnyxSmsSenderTests` keeps the style consistent.

### `TelnyxSmsSenderTests` changes

- Drop the `Assert.Equal("Bearer test-api-key", capturedAuth);` line from
  `Send_PostsCorrectPayloadToTelnyxApi`. The test still asserts the payload
  shape, which is the sender's remaining responsibility.
- Optionally drop the `capturedAuth` capture entirely since the variable is
  no longer used.
- The "non-success throws" test is unaffected.

### Integration tests

No new integration test is needed. The handler is a pure in-process piece of
the HttpClient pipeline; there is nothing DB- or HTTP-server-dependent to
exercise. Following the testing pyramid convention, unit tests at the handler
level are the cheapest and most accurate layer.

## Risks and Edge Cases

- **Handler lifetime footgun.** If anyone later changes
  `AddTransient<TelnyxAuthHandler>()` to scoped/singleton, `AddHttpClient`
  will throw at startup with an opaque message. Leave a short comment on the
  registration line (`// Must be transient — HttpClient factory requirement`)
  to save the next person a debugging session.
- **Secret rotation.** `IOptions<T>` is a snapshot captured at DI build. If
  the project later adopts Key Vault reload, switch to
  `IOptionsMonitor<TelnyxOptions>` inside the handler — but do NOT do that
  now, because the broader codebase uses `IOptions<T>` uniformly and the
  reload story is not in scope for this change.
- **Test environment parity.** The sender's HttpClient in tests constructs the
  real `HttpClient(handler)` directly without going through
  `IHttpClientFactory`, so the delegating handler is **not** in the pipeline
  during `TelnyxSmsSenderTests`. That is fine — those tests should no longer
  assert on auth at all, and the handler has its own tests. Just flag this in
  the PR description so reviewers do not expect the sender tests to cover the
  header.
- **No changes to the dev branch.** `DatabaseSmsSender` is used in Development
  and never touches Telnyx; the `TelnyxAuthHandler` registration lives inside
  the same `else` block, so dev startup is unaffected.

## Open Questions

1. **Should the handler throw if `ApiKey` is empty?** Current behavior: a
   missing API key results in `Bearer ` being sent and a 401 from Telnyx.
   Adding an early `DomainException`-style throw in the handler is tempting
   but feels out of place — this is infrastructure, not domain. A better home
   for that check is a startup validator on `TelnyxOptions`
   (`Services.AddOptions<TelnyxOptions>().Validate(...).ValidateOnStart()`),
   which is a separate piece of work. Leaving out of scope unless reviewer
   pushback.
2. **Folder name: `Http/` vs flat?** Proposed `Sms/Http/TelnyxAuthHandler.cs`
   because more Telnyx-transport plumbing is likely (retry handler, logging
   handler, webhook signature verifier). If reviewers prefer flat, drop the
   subfolder — the plan does not depend on it.

## Dev Tasks

### Backend Developer

- [ ] Create `Infrastructure/Services/Notifications/Sms/Http/TelnyxAuthHandler.cs`
      that sets `Authorization: Bearer {ApiKey}` on every outgoing request.
- [ ] Remove the `Authorization` header assignment from
      `TelnyxSmsSender.Send`. Keep the `IOptions<TelnyxOptions>` dependency
      for `FromNumber`.
- [ ] In `Program.cs`, register `TelnyxAuthHandler` as transient and chain
      `.AddHttpMessageHandler<TelnyxAuthHandler>()` onto the existing
      `AddHttpClient<TelnyxSmsSender>(...)`.
- [ ] Add `TelnyxAuthHandlerTests` with the two scenarios above
      (sets header, overwrites existing header).
- [ ] Update `TelnyxSmsSenderTests` to drop the Authorization assertion and
      post a short PR comment justifying the removal per the test-integrity
      rule.
- [ ] `dotnet build teeforce.slnx` and `dotnet format teeforce.slnx` clean.
- [ ] Run the affected unit test classes locally; both pass.
- [ ] `make dev` starts cleanly (catches DI misconfiguration that unit tests
      would miss).
