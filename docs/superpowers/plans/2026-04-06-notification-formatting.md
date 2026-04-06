# Notification Formatting + Wolverine Command Dispatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace plain string messages with typed `INotification` objects, add channel-specific formatters, and deliver notifications via Wolverine's transactional outbox with separate `DeliverSms` and `DeliverEmail` commands.

**Architecture:** Notification handlers (reacting to domain events) build typed `INotification` records and call `INotificationService.Send<T>()`. The `NotificationService` implementation resolves the user's contact info, picks the channel, resolves the appropriate formatter (SMS or email), formats the message, and publishes a `DeliverSms` or `DeliverEmail` command via Wolverine's `IMessageBus`. The delivery handlers process these commands through the transactional outbox, giving retry safety and crash resilience. Wolverine doesn't support open generic handlers, so formatting happens at publish time (in `NotificationService.Send<T>`) — formatters are pure functions with no I/O, so this is safe. The outbox commands carry pre-formatted channel-specific content.

**Tech Stack:** .NET 10, WolverineFx (transactional outbox, SQL Server transport), EF Core 10, NSubstitute, keyed DI services

**Worktree:** `.worktrees/issue/notification-service` (branch: `issue/notification-service`)

**Contact info lookup order:**
1. AppUser by ID → check Phone, then Email
2. If no AppUser, Golfer by ID → check Phone (legacy fallback)
3. Route: phone → `DeliverSms`, else email → `DeliverEmail`, else log warning and skip

---

### Task 1: Add Phone to AppUser + EF Migration

**Files:**
- Modify: `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs`
- Create: EF migration (auto-generated)

- [ ] **Step 1: Add Phone property to AppUser**

In `src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs`, add after `Email` property (line 12):

```csharp
public string? Phone { get; private set; }
```

- [ ] **Step 2: Add EF configuration for Phone**

In `AppUserConfiguration.cs`, add after the `Email` property configuration (line 18):

```csharp
        builder.Property(u => u.Phone).IsRequired(false).HasMaxLength(20);
```

- [ ] **Step 3: Generate EF migration**

Run:
```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add AddAppUserPhone --project src/backend/Teeforce.Api
```

- [ ] **Step 4: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Domain/AppUserAggregate/AppUser.cs \
       src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/AppUserConfiguration.cs \
       src/backend/Teeforce.Api/Migrations/
git commit -m "feat: add Phone property to AppUser for notification routing"
```

---

### Task 2: INotification Marker Interface + Update INotificationService Signature

**Files:**
- Create: `src/backend/Teeforce.Domain/Common/INotification.cs`
- Modify: `src/backend/Teeforce.Domain/Common/INotificationService.cs`

- [ ] **Step 1: Create INotification marker interface**

```csharp
namespace Teeforce.Domain.Common;

public interface INotification;
```

- [ ] **Step 2: Update INotificationService signature**

Replace the contents of `INotificationService.cs`:

```csharp
namespace Teeforce.Domain.Common;

public interface INotificationService
{
    Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification;
}
```

- [ ] **Step 3: Verify build fails**

Run: `dotnet build teeforce.slnx`
Expected: Build errors — `NotificationService` and all handlers still pass `string` to `Send`. This is expected.

- [ ] **Step 4: Commit**

```bash
git add src/backend/Teeforce.Domain/Common/INotification.cs \
       src/backend/Teeforce.Domain/Common/INotificationService.cs
git commit -m "feat: add INotification marker interface and update INotificationService signature"
```

---

### Task 3: Formatter Interfaces and Default Email Formatter

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/ISmsFormatter.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/IEmailFormatter.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/DefaultEmailFormatter.cs`
- Create: `tests/Teeforce.Api.Tests/Services/DefaultEmailFormatterTests.cs`

Formatters use a non-generic interface with a generic abstract base class. This allows keyed DI resolution by notification runtime type while giving concrete formatters type-safe access to the notification data.

- [ ] **Step 1: Create ISmsFormatter**

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface ISmsFormatter
{
    string Format(INotification notification);
}

