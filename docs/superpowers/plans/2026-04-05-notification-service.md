# Notification Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ITextMessageService` with a channel-agnostic `INotificationService` that resolves user contact info and routes to the appropriate channel (SMS via Telnyx, email no-op for now).

**Architecture:** Domain handlers call `INotificationService.Send(appUserId, message)`. The implementation looks up the user's phone/email from the database and routes to `ISmsSender` (phone available) or `IEmailSender` (email fallback). Telnyx provides real SMS delivery via typed `HttpClient`; dev environment uses a `DatabaseSmsSender` that persists to the existing `DevSmsMessages` table.

**Tech Stack:** .NET 10, Telnyx REST API v2 (typed HttpClient), EF Core, NSubstitute for tests

**Worktree:** `.worktrees/issue/notification-service` (branch: `issue/notification-service`)

---

### Task 1: INotificationService Domain Interface

**Files:**
- Create: `src/backend/Teeforce.Domain/Common/INotificationService.cs`

- [ ] **Step 1: Create the interface**

```csharp
namespace Teeforce.Domain.Common;

public interface INotificationService
{
    Task Send(Guid appUserId, string message, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Teeforce.Domain/Common/INotificationService.cs
git commit -m "feat: add INotificationService domain interface"
```

---

### Task 2: Channel Sender Interfaces and No-Op Email

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/ISmsSender.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/IEmailSender.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/NoOpEmailSender.cs`

- [ ] **Step 1: Create ISmsSender**

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public interface ISmsSender
{
    Task Send(string toPhoneNumber, string message, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create IEmailSender**

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public interface IEmailSender
{
    Task Send(string toEmail, string subject, string body, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create NoOpEmailSender**

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task Send(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        logger.LogWarning("Email not configured. Would have sent to {Email}: {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/ISmsSender.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/IEmailSender.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/NoOpEmailSender.cs
git commit -m "feat: add ISmsSender, IEmailSender interfaces and NoOpEmailSender"
```

---

### Task 3: NotificationService Implementation (TDD)

**Files:**
- Create: `tests/Teeforce.Api.Tests/Services/NotificationServiceTests.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/NotificationService.cs`

The NotificationService resolves user contact info from the database and routes to the appropriate channel. Since it queries `ApplicationDbContext` directly, we need to use an in-memory or mock DbContext for unit tests. However, to keep tests simple and focused on routing logic, we'll use NSubstitute for `ISmsSender` and `IEmailSender`, and use an in-memory EF context for the user lookup.

**Important context:** `AppUser` has `Email` but no `Phone`. `Golfer` has `Phone` but no `Email`. Since GolferId = AppUserId (by convention, not yet formalized), the NotificationService queries both tables to resolve contact info. This is transitional — once Golfer is consolidated into AppUser, only one query is needed.

- [ ] **Step 1: Write failing tests for routing logic**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.GolferAggregate;

