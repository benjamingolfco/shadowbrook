# Notification Formatting + Wolverine Command Dispatch Design

## Problem

All 6 notification handlers currently build a plain string message and call `INotificationService.Send(appUserId, message)`. The notification service routes to SMS or email, but both channels receive the same string. Email gets a hardcoded subject line ("Teeforce Notification") and no formatting. As we add user notification preferences, users who prefer email will get a poor experience.

Additionally, the notification service currently sends synchronously inside the event handler. If the handler fails after sending, the event retries and the notification is sent again. There's no delivery guarantee or retry safety.

## Goals

1. Introduce `INotification` as a typed contract between notification handlers and the delivery pipeline
2. Channel-specific formatters that produce appropriate content for SMS (short text) and email (subject + body)
3. Move the actual send operation to a Wolverine command processed via the transactional outbox, giving retry safety and crash resilience
4. Keep notification handlers thin — they fetch data and publish a command, nothing more

## Non-Goals

- HTML email templates or rich formatting — plain text is fine for now
- User notification preferences / channel selection — that's #371
- Email provider integration — `NoOpEmailSender` stays

## Design

### Domain Layer

**`INotification`** — empty marker interface in `Domain/Common/`:

```csharp
public interface INotification;
```

**`INotificationService`** — updated signature:

```csharp
public interface INotificationService
{
    Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification;
}
```

### Notification Types

Pure data records, co-located with their notification handlers in the feature folders. One per existing handler:

| Type | Location | Properties |
|------|----------|------------|
| `BookingConfirmation` | `Features/Bookings/Handlers/BookingCreated/` | CourseName, Date, Time |
| `BookingCancellation` | `Features/Bookings/Handlers/BookingCancelled/` | CourseName, Date, Time |
| `WaitlistJoined` | `Features/Waitlist/Handlers/GolferJoinedWaitlist/` | CourseName |
| `WaitlistOfferAvailable` | `Features/Waitlist/Handlers/WaitlistOfferCreated/` | CourseName, Time, ClaimUrl |
| `WaitlistOfferExpired` | `Features/Waitlist/Handlers/WaitlistOfferRejected/` | (no properties — static message) |
| `WalkupConfirmation` | `Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/` | CourseName, Date, Time |

All implement `INotification`. All are records.

### Formatter Interfaces

In `Infrastructure/Services/`:

```csharp
public interface ISmsFormatter<T> where T : INotification
{
    string Format(T notification);
}

public interface IEmailFormatter<T> where T : INotification
{
    (string Subject, string Body) Format(T notification);
}
```

Each notification type gets an `ISmsFormatter<T>` implementation co-located with the notification type.

**Default email formatter:** `DefaultEmailFormatter<T>` resolves `ISmsFormatter<T>` via DI, calls `Format()`, and returns a generic subject ("Teeforce Notification") with the SMS text as the body. Logs an `Information` message when a notification type falls back to the default email formatter. Registered as an open generic so it applies to any `T` without an explicit `IEmailFormatter<T>`.

Dedicated `IEmailFormatter<T>` implementations are added per-type as needed (none required initially).

### Wolverine Command Flow

**`SendNotification<T>`** — a Wolverine message (command) carrying:

```csharp
public record SendNotification<T>(Guid AppUserId, T Notification) where T : INotification;
```

**`NotificationService.Send<T>`** — the implementation no longer does routing or sending. It publishes the command via `IMessageBus`:

```csharp
public async Task Send<T>(Guid appUserId, T notification, CancellationToken ct) where T : INotification
{
    await messageBus.PublishAsync(new SendNotification<T>(appUserId, notification));
}
```

**`SendNotificationHandler<T>`** — a Wolverine handler that processes the command:

1. Looks up the user's phone number (Golfer table) and email (AppUser table)
2. Routes: phone available → SMS, no phone but email → email, neither → log warning and skip
3. Resolves the appropriate formatter via DI (`ISmsFormatter<T>` or `IEmailFormatter<T>`)
4. Formats the notification
5. Sends via `ISmsSender` or `IEmailSender`

The command is persisted in Wolverine's transactional outbox — if the notification handler's transaction commits, the command is guaranteed to be processed. Wolverine handles retries.

### Handler Changes

Existing SMS handlers are renamed to notification handlers. They become thin:

**Before:**
```csharp
var message = $"You're booked! {courseName} at {time} on {date}. See you on the course!";
await notificationService.Send(domainEvent.GolferId, message, ct);
```

**After:**
```csharp
var notification = new BookingConfirmation(courseName, booking.TeeTime.Date, booking.TeeTime.Time);
await notificationService.Send(domainEvent.GolferId, notification, ct);
```

Handlers no longer import anything SMS/email-related. They just build the notification and hand it off.

### DI Registration

- `ISmsFormatter<T>` — one per notification type, registered individually
- `IEmailFormatter<T>` — `DefaultEmailFormatter<T>` registered as open generic; specific implementations override per-type
- `INotificationService` → `NotificationService` (now just publishes commands)
- `SendNotificationHandler<T>` — discovered by Wolverine automatically

### Removals

- The current `NotificationService` routing/sending logic moves to `SendNotificationHandler<T>`
- The hardcoded "Teeforce Notification" subject in `NotificationService` is replaced by the default email formatter

## Testing Strategy

**Unit tests:**
- Each `ISmsFormatter<T>` — verify output string for given notification data
- `DefaultEmailFormatter<T>` — verify it delegates to SMS formatter and logs information
- `SendNotificationHandler<T>` — verify routing logic (phone → SMS, email fallback, neither → skip), formatter resolution, and channel dispatch. Use NSubstitute for senders and formatters.
- Each notification handler — verify it builds the correct notification type and calls `notificationService.Send<T>` with the right data

**Integration tests:**
- Existing flow-based tests continue to work — `DatabaseSmsSender` captures messages as before

## File Summary

**New files:**
- `Domain/Common/INotification.cs`
- `Infrastructure/Services/ISmsFormatter.cs`
- `Infrastructure/Services/IEmailFormatter.cs`
- `Infrastructure/Services/DefaultEmailFormatter.cs`
- `Infrastructure/Services/SendNotification.cs` (command record)
- `Infrastructure/Services/SendNotificationHandler.cs`
- 6 notification type records (co-located with handlers)
- 6 SMS formatter implementations (co-located with handlers)

**Modified files:**
- `Domain/Common/INotificationService.cs` — updated signature
- `Infrastructure/Services/NotificationService.cs` — simplified to publish command
- 6 notification handlers — build notification objects instead of strings
- `Program.cs` — register formatters
- Existing tests — updated for new signatures