public abstract class SmsFormatter<T> : ISmsFormatter where T : INotification
{
    public string Format(INotification notification) => FormatMessage((T)notification);

    protected abstract string FormatMessage(T notification);
}
```

- [ ] **Step 2: Create IEmailFormatter**

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface IEmailFormatter
{
    (string Subject, string Body) Format(INotification notification);
}

public abstract class EmailFormatter<T> : IEmailFormatter where T : INotification
{
    public (string Subject, string Body) Format(INotification notification) => FormatMessage((T)notification);

    protected abstract (string Subject, string Body) FormatMessage(T notification);
}
```

- [ ] **Step 3: Write DefaultEmailFormatter tests**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Tests.Services;

public record TestNotification(string Message) : INotification;

public class TestSmsFormatter : SmsFormatter<TestNotification>
{
    protected override string FormatMessage(TestNotification notification) => notification.Message;
}

public class DefaultEmailFormatterTests
{
    [Fact]
    public void Format_UsesSmsFormatterTextAsBody()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<ISmsFormatter, TestSmsFormatter>(typeof(TestNotification));
        var sp = services.BuildServiceProvider();
        var logger = Substitute.For<ILogger<DefaultEmailFormatter>>();

        var sut = new DefaultEmailFormatter(sp, logger);
        var notification = new TestNotification("Hello from SMS");

        var (subject, body) = sut.Format(notification);

        Assert.Equal("Teeforce Notification", subject);
        Assert.Equal("Hello from SMS", body);
    }

    [Fact]
    public void Format_LogsInformationAboutFallback()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<ISmsFormatter, TestSmsFormatter>(typeof(TestNotification));
        var sp = services.BuildServiceProvider();
        var logger = Substitute.For<ILogger<DefaultEmailFormatter>>();

        var sut = new DefaultEmailFormatter(sp, logger);
        sut.Format(new TestNotification("test"));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~DefaultEmailFormatterTests" --no-restore -v n`
Expected: FAIL — `DefaultEmailFormatter` doesn't exist yet

- [ ] **Step 5: Implement DefaultEmailFormatter**

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public class DefaultEmailFormatter(IServiceProvider serviceProvider, ILogger<DefaultEmailFormatter> logger) : IEmailFormatter
{
    public (string Subject, string Body) Format(INotification notification)
    {
        var smsFormatter = serviceProvider.GetRequiredKeyedService<ISmsFormatter>(notification.GetType());

        logger.LogInformation(
            "No dedicated email formatter for {NotificationType}, using SMS text as email body",
            notification.GetType().Name);

        var body = smsFormatter.Format(notification);
        return ("Teeforce Notification", body);
    }
}
```

**Note:** Add `using Microsoft.Extensions.DependencyInjection;` for `GetRequiredKeyedService`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~DefaultEmailFormatterTests" --no-restore -v n`
Expected: All 2 tests PASS

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/ISmsFormatter.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/IEmailFormatter.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/DefaultEmailFormatter.cs \
       tests/Teeforce.Api.Tests/Services/DefaultEmailFormatterTests.cs
git commit -m "feat: add formatter interfaces and DefaultEmailFormatter with SMS text fallback"
```

---

### Task 4: Notification Types + SMS Formatters

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/BookingConfirmation.cs`
- Create: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/BookingCancellation.cs`
- Create: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/WaitlistJoined.cs`
- Create: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/WaitlistOfferAvailable.cs`
- Create: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/WaitlistOfferExpired.cs`
- Create: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/WalkupConfirmation.cs`
- Create: `tests/Teeforce.Api.Tests/Features/Notifications/SmsFormatterTests.cs`

Each file contains the notification record and its SMS formatter. This follows the command colocation convention — the notification type lives with the handler that creates it.

- [ ] **Step 1: Create BookingConfirmation + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingConfirmation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingConfirmationSmsFormatter : SmsFormatter<BookingConfirmation>
{
    protected override string FormatMessage(BookingConfirmation n) =>
        $"You're booked! {n.CourseName} at {n.Time:h:mm tt} on {n.Date:MMMM d, yyyy}. See you on the course!";
}
```