namespace Teeforce.Api.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly ISmsSender smsSender = Substitute.For<ISmsSender>();
    private readonly IEmailSender emailSender = Substitute.For<IEmailSender>();
    private readonly ILogger<NotificationService> logger = Substitute.For<ILogger<NotificationService>>();
    private readonly NotificationService sut;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        this.db = new ApplicationDbContext(options);
        this.sut = new NotificationService(this.smsSender, this.emailSender, this.db, this.logger);
    }

    public void Dispose() => this.db.Dispose();

    [Fact]
    public async Task Send_GolferWithPhone_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        this.db.Golfers.Add(golfer);
        await this.db.SaveChangesAsync();

        await this.sut.Send(golfer.Id, "Test message", CancellationToken.None);

        await this.smsSender.Received(1).Send("+15551234567", "Test message", Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_NoGolferButAppUserWithEmail_SendsEmail()
    {
        // AppUser with email but no corresponding Golfer record (no phone)
        var userId = Guid.CreateVersion7();
        this.db.AppUsers.Add(new TestAppUser { Id = userId, Email = "jane@example.com" });
        await this.db.SaveChangesAsync();

        await this.sut.Send(userId, "Test message", CancellationToken.None);

        await this.emailSender.Received(1).Send("jane@example.com", "Teeforce Notification", "Test message", Arg.Any<CancellationToken>());
        await this.smsSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_NoContactInfo_LogsWarningAndSkips()
    {
        var unknownUserId = Guid.CreateVersion7();

        await this.sut.Send(unknownUserId, "Test message", CancellationToken.None);

        await this.smsSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Send_GolferWithPhoneAndAppUserWithEmail_PrefersSms()
    {
        var golfer = Golfer.Create("+15559876543", "Bob", "Green");
        this.db.Golfers.Add(golfer);
        this.db.AppUsers.Add(new TestAppUser { Id = golfer.Id, Email = "bob@example.com" });
        await this.db.SaveChangesAsync();

        await this.sut.Send(golfer.Id, "Test message", CancellationToken.None);

        await this.smsSender.Received(1).Send("+15559876543", "Test message", Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
```

**Note:** `TestAppUser` may need to be a helper that creates an AppUser via its factory method, or we may need to use the `CreateAdmin`/`CreateOperator` factory and set up the required `IAppUserEmailUniquenessChecker` stub. The implementing agent should check how AppUser is constructed in existing tests and follow that pattern. If the in-memory database doesn't work cleanly with `ApplicationDbContext` (which has query filters and Identity), adjust the approach — the important thing is testing the routing logic.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~NotificationServiceTests" --no-restore -v n`
Expected: FAIL — `NotificationService` class doesn't exist yet

- [ ] **Step 3: Implement NotificationService**

```csharp
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class NotificationService(
    ISmsSender smsSender,
    IEmailSender emailSender,
    ApplicationDbContext db,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task Send(Guid appUserId, string message, CancellationToken ct = default)
    {
        // Try SMS first — look up phone from Golfer table
        var phone = await db.Golfers
            .IgnoreQueryFilters()
            .Where(g => g.Id == appUserId)
            .Select(g => g.Phone)
            .FirstOrDefaultAsync(ct);

        if (phone is not null)
        {
            await smsSender.Send(phone, message, ct);
            return;
        }

        // Fall back to email — look up from AppUser table
        var email = await db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Id == appUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        if (email is not null)
        {
            await emailSender.Send(email, "Teeforce Notification", message, ct);
            return;
        }

        logger.LogWarning("No contact info found for user {AppUserId}, skipping notification", appUserId);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~NotificationServiceTests" --no-restore -v n`
Expected: All 4 tests PASS

- [ ] **Step 5: Commit**

```bash
git add tests/Teeforce.Api.Tests/Services/NotificationServiceTests.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/NotificationService.cs
git commit -m "feat: implement NotificationService with SMS-first routing"
```

---

### Task 4: DatabaseSmsSender (Dev Implementation)

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/DatabaseSmsSender.cs`

This replaces `DatabaseTextMessageService` as the dev implementation. It persists to the same `DevSmsMessages` table so `/dev/sms` endpoints continue working.

- [ ] **Step 1: Create DatabaseSmsSender**

```csharp
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Dev;

namespace Teeforce.Api.Infrastructure.Services;

public class DatabaseSmsSender(ApplicationDbContext db, ILogger<DatabaseSmsSender> logger) : ISmsSender
{
    public const string SystemPhoneNumber = "+10000000000";

    public async Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        var sms = new DevSmsMessage
        {
            Id = Guid.NewGuid(),
            From = SystemPhoneNumber,
            To = toPhoneNumber,
            Body = message,
            Direction = SmsDirection.Outbound,
            Timestamp = DateTimeOffset.UtcNow
        };

        db.DevSmsMessages.Add(sms);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[SMS] To: {PhoneNumber} | Message: {Message}", toPhoneNumber, message);
    }

    public async Task AddInbound(string fromPhoneNumber, string message, CancellationToken ct = default)
    {
        var sms = new DevSmsMessage
        {
            Id = Guid.NewGuid(),
            From = fromPhoneNumber,
            To = SystemPhoneNumber,
            Body = message,
            Direction = SmsDirection.Inbound,
            Timestamp = DateTimeOffset.UtcNow
        };

        db.DevSmsMessages.Add(sms);
        await db.SaveChangesAsync(ct);
    }
}
```

**Note:** `AddInbound` is not on the `ISmsSender` interface — it's a dev-only method used by the `/dev/sms/inbound` endpoint. The endpoint currently injects `DatabaseTextMessageService` directly; it will need to inject `DatabaseSmsSender` directly instead.

- [ ] **Step 2: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/DatabaseSmsSender.cs
git commit -m "feat: add DatabaseSmsSender for dev environment"
```

---

### Task 5: TelnyxSmsSender Implementation (TDD)

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Configuration/TelnyxOptions.cs`
- Create: `tests/Teeforce.Api.Tests/Services/TelnyxSmsSenderTests.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/TelnyxSmsSender.cs`

**Reference:** Telnyx Messaging API v2 — `POST https://api.telnyx.com/v2/messages` with JSON body `{ "from": "+1...", "to": "+1...", "text": "..." }` and header `Authorization: Bearer <API_KEY>`. Success returns HTTP 200 with a JSON response containing `data.id`.

- [ ] **Step 1: Create TelnyxOptions**

```csharp
namespace Teeforce.Api.Infrastructure.Configuration;

public class TelnyxOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string FromNumber { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Write failing tests**

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Tests.Services;

public class TelnyxSmsSenderTests
{
    private static TelnyxSmsSender CreateSender(
        HttpMessageHandler handler,
        string apiKey = "test-api-key",
        string fromNumber = "+10001112222")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com") };
        var options = Options.Create(new TelnyxOptions { ApiKey = apiKey, FromNumber = fromNumber });
        var logger = Substitute.For<ILogger<TelnyxSmsSender>>();
        return new TelnyxSmsSender(httpClient, options, logger);
    }

    [Fact]
    public async Task Send_PostsCorrectPayloadToTelnyxApi()
    {
        string? capturedBody = null;
        string? capturedAuth = null;
        var handler = new FakeHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.ToString();
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"id":"msg-123"}}""")
            };
        });

        var sender = CreateSender(handler);
        await sender.Send("+15559876543", "Hello from Teeforce", CancellationToken.None);

        Assert.Equal("Bearer test-api-key", capturedAuth);
        Assert.NotNull(capturedBody);
        var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("+10001112222", doc.RootElement.GetProperty("from").GetString());
        Assert.Equal("+15559876543", doc.RootElement.GetProperty("to").GetString());
        Assert.Equal("Hello from Teeforce", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"errors":[{"detail":"Invalid API key"}]}""")
        });

        var sender = CreateSender(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sender.Send("+15551234567", "Test", CancellationToken.None));
    }

    private class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~TelnyxSmsSenderTests" --no-restore -v n`
Expected: FAIL — `TelnyxSmsSender` class doesn't exist yet

- [ ] **Step 4: Implement TelnyxSmsSender**

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;

namespace Teeforce.Api.Infrastructure.Services;

public class TelnyxSmsSender(
    HttpClient httpClient,
    IOptions<TelnyxOptions> options,
    ILogger<TelnyxSmsSender> logger) : ISmsSender
{
    public async Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        var payload = new { from = options.Value.FromNumber, to = toPhoneNumber, text = message };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/messages");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Telnyx API error {StatusCode}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        logger.LogInformation("SMS sent to {PhoneNumber} via Telnyx", toPhoneNumber);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~TelnyxSmsSenderTests" --no-restore -v n`
Expected: All 2 tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Configuration/TelnyxOptions.cs \
       tests/Teeforce.Api.Tests/Services/TelnyxSmsSenderTests.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/TelnyxSmsSender.cs
git commit -m "feat: implement TelnyxSmsSender with typed HttpClient"
```

---

### Task 6: Wire Up DI Registration in Program.cs

**Files:**
- Modify: `src/backend/Teeforce.Api/Program.cs:93-95`

- [ ] **Step 1: Replace ITextMessageService registrations with new services**

Replace lines 93-95 in `Program.cs`:

```csharp
// Old:
builder.Services.AddSingleton<InMemoryTextMessageService>();
builder.Services.AddScoped<DatabaseTextMessageService>();
builder.Services.AddScoped<ITextMessageService>(sp => sp.GetRequiredService<DatabaseTextMessageService>());
```

With:

```csharp
// Notification service
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailSender, NoOpEmailSender>();

// SMS channel — environment-dependent
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<DatabaseSmsSender>();
    builder.Services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<DatabaseSmsSender>());
}
else
{
    builder.Services.Configure<TelnyxOptions>(builder.Configuration.GetSection("Telnyx"));
    builder.Services.AddHttpClient<TelnyxSmsSender>(client =>
    {
        client.BaseAddress = new Uri("https://api.telnyx.com");
    });
    builder.Services.AddScoped<ISmsSender, TelnyxSmsSender>();
}
```

**Note:** `DatabaseSmsSender` is registered both as itself (for direct injection by `/dev/sms/inbound` endpoint) and as `ISmsSender`. Add `using Teeforce.Api.Infrastructure.Configuration;` if not already present (it is — `AppSettings` uses it).

- [ ] **Step 2: Add Telnyx config section to appsettings.json files**

Add to `src/backend/Teeforce.Api/appsettings.json` (empty defaults):

```json
"Telnyx": {
    "ApiKey": "",
    "FromNumber": ""
}
```

Add to `src/backend/Teeforce.Api/appsettings.Test.json` (if it exists — the deployed test environment):

```json
"Telnyx": {
    "ApiKey": "",
    "FromNumber": ""
}
```

These will be populated from Azure Key Vault / app settings in each environment.

- [ ] **Step 3: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Api/Program.cs \
       src/backend/Teeforce.Api/appsettings.json \
       src/backend/Teeforce.Api/appsettings.Test.json
git commit -m "feat: wire up NotificationService and channel senders in DI"
```

---

### Task 7: Migrate SMS Handlers to INotificationService

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/ConfirmationSmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/SendSmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/SmsHandler.cs`

Each handler changes from `ITextMessageService` + golfer phone lookup to `INotificationService.Send(golferId, message)`. The notification service handles contact resolution internally.

- [ ] **Step 1: Migrate BookingCancelledSmsHandler**

```csharp
using Microsoft.Extensions.Logging;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCancelledSmsHandler
{
    public static async Task Handle(
        BookingCancelled evt,
        IBookingRepository bookingRepository,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        ILogger logger,
        CancellationToken ct)
    {
        if (evt.PreviousStatus != BookingStatus.Confirmed)
        {
            logger.LogWarning("Booking {BookingId} was cancelled from {PreviousStatus} status, skipping SMS (only confirmed bookings receive cancellation SMS)", evt.BookingId, evt.PreviousStatus);
            return;
        }

        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        var course = await courseRepository.GetByIdAsync(booking.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for BookingCancelled event {EventId}, skipping SMS", booking.CourseId, evt.EventId);
            return;
        }

        var message = $"Your tee time at {course.Name} on {booking.TeeTime.Date:MMMM d, yyyy} at {booking.TeeTime.Time:h:mm tt} has been cancelled.";
        await notificationService.Send(booking.GolferId, message, ct);
    }
}
```

**Changes:** Removed `IGolferRepository` dependency, removed golfer null check (notification service handles missing contact info), replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(booking.GolferId, ...)`.

- [ ] **Step 2: Migrate BookingCreatedConfirmationSmsHandler**

```csharp
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCreatedConfirmationSmsHandler
{
    public static async Task Handle(
        BookingCreated domainEvent,
        IBookingRepository bookingRepository,
        ApplicationDbContext db,
        INotificationService notificationService,
        CancellationToken ct)
    {
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == domainEvent.CourseId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Course {domainEvent.CourseId} not found for event {nameof(BookingCreated)}.");

        var booking = await bookingRepository.GetRequiredByIdAsync(domainEvent.BookingId);

        var message = $"You're booked! {courseName} at {booking.TeeTime.Time:h:mm tt} on {booking.TeeTime.Date:MMMM d, yyyy}. See you on the course!";
        await notificationService.Send(domainEvent.GolferId, message, ct);
    }
}
```

**Changes:** Removed `IGolferRepository`, replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(domainEvent.GolferId, ...)`.

- [ ] **Step 3: Migrate GolferJoinedWaitlistSmsHandler**

```csharp
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class GolferJoinedWaitlistSmsHandler
{
    public static async Task Handle(
        GolferJoinedWaitlist domainEvent,
        INotificationService notificationService,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var courseName = await db.CourseWaitlists
            .IgnoreQueryFilters()
            .Where(w => w.Id == domainEvent.CourseWaitlistId)
            .Join(db.Courses.IgnoreQueryFilters(), w => w.CourseId, c => c.Id, (w, c) => c.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"CourseWaitlist {domainEvent.CourseWaitlistId} or its course not found for event {nameof(GolferJoinedWaitlist)}.");

        var message = $"You're on the waitlist at {courseName}. Keep your phone handy - we'll text you when a spot opens up!";
        await notificationService.Send(domainEvent.GolferId, message, ct);
    }
}
```

**Changes:** Removed `IGolferRepository`, replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(domainEvent.GolferId, ...)`.

- [ ] **Step 4: Migrate WaitlistOfferCreatedSendSmsHandler**

```csharp
using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferCreatedSendSmsHandler
{
    public static async Task Handle(
        WaitlistOfferCreated evt,
        IWaitlistOfferRepository offerRepository,
        ITeeTimeOpeningRepository openingRepository,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        IOptions<AppSettings> appSettings,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(evt.WaitlistOfferId);
        var opening = await openingRepository.GetRequiredByIdAsync(evt.OpeningId);

        var course = await courseRepository.GetByIdAsync(opening.CourseId);
        var courseName = course?.Name ?? "Course";

        var baseUrl = appSettings.Value.FrontendUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException(
                "App:FrontendUrl is not configured. SMS offer links require a valid frontend URL.");
        }

        var message =
            $"{courseName}: {opening.TeeTime.Time:h:mm tt} tee time available! Claim your spot: {baseUrl}/book/walkup/{offer.Token}";
        await notificationService.Send(evt.GolferId, message, ct);

        offer.MarkNotified(timeProvider);
    }
}
```

**Changes:** Removed `IGolferRepository`, replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(evt.GolferId, ...)`.

- [ ] **Step 5: Migrate WaitlistOfferRejectedSmsHandler**

```csharp
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistOfferAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class WaitlistOfferRejectedSmsHandler
{
    public static async Task Handle(
        WaitlistOfferRejected domainEvent,
        IWaitlistOfferRepository offerRepository,
        IGolferWaitlistEntryRepository entryRepository,
        INotificationService notificationService,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(domainEvent.WaitlistOfferId);

        if (offer.NotifiedAt is null)
        {
            // Golfer was never texted about this offer — no SMS to send
            return;
        }

        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);

        // Skip if golfer was already removed from the waitlist
        if (entry.RemovedAt is not null)
        {
            return;
        }

        var message = "Sorry, that tee time is no longer available.";
        await notificationService.Send(entry.GolferId, message, ct);
    }
}
```

**Changes:** Removed `IGolferRepository`, replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(entry.GolferId, ...)`.

- [ ] **Step 6: Migrate TeeTimeOpeningSlotsClaimedSmsHandler**

```csharp
using Microsoft.Extensions.Logging;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public static class TeeTimeOpeningSlotsClaimedSmsHandler
{
    public static async Task Handle(
        TeeTimeOpeningSlotsClaimed evt,
        ICourseRepository courseRepository,
        INotificationService notificationService,
        ILogger logger,
        CancellationToken ct)
    {
        var course = await courseRepository.GetByIdAsync(evt.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping SMS", evt.CourseId, evt.EventId);
            return;
        }

        var message = $"Your tee time at {course.Name} on {evt.Date:MMMM d} at {evt.TeeTime:h:mm tt} is confirmed. See you on the course!";
        await notificationService.Send(evt.GolferId, message, ct);
    }
}
```

**Changes:** Removed `IGolferRepository` and golfer null check (notification service handles it), replaced `textMessageService.SendAsync(golfer.Phone, ...)` with `notificationService.Send(evt.GolferId, ...)`.

- [ ] **Step 7: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build errors — existing tests still reference `ITextMessageService`. That's expected, we'll fix tests in the next task.

- [ ] **Step 8: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/ConfirmationSmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/SendSmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/SmsHandler.cs
git commit -m "refactor: migrate all SMS handlers to INotificationService"
```

---

### Task 8: Update Existing Handler Tests

**Files:**
- Modify: `tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/BookingCancelledSmsHandlerTests.cs`
- Modify: `tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs`

Tests need to substitute `INotificationService` instead of `ITextMessageService`, remove golfer repository stubs where the handler no longer needs them, and verify `notificationService.Send(golferId, message)` instead of `sms.SendAsync(phone, message)`.

- [ ] **Step 1: Update BookingCancelledSmsHandlerTests**

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class BookingCancelledSmsHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    private static BookingCancelled BuildEvent(Guid? bookingId = null, BookingStatus? previousStatus = null)
    {
        return new BookingCancelled
        {
            BookingId = bookingId ?? Guid.NewGuid(),
            PreviousStatus = previousStatus ?? BookingStatus.Confirmed
        };
    }

    private static Booking BuildCancelledBooking(Guid? bookingId = null, Guid? courseId = null, Guid? golferId = null)
    {
        var booking = Booking.CreateConfirmed(
            bookingId ?? Guid.CreateVersion7(),
            courseId ?? Guid.NewGuid(),
            golferId ?? Guid.NewGuid(),
            new DateOnly(2026, 7, 4),
            new TimeOnly(8, 0),
            2);
        booking.Cancel();
        booking.ClearDomainEvents();
        return booking;
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoNotificationAndLogsWarning()
    {
        var booking = BuildCancelledBooking();
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.courseRepo.GetByIdAsync(booking.CourseId).Returns((Course?)null);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsCancellationNotification()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var booking = BuildCancelledBooking(golferId: golferId, courseId: course.Id);
        var evt = BuildEvent(bookingId: booking.Id);

        this.bookingRepo.GetByIdAsync(booking.Id).Returns(booking);
        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<string>(m => m.Contains("Teeforce Golf Club") && m.Contains("cancelled") && m.Contains("July 4, 2026") && m.Contains("8:00 AM")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingBookingCancelled_NoNotificationAndLogsWarning()
    {
        var evt = BuildEvent(previousStatus: BookingStatus.Pending);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.bookingRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
```

**Changes:** Removed `IGolferRepository` stub, replaced `ITextMessageService` with `INotificationService`, assertions now verify `Send(golferId, message)` instead of `SendAsync(phone, message)`. Removed the `GolferNotFound` test — the handler no longer looks up golfers, so that scenario is handled by `NotificationService` (tested in Task 3). The `CourseNotFound` test now passes a `golferId` instead of a `Golfer` object.

- [ ] **Step 2: Update TeeTimeOpeningSlotsClaimedSmsHandlerTests**

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Waitlist.Handlers;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate.Events;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

public class TeeTimeOpeningSlotsClaimedSmsHandlerTests
{
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly INotificationService notificationService = Substitute.For<INotificationService>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    private static TeeTimeOpeningSlotsClaimed BuildEvent(
        Guid? golferId = null,
        Guid? courseId = null)
    {
        return new TeeTimeOpeningSlotsClaimed
        {
            OpeningId = Guid.NewGuid(),
            BookingId = Guid.CreateVersion7(),
            GolferId = golferId ?? Guid.NewGuid(),
            CourseId = courseId ?? Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 15),
            TeeTime = new TimeOnly(9, 30),
            GroupSize = 2
        };
    }

    [Fact]
    public async Task Handle_CourseNotFound_NoNotificationAndLogsWarning()
    {
        var evt = BuildEvent();
        this.courseRepo.GetByIdAsync(evt.CourseId).Returns((Course?)null);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_Success_SendsNotificationToGolferWithConfirmationContent()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var evt = BuildEvent(golferId: golferId, courseId: course.Id);

        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<string>(m => m.Contains("Teeforce Golf Club") && m.Contains("confirmed")),
            Arg.Any<CancellationToken>());
    }
}
```

**Changes:** Removed `IGolferRepository`, replaced `ITextMessageService` with `INotificationService`, removed `GolferNotFound` test (handled by NotificationService). Assertions verify `Send(golferId, message)`.

- [ ] **Step 3: Run all tests**

Run: `dotnet test tests/Teeforce.Api.Tests --no-restore -v n`
Expected: All tests PASS

- [ ] **Step 4: Commit**

```bash
git add tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/BookingCancelledSmsHandlerTests.cs \
       tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs
git commit -m "test: update handler tests for INotificationService"
```

---

### Task 9: Update DevSmsEndpoints and Remove Old Services

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Dev/DevSmsEndpoints.cs:52` (change `DatabaseTextMessageService` → `DatabaseSmsSender`)
- Delete: `src/backend/Teeforce.Domain/Common/ITextMessageService.cs`
- Delete: `src/backend/Teeforce.Api/Infrastructure/Services/DatabaseTextMessageService.cs`
- Delete: `src/backend/Teeforce.Api/Infrastructure/Services/InMemoryTextMessageService.cs`

- [ ] **Step 1: Update DevSmsEndpoints inbound endpoint**

In `DevSmsEndpoints.cs`, change the inbound SMS endpoint to inject `DatabaseSmsSender` instead of `DatabaseTextMessageService`:

```csharp
group.MapPost("/inbound", async (InboundSmsRequest request, DatabaseSmsSender smsSender) =>
{
    await smsSender.AddInbound(request.FromPhoneNumber, request.Message);
    return Results.Ok();
}).WithSummary("Simulate an inbound SMS from a golfer");
```

- [ ] **Step 2: Delete old services**

```bash
rm src/backend/Teeforce.Domain/Common/ITextMessageService.cs
rm src/backend/Teeforce.Api/Infrastructure/Services/DatabaseTextMessageService.cs
rm src/backend/Teeforce.Api/Infrastructure/Services/InMemoryTextMessageService.cs
```

- [ ] **Step 3: Move SmsDirection enum**

`SmsDirection` is currently defined in `InMemoryTextMessageService.cs`. It's still used by `DevSmsMessage` and `DatabaseSmsSender`. Move it to its own file or into `DevSmsMessage.cs` since it's a dev concern.

Create `src/backend/Teeforce.Api/Infrastructure/Services/SmsDirection.cs`:

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public enum SmsDirection
{
    Outbound,
    Inbound
}
```

- [ ] **Step 4: Update DevSmsEndpointsTests**

In `tests/Teeforce.Api.IntegrationTests/Features/Dev/DevSmsEndpointsTests.cs`, replace references to `DatabaseTextMessageService.SystemPhoneNumber` with `DatabaseSmsSender.SystemPhoneNumber`.

- [ ] **Step 5: Verify build and run all tests**

Run: `dotnet build teeforce.slnx && dotnet test teeforce.slnx --no-restore -v n`
Expected: Build succeeded, all tests PASS

- [ ] **Step 6: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: remove ITextMessageService and wire DatabaseSmsSender into dev endpoints"
```

---

### Task 10: Create GitHub Issue for Deferred Work

**Files:** None (GitHub CLI only)

- [ ] **Step 1: Create issue for AppUser notification preferences**

```bash
gh issue create \
  --title "AppUser notification preferences" \
  --body "## Summary
Add per-user notification preference settings so users can choose their preferred notification channel(s).

## Context
The INotificationService abstraction (from notification-service branch) currently defaults to SMS-first, email-fallback routing. This issue adds user-controlled preferences.

## Requirements
- AppUser gets a notification preferences model (preferred channels, quiet hours, opt-out)
- NotificationService reads preferences before routing
- Default behavior (SMS > email) applies when no preference is set
- Settings UI for users to manage their preferences

## Dependencies
- Depends on INotificationService being merged
- Depends on Golfer/AppUser identity consolidation

## Acceptance Criteria
- [ ] AppUser has notification preference settings persisted in the database
- [ ] NotificationService respects user channel preferences when routing
- [ ] Users can update their notification preferences via API
- [ ] Users without preferences get the default routing behavior" \
  --label "enhancement"
```

- [ ] **Step 2: Commit (nothing to commit — just the issue)**

Record the issue number for reference.

---

### Task 11: Verify Full Build and Run Dev Environment

- [ ] **Step 1: Run full test suite**

Run: `dotnet test teeforce.slnx --no-restore -v n`
Expected: All tests PASS

- [ ] **Step 2: Run make dev**

Run: `make dev`
Expected: API starts on :5221, Web on :3000, no startup errors. Check logs for any DI resolution failures.

- [ ] **Step 3: Verify /dev/sms endpoints still work**

Using the running dev server, test that `/dev/sms` still returns messages and `/dev/sms/inbound` still accepts simulated inbound SMS.

- [ ] **Step 4: Run dotnet format**

Run: `dotnet format teeforce.slnx`
Expected: No changes (or minimal formatting fixes)

- [ ] **Step 5: Final commit if any format changes**

```bash
git add -A
git commit -m "chore: format"
```
