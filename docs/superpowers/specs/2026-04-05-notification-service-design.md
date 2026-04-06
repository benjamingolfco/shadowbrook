# Notification Service Design

## Problem

All SMS sends today go through `ITextMessageService`, which takes a raw phone number and message string. Handlers are coupled to the SMS channel — they look up the golfer's phone number, build a message, and call `SendAsync`. This makes it hard to add email, push, or per-user notification preferences later.

## Goals

1. Introduce `INotificationService` that abstracts channel selection — handlers say "notify this user with this message" without knowing how
2. Implement Telnyx as the real SMS provider (typed `HttpClient`, no SDK dependency)
3. Preserve the dev SMS experience (`/dev/sms` endpoints, database-backed message inspection)
4. Design for future channel expansion (email, push) and per-user notification preferences without building them yet

## Non-Goals

- Golfer/AppUser identity consolidation (separate initiative — this design assumes `GolferId` equals `AppUserId`)
- Message templating or per-channel formatting
- AppUser notification preferences storage and routing (deferred — GitHub issue created)
- Email provider integration (no-op placeholder only)

## Design

### Domain Layer

New interface in `Teeforce.Domain/Common/`:

```csharp
public interface INotificationService
{
    Task Send(Guid appUserId, string message, CancellationToken ct = default);
}
```

`ITextMessageService` is removed.

### Channel Sender Interfaces

In `Infrastructure/Services/` (API layer — these are infrastructure concerns):

```csharp
public interface ISmsSender
{
    Task Send(string toPhoneNumber, string message, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task Send(string toEmail, string subject, string body, CancellationToken ct = default);
}
```

### NotificationService Implementation

`Infrastructure/Services/NotificationService.cs`:

- Accepts `ISmsSender`, `IEmailSender`, `ApplicationDbContext`, `ILogger`
- Looks up the AppUser's phone number and email from the database
- Routing logic: phone available → SMS; no phone but email available → email; neither → log warning and skip
- When falling back to email, uses a generic subject line (e.g., "Teeforce Notification") since handlers pass plain text. Per-channel formatting is a future concern.
- When user notification preferences are added later, this is where channel routing logic lives

### Telnyx SMS Sender

`Infrastructure/Services/TelnyxSmsSender.cs`:

- Typed `HttpClient` — no Telnyx SDK dependency
- Uses Telnyx Messaging API v2 (`POST /v2/messages`)
- Configuration via `TelnyxOptions` (API key, from number) bound to `Telnyx` config section
- API key stored in Azure Key Vault for production

```csharp
public class TelnyxOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string FromNumber { get; init; } = string.Empty;
}
```

### Dev SMS Sender

`Infrastructure/Services/DatabaseSmsSender.cs`:

- Replaces `DatabaseTextMessageService` as the dev implementation of `ISmsSender`
- Persists messages to `DevSmsMessages` table (same as today)
- `/dev/sms` endpoints continue to work unchanged

### No-Op Email Sender

`Infrastructure/Services/NoOpEmailSender.cs`:

- Placeholder implementation of `IEmailSender`
- Logs a warning that email is not configured
- Replaced with a real implementation when an email provider is chosen

### Registration

In `Program.cs`:

- `INotificationService` → `NotificationService` (always)
- `IEmailSender` → `NoOpEmailSender` (always, until real provider)
- `ISmsSender` → `DatabaseSmsSender` (Development) or `TelnyxSmsSender` (Production/Test)
- `TelnyxOptions` configured from `Telnyx` config section in non-dev environments
- `TelnyxSmsSender` registered as a typed `HttpClient`

### Handler Migration

All 6 SMS handlers migrate from `ITextMessageService` to `INotificationService`:

1. `BookingCreated/ConfirmationSmsHandler`
2. `BookingCancelled/SmsHandler`
3. `GolferJoinedWaitlist/SmsHandler`
4. `WaitlistOfferCreated/SendSmsHandler`
5. `WaitlistOfferRejected/SmsHandler`
6. `TeeTimeOpeningSlotsClaimed/SmsHandler`

**Before:**
```csharp
var golfer = await golferRepository.GetRequiredByIdAsync(domainEvent.GolferId);
await textMessageService.SendAsync(golfer.Phone, message, ct);
```

**After:**
```csharp
await notificationService.Send(domainEvent.GolferId, message, ct);
```

Handlers no longer look up golfer phone numbers — the notification service resolves contact info internally.

### Removals

- `ITextMessageService` (domain)
- `DatabaseTextMessageService` (replaced by `DatabaseSmsSender`)
- `InMemoryTextMessageService` (already superseded)

### Config Shape

Production/Test `appsettings.json`:

```json
{
  "Telnyx": {
    "ApiKey": "KEY...",
    "FromNumber": "+1XXXXXXXXXX"
  }
}
```

## Testing Strategy

**Unit tests:**
- `NotificationService` — verify routing logic (phone → SMS, email fallback, neither → skip), using NSubstitute for `ISmsSender` and `IEmailSender`
- `TelnyxSmsSender` — verify correct HTTP request construction using a mock `HttpMessageHandler`
- All 6 migrated handlers — verify they call `INotificationService.Send` with correct AppUserId and message

**Integration tests:**
- Existing flow-based tests continue to work with `DatabaseSmsSender` registered in the test factory

## Deferred Work

- **AppUser notification preferences** — per-user settings for preferred channel(s), quiet hours, opt-out. GitHub issue to be created.
- **Golfer/AppUser consolidation** — formalizing that GolferId = AppUserId across the domain
- **Email provider** — replace `NoOpEmailSender` with real implementation
- **Inbound SMS** — Telnyx webhook for receiving SMS (two-way conversational booking)