- [ ] **Step 2: Create BookingCancellation + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Bookings.Handlers;

public record BookingCancellation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class BookingCancellationSmsFormatter : SmsFormatter<BookingCancellation>
{
    protected override string FormatMessage(BookingCancellation n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d, yyyy} at {n.Time:h:mm tt} has been cancelled.";
}
```

- [ ] **Step 3: Create WaitlistJoined + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistJoined(string CourseName) : INotification;

public class WaitlistJoinedSmsFormatter : SmsFormatter<WaitlistJoined>
{
    protected override string FormatMessage(WaitlistJoined n) =>
        $"You're on the waitlist at {n.CourseName}. Keep your phone handy - we'll text you when a spot opens up!";
}
```

- [ ] **Step 4: Create WaitlistOfferAvailable + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferAvailable(string CourseName, TimeOnly Time, string ClaimUrl) : INotification;

public class WaitlistOfferAvailableSmsFormatter : SmsFormatter<WaitlistOfferAvailable>
{
    protected override string FormatMessage(WaitlistOfferAvailable n) =>
        $"{n.CourseName}: {n.Time:h:mm tt} tee time available! Claim your spot: {n.ClaimUrl}";
}
```

- [ ] **Step 5: Create WaitlistOfferExpired + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistOfferExpired : INotification;

public class WaitlistOfferExpiredSmsFormatter : SmsFormatter<WaitlistOfferExpired>
{
    protected override string FormatMessage(WaitlistOfferExpired n) =>
        "Sorry, that tee time is no longer available.";
}
```

- [ ] **Step 6: Create WalkupConfirmation + SMS formatter**

```csharp
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WalkupConfirmation(string CourseName, DateOnly Date, TimeOnly Time) : INotification;

public class WalkupConfirmationSmsFormatter : SmsFormatter<WalkupConfirmation>
{
    protected override string FormatMessage(WalkupConfirmation n) =>
        $"Your tee time at {n.CourseName} on {n.Date:MMMM d} at {n.Time:h:mm tt} is confirmed. See you on the course!";
}
```

- [ ] **Step 7: Write SMS formatter tests**

```csharp
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Api.Features.Waitlist.Handlers;

namespace Teeforce.Api.Tests.Features.Notifications;

public class SmsFormatterTests
{
    [Fact]
    public void BookingConfirmation_FormatsCorrectly()
    {
        var formatter = new BookingConfirmationSmsFormatter();
        var notification = new BookingConfirmation("Teeforce Golf Club", new DateOnly(2026, 7, 4), new TimeOnly(8, 0));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("8:00 AM", result);
        Assert.Contains("July 4, 2026", result);
        Assert.Contains("booked", result);
    }

    [Fact]
    public void BookingCancellation_FormatsCorrectly()
    {
        var formatter = new BookingCancellationSmsFormatter();
        var notification = new BookingCancellation("Teeforce Golf Club", new DateOnly(2026, 7, 4), new TimeOnly(8, 0));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("cancelled", result);
        Assert.Contains("July 4, 2026", result);
        Assert.Contains("8:00 AM", result);
    }

    [Fact]
    public void WaitlistJoined_FormatsCorrectly()
    {
        var formatter = new WaitlistJoinedSmsFormatter();
        var notification = new WaitlistJoined("Teeforce Golf Club");

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("waitlist", result);
    }

    [Fact]
    public void WaitlistOfferAvailable_FormatsWithClaimUrl()
    {
        var formatter = new WaitlistOfferAvailableSmsFormatter();
        var notification = new WaitlistOfferAvailable("Teeforce Golf Club", new TimeOnly(9, 30), "https://app.teeforce.com/book/walkup/abc123");

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("9:30 AM", result);
        Assert.Contains("https://app.teeforce.com/book/walkup/abc123", result);
    }

    [Fact]
    public void WaitlistOfferExpired_FormatsStaticMessage()
    {
        var formatter = new WaitlistOfferExpiredSmsFormatter();
        var notification = new WaitlistOfferExpired();

        var result = formatter.Format(notification);

        Assert.Contains("no longer available", result);
    }

    [Fact]
    public void WalkupConfirmation_FormatsCorrectly()
    {
        var formatter = new WalkupConfirmationSmsFormatter();
        var notification = new WalkupConfirmation("Teeforce Golf Club", new DateOnly(2026, 6, 15), new TimeOnly(9, 30));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("confirmed", result);
        Assert.Contains("June 15", result);
        Assert.Contains("9:30 AM", result);
    }
}
```

- [ ] **Step 8: Run formatter tests**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~SmsFormatterTests" --no-restore -v n`
Expected: All 6 tests PASS

- [ ] **Step 9: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/BookingConfirmation.cs \
       src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/BookingCancellation.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/WaitlistJoined.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/WaitlistOfferAvailable.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/WaitlistOfferExpired.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/WalkupConfirmation.cs \
       tests/Teeforce.Api.Tests/Features/Notifications/SmsFormatterTests.cs
git commit -m "feat: add 6 notification types with SMS formatters"
```

---

### Task 5: DeliverSms and DeliverEmail Commands + Handlers

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/DeliverSmsHandler.cs`
- Create: `src/backend/Teeforce.Api/Infrastructure/Services/DeliverEmailHandler.cs`
- Create: `tests/Teeforce.Api.Tests/Services/DeliverSmsHandlerTests.cs`
- Create: `tests/Teeforce.Api.Tests/Services/DeliverEmailHandlerTests.cs`

These are Wolverine command handlers processed through the transactional outbox. They receive pre-formatted content and deliver via the appropriate channel sender.

- [ ] **Step 1: Write DeliverSms handler tests**

```csharp
using NSubstitute;
using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Tests.Services;

public class DeliverSmsHandlerTests
{
    private readonly ISmsSender smsSender = Substitute.For<ISmsSender>();

    [Fact]
    public async Task Handle_SendsSmsToPhoneNumber()
    {
        var command = new DeliverSms("+15551234567", "Your tee time is confirmed.");

        await DeliverSmsHandler.Handle(command, this.smsSender, CancellationToken.None);

        await this.smsSender.Received(1).Send("+15551234567", "Your tee time is confirmed.", Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Write DeliverEmail handler tests**

```csharp
using NSubstitute;
using Teeforce.Api.Infrastructure.Services;

namespace Teeforce.Api.Tests.Services;

public class DeliverEmailHandlerTests
{
    private readonly IEmailSender emailSender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task Handle_SendsEmailWithSubjectAndBody()
    {
        var command = new DeliverEmail("golfer@example.com", "Booking Confirmed", "You're booked!");

        await DeliverEmailHandler.Handle(command, this.emailSender, CancellationToken.None);

        await this.emailSender.Received(1).Send("golfer@example.com", "Booking Confirmed", "You're booked!", Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~DeliverSmsHandlerTests|FullyQualifiedName~DeliverEmailHandlerTests" --no-restore -v n`
Expected: FAIL — classes don't exist yet

- [ ] **Step 4: Implement DeliverSms command + handler**

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public record DeliverSms(string PhoneNumber, string Message);

public static class DeliverSmsHandler
{
    public static async Task Handle(DeliverSms command, ISmsSender smsSender, CancellationToken ct)
    {
        await smsSender.Send(command.PhoneNumber, command.Message, ct);
    }
}
```

- [ ] **Step 5: Implement DeliverEmail command + handler**

```csharp
namespace Teeforce.Api.Infrastructure.Services;

public record DeliverEmail(string EmailAddress, string Subject, string Body);

public static class DeliverEmailHandler
{
    public static async Task Handle(DeliverEmail command, IEmailSender emailSender, CancellationToken ct)
    {
        await emailSender.Send(command.EmailAddress, command.Subject, command.Body, ct);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~DeliverSmsHandlerTests|FullyQualifiedName~DeliverEmailHandlerTests" --no-restore -v n`
Expected: All 2 tests PASS

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/DeliverSmsHandler.cs \
       src/backend/Teeforce.Api/Infrastructure/Services/DeliverEmailHandler.cs \
       tests/Teeforce.Api.Tests/Services/DeliverSmsHandlerTests.cs \
       tests/Teeforce.Api.Tests/Services/DeliverEmailHandlerTests.cs
git commit -m "feat: add DeliverSms and DeliverEmail Wolverine command handlers"
```

---

### Task 6: Rewrite NotificationService (TDD)

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/Services/NotificationService.cs`
- Modify: `tests/Teeforce.Api.Tests/Services/NotificationServiceTests.cs`

The `NotificationService` is completely rewritten. It now:
1. Resolves contact info (AppUser phone → AppUser email → Golfer phone fallback)
2. Resolves the appropriate formatter (SMS or email) via keyed DI
3. Formats the notification
4. Publishes `DeliverSms` or `DeliverEmail` via `IMessageBus`

- [ ] **Step 1: Rewrite NotificationService tests**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.Services;
using Wolverine;

namespace Teeforce.Api.Tests.Services;

public record FakeNotification(string Content) : INotification;

public class FakeNotificationSmsFormatter : SmsFormatter<FakeNotification>
{
    protected override string FormatMessage(FakeNotification n) => $"SMS: {n.Content}";
}

public class FakeNotificationEmailFormatter : EmailFormatter<FakeNotification>
{
    protected override (string Subject, string Body) FormatMessage(FakeNotification n) =>
        ("Test Subject", $"Email: {n.Content}");
}

public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly IMessageBus messageBus;
    private readonly ILogger<NotificationService> logger;
    private readonly IAppUserEmailUniquenessChecker emailChecker;
    private readonly ServiceProvider serviceProvider;
    private readonly NotificationService sut;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        this.db = new ApplicationDbContext(options, userContext: null);
        this.messageBus = Substitute.For<IMessageBus>();
        this.logger = Substitute.For<ILogger<NotificationService>>();
        this.emailChecker = Substitute.For<IAppUserEmailUniquenessChecker>();
        this.emailChecker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var services = new ServiceCollection();
        services.AddKeyedScoped<ISmsFormatter, FakeNotificationSmsFormatter>(typeof(FakeNotification));
        services.AddKeyedScoped<IEmailFormatter, FakeNotificationEmailFormatter>(typeof(FakeNotification));
        services.AddScoped<DefaultEmailFormatter>();
        services.AddScoped<ILogger<DefaultEmailFormatter>>(_ => Substitute.For<ILogger<DefaultEmailFormatter>>());
        this.serviceProvider = services.BuildServiceProvider();

        this.sut = new NotificationService(this.db, this.messageBus, this.serviceProvider, this.logger);
    }

    public void Dispose()
    {
        this.db.Dispose();
        this.serviceProvider.Dispose();
    }

    [Fact]
    public async Task Send_AppUserWithPhone_PublishesDeliverSms()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        // Set phone via reflection since there's no public setter yet
        typeof(AppUser).GetProperty("Phone")!.SetValue(user, "+15551234567");
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverSms>(cmd => cmd.PhoneNumber == "+15551234567" && cmd.Message == "SMS: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_AppUserWithEmailOnly_PublishesDeliverEmail()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverEmail>(cmd => cmd.EmailAddress == "jane@example.com" && cmd.Subject == "Test Subject" && cmd.Body == "Email: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_NoAppUserButGolferWithPhone_PublishesDeliverSms()
    {
        var golfer = Golfer.Create("+15559876543", "Bob", "Green");
        this.db.Golfers.Add(golfer);
        await this.db.SaveChangesAsync();

        await this.sut.Send(golfer.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverSms>(cmd => cmd.PhoneNumber == "+15559876543" && cmd.Message == "SMS: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_NoContactInfo_LogsWarningAndSkips()
    {
        var unknownId = Guid.CreateVersion7();

        await this.sut.Send(unknownId, new FakeNotification("hello"));

        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverSms>(), Arg.Any<DeliveryOptions?>());
        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverEmail>(), Arg.Any<DeliveryOptions?>());
        this.logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Send_AppUserWithPhoneAndEmail_PrefersSms()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        typeof(AppUser).GetProperty("Phone")!.SetValue(user, "+15551234567");
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(Arg.Any<DeliverSms>(), Arg.Any<DeliveryOptions?>());
        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverEmail>(), Arg.Any<DeliveryOptions?>());
    }
}
```

**Note:** The `PublishAsync` call on `IMessageBus` takes an optional `DeliveryOptions?` parameter — NSubstitute needs `Arg.Any<DeliveryOptions?>()` to match it. The test uses reflection to set `Phone` on `AppUser` since there's no public method for it yet. The implementing agent should check whether `IMessageBus.PublishAsync` is a generic method or takes `object` — adjust the `Received` assertions accordingly.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~NotificationServiceTests" --no-restore -v n`
Expected: FAIL — `NotificationService` constructor signature doesn't match

- [ ] **Step 3: Rewrite NotificationService**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Common;
using Wolverine;

namespace Teeforce.Api.Infrastructure.Services;

public class NotificationService(
    ApplicationDbContext db,
    IMessageBus messageBus,
    IServiceProvider serviceProvider,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task Send<T>(Guid appUserId, T notification, CancellationToken ct = default) where T : INotification
    {
        // 1. Resolve contact info — AppUser first, then Golfer fallback
        var appUser = await db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Id == appUserId)
            .Select(u => new { u.Phone, u.Email })
            .FirstOrDefaultAsync(ct);

        var phone = appUser?.Phone;
        var email = appUser?.Email;

        // Golfer fallback for phone if AppUser has no phone
        if (string.IsNullOrEmpty(phone))
        {
            phone = await db.Golfers
                .IgnoreQueryFilters()
                .Where(g => g.Id == appUserId)
                .Select(g => g.Phone)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Route and format
        if (!string.IsNullOrEmpty(phone))
        {
            var smsFormatter = serviceProvider.GetRequiredKeyedService<ISmsFormatter>(typeof(T));
            var message = smsFormatter.Format(notification);
            await messageBus.PublishAsync(new DeliverSms(phone, message));
            return;
        }

        if (!string.IsNullOrEmpty(email))
        {
            var emailFormatter = serviceProvider.GetKeyedService<IEmailFormatter>(typeof(T))
                ?? serviceProvider.GetRequiredService<DefaultEmailFormatter>();
            var (subject, body) = emailFormatter.Format(notification);
            await messageBus.PublishAsync(new DeliverEmail(email, subject, body));
            return;
        }

        logger.LogWarning("No contact info found for user {AppUserId}, skipping notification", appUserId);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~NotificationServiceTests" --no-restore -v n`
Expected: All 5 tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Services/NotificationService.cs \
       tests/Teeforce.Api.Tests/Services/NotificationServiceTests.cs
git commit -m "refactor: rewrite NotificationService with formatter resolution and outbox dispatch"
```

---

### Task 7: Update Notification Handlers to Build Notification Objects

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/ConfirmationSmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/SendSmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/SmsHandler.cs`
- Modify: `src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/SmsHandler.cs`

Each handler changes from building a string message to building a typed notification object.

- [ ] **Step 1: Update BookingCreatedConfirmationSmsHandler**

Replace the full file:

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

        var notification = new BookingConfirmation(courseName, booking.TeeTime.Date, booking.TeeTime.Time);
        await notificationService.Send(domainEvent.GolferId, notification, ct);
    }
}
```

- [ ] **Step 2: Update BookingCancelledSmsHandler**

Replace the full file:

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
            logger.LogWarning("Booking {BookingId} was cancelled from {PreviousStatus} status, skipping notification (only confirmed bookings receive cancellation notifications)", evt.BookingId, evt.PreviousStatus);
            return;
        }

        var booking = await bookingRepository.GetRequiredByIdAsync(evt.BookingId);

        var course = await courseRepository.GetByIdAsync(booking.CourseId);

        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found for BookingCancelled event {EventId}, skipping notification", booking.CourseId, evt.EventId);
            return;
        }

        var notification = new BookingCancellation(course.Name, booking.TeeTime.Date, booking.TeeTime.Time);
        await notificationService.Send(booking.GolferId, notification, ct);
    }
}
```

- [ ] **Step 3: Update GolferJoinedWaitlistSmsHandler**

Replace the full file:

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

        var notification = new WaitlistJoined(courseName);
        await notificationService.Send(domainEvent.GolferId, notification, ct);
    }
}
```

- [ ] **Step 4: Update WaitlistOfferCreatedSendSmsHandler**

Replace the full file:

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
                "App:FrontendUrl is not configured. Offer claim links require a valid frontend URL.");
        }

        var claimUrl = $"{baseUrl}/book/walkup/{offer.Token}";
        var notification = new WaitlistOfferAvailable(courseName, opening.TeeTime.Time, claimUrl);
        await notificationService.Send(evt.GolferId, notification, ct);

        offer.MarkNotified(timeProvider);
    }
}
```

- [ ] **Step 5: Update WaitlistOfferRejectedSmsHandler**

Replace the full file:

```csharp
using Microsoft.Extensions.Logging;
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
        ILogger logger,
        CancellationToken ct)
    {
        var offer = await offerRepository.GetRequiredByIdAsync(domainEvent.WaitlistOfferId);

        if (offer.NotifiedAt is null)
        {
            logger.LogWarning("Offer {WaitlistOfferId} was never notified, skipping rejection notification", domainEvent.WaitlistOfferId);
            return;
        }

        var entry = await entryRepository.GetRequiredByIdAsync(domainEvent.GolferWaitlistEntryId);

        if (entry.RemovedAt is not null)
        {
            logger.LogWarning("Golfer waitlist entry {EntryId} already removed, skipping rejection notification", domainEvent.GolferWaitlistEntryId);
            return;
        }

        var notification = new WaitlistOfferExpired();
        await notificationService.Send(entry.GolferId, notification, ct);
    }
}
```

- [ ] **Step 6: Update TeeTimeOpeningSlotsClaimedSmsHandler**

Replace the full file:

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
            logger.LogWarning("Course {CourseId} not found for TeeTimeOpeningSlotsClaimed event {EventId}, skipping notification", evt.CourseId, evt.EventId);
            return;
        }

        var notification = new WalkupConfirmation(course.Name, evt.Date, evt.TeeTime);
        await notificationService.Send(evt.GolferId, notification, ct);
    }
}
```

- [ ] **Step 7: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeds (handler tests may still reference old `Send` signature — we fix those in Task 8)

- [ ] **Step 8: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCreated/ConfirmationSmsHandler.cs \
       src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/GolferJoinedWaitlist/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferCreated/SendSmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/WaitlistOfferRejected/SmsHandler.cs \
       src/backend/Teeforce.Api/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimed/SmsHandler.cs
git commit -m "refactor: update notification handlers to build typed notification objects"
```

---

### Task 8: Update Existing Handler Tests

**Files:**
- Modify: `tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/BookingCancelledSmsHandlerTests.cs`
- Modify: `tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs`
- Find and modify any other notification handler tests

Tests change from asserting `Send(golferId, Arg.Is<string>(m => m.Contains(...)))` to asserting `Send(golferId, Arg.Is<BookingCancellation>(n => n.CourseName == ...))`.

- [ ] **Step 1: Update BookingCancelledSmsHandlerTests**

Replace the full file:

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

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<BookingCancellation>(), Arg.Any<CancellationToken>());
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
            Arg.Is<BookingCancellation>(n =>
                n.CourseName == "Teeforce Golf Club" &&
                n.Date == new DateOnly(2026, 7, 4) &&
                n.Time == new TimeOnly(8, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingBookingCancelled_NoNotificationAndLogsWarning()
    {
        var evt = BuildEvent(previousStatus: BookingStatus.Pending);

        await BookingCancelledSmsHandler.Handle(evt, this.bookingRepo, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<BookingCancellation>(), Arg.Any<CancellationToken>());
        await this.bookingRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }
}
```

- [ ] **Step 2: Update TeeTimeOpeningSlotsClaimedSmsHandlerTests**

Replace the full file:

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

        await this.notificationService.DidNotReceive().Send(Arg.Any<Guid>(), Arg.Any<WalkupConfirmation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsWalkupConfirmationNotification()
    {
        var golferId = Guid.CreateVersion7();
        var course = Course.Create(Guid.NewGuid(), "Teeforce Golf Club", "America/Chicago");
        var evt = BuildEvent(golferId: golferId, courseId: course.Id);

        this.courseRepo.GetByIdAsync(course.Id).Returns(course);

        await TeeTimeOpeningSlotsClaimedSmsHandler.Handle(evt, this.courseRepo, this.notificationService, this.logger, CancellationToken.None);

        await this.notificationService.Received(1).Send(
            golferId,
            Arg.Is<WalkupConfirmation>(n =>
                n.CourseName == "Teeforce Golf Club" &&
                n.Date == new DateOnly(2026, 6, 15) &&
                n.Time == new TimeOnly(9, 30)),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 3: Run all tests**

Run: `dotnet test tests/Teeforce.Api.Tests --no-restore -v n`
Expected: All tests PASS

- [ ] **Step 4: Commit**

```bash
git add tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/BookingCancelledSmsHandlerTests.cs \
       tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs
git commit -m "test: update handler tests for typed notification objects"
```

---

### Task 9: DI Registration + Final Wiring

**Files:**
- Modify: `src/backend/Teeforce.Api/Program.cs`

Register all SMS formatters as keyed services, the `DefaultEmailFormatter`, and update the `NotificationService` registration.

- [ ] **Step 1: Update DI registration in Program.cs**

Replace the notification service registration block (around lines 94-95) with:

```csharp
// Notification service
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<DefaultEmailFormatter>();

// SMS formatters (keyed by notification type)
builder.Services.AddKeyedScoped<ISmsFormatter, BookingConfirmationSmsFormatter>(typeof(BookingConfirmation));
builder.Services.AddKeyedScoped<ISmsFormatter, BookingCancellationSmsFormatter>(typeof(BookingCancellation));
builder.Services.AddKeyedScoped<ISmsFormatter, WaitlistJoinedSmsFormatter>(typeof(WaitlistJoined));
builder.Services.AddKeyedScoped<ISmsFormatter, WaitlistOfferAvailableSmsFormatter>(typeof(WaitlistOfferAvailable));
builder.Services.AddKeyedScoped<ISmsFormatter, WaitlistOfferExpiredSmsFormatter>(typeof(WaitlistOfferExpired));
builder.Services.AddKeyedScoped<ISmsFormatter, WalkupConfirmationSmsFormatter>(typeof(WalkupConfirmation));
```

Add the necessary `using` statements:

```csharp
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Api.Features.Waitlist.Handlers;
```

Keep the existing `IEmailSender`, `ISmsSender`, `DatabaseSmsSender`, and `TelnyxSmsSender` registrations — those are still used by the `DeliverSms` and `DeliverEmail` handlers.

- [ ] **Step 2: Verify build**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 3: Run full test suite**

Run: `dotnet test teeforce.slnx --no-restore -v n`
Expected: All tests PASS

- [ ] **Step 4: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 5: Commit**

```bash
git add src/backend/Teeforce.Api/Program.cs
git commit -m "feat: register notification formatters and wire DI for outbox dispatch"
```

---

### Task 10: Verify Full Build + Run Dev Environment

- [ ] **Step 1: Run full test suite**

Run: `dotnet test teeforce.slnx --no-restore -v n`
Expected: All tests PASS

- [ ] **Step 2: Run dotnet format**

Run: `dotnet format teeforce.slnx`
Expected: No changes (or minimal formatting fixes)

- [ ] **Step 3: Run make dev**

Run: `make dev`
Expected: API starts on :5221, Web on :3000, no startup errors. Check logs for any DI resolution failures.

- [ ] **Step 4: Final commit if any format changes**

```bash
git add -A
git commit -m "chore: format"
```
