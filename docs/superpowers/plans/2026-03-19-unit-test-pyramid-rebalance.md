# Unit Test Pyramid Rebalance

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add unit tests for all untested validators, message handlers, and the Golfer aggregate to rebalance the testing pyramid from 75% integration / 25% unit to roughly 50/50.

**Architecture:** All new tests are pure unit tests — no `TestWebApplicationFactory`, no SQL Server, no HTTP. Handlers are static methods tested by calling `Handle()` directly with NSubstitute stubs. Validators are tested by calling `Validate()` directly.

**Tech Stack:** xUnit, NSubstitute, FluentValidation, .NET 10

**Key references:**
- Existing domain test pattern: `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/TeeTimeRequestServiceTests.cs`
- Existing validator-style test: `tests/Shadowbrook.Api.Tests/PhoneNormalizerTests.cs` (no factory)
- Handler source: `src/backend/Shadowbrook.Api/Features/` (all static `Handle()` methods)
- Validator source: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/` (inline with endpoints)

**NSubstitute pattern for handler tests:**
- Stub repository methods with `.Returns()` to control what data the handler sees
- Use real domain objects (aggregates, entities) — don't substitute those, they have behavior
- For SMS verification, use `Received()` / `DidNotReceive()` on the `ITextMessageService` substitute

---

### Task 1: Project Setup

Add NSubstitute and grant test project access to domain internals.

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`
- Modify: `src/backend/Shadowbrook.Domain/Properties/AssemblyInfo.cs`

- [ ] **Step 1: Add NSubstitute NuGet package**

Run: `dotnet add tests/Shadowbrook.Api.Tests package NSubstitute`

- [ ] **Step 2: Add InternalsVisibleTo for Api.Tests**

Handler tests need access to `internal` members like `GolferWaitlistEntry` constructor and `TeeTimeRequest.Fill()`.

Edit `src/backend/Shadowbrook.Domain/Properties/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Shadowbrook.Domain.Tests")]
[assembly: InternalsVisibleTo("Shadowbrook.Api")]
[assembly: InternalsVisibleTo("Shadowbrook.Api.Tests")]
```

- [ ] **Step 3: Verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```
git add tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj src/backend/Shadowbrook.Domain/Properties/AssemblyInfo.cs
git commit -m "chore: add NSubstitute and grant InternalsVisibleTo for Api.Tests"
```

---

### Task 2: Validator Unit Tests

Test all 4 FluentValidation validators directly — no HTTP, no factory. These don't need NSubstitute since validators are self-contained.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Validators/VerifyCodeRequestValidatorTests.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/JoinWaitlistRequestValidatorTests.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/AddGolferToWaitlistRequestValidatorTests.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/CreateWalkUpWaitlistRequestRequestValidatorTests.cs`

- [ ] **Step 1: Write VerifyCodeRequestValidator tests**

Source: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpJoinEndpoints.cs:117-126`
Rules: NotEmpty, Length(4), all digits.

```csharp
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class VerifyCodeRequestValidatorTests
{
    private readonly VerifyCodeRequestValidator validator = new();

    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    [InlineData("9999")]
    public void ValidCode_Passes(string code) =>
        Assert.True(validator.Validate(new VerifyCodeRequest(code)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_Fails(string code) =>
        Assert.False(validator.Validate(new VerifyCodeRequest(code)).IsValid);

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("123")]
    [InlineData("12ab")]
    public void InvalidFormat_Fails(string code) =>
        Assert.False(validator.Validate(new VerifyCodeRequest(code)).IsValid);
}
```

- [ ] **Step 2: Write JoinWaitlistRequestValidator tests**

Source: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpJoinEndpoints.cs:139-152`
Rules: FirstName not empty, LastName not empty, Phone valid.

```csharp
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class JoinWaitlistRequestValidatorTests
{
    private readonly JoinWaitlistRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", "555-123-4567")).IsValid);

    [Fact]
    public void EmptyFirstName_Fails()
    {
        var result = validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "", "Smith", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void EmptyLastName_Fails()
    {
        var result = validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "", "555-123-4567"));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "LastName");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("abcdefghij")]
    public void InvalidPhone_Fails(string phone)
    {
        var result = validator.Validate(new JoinWaitlistRequest(Guid.NewGuid(), "John", "Smith", phone));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Phone");
    }
}
```

- [ ] **Step 3: Write AddGolferToWaitlistRequestValidator tests**

Source: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpWaitlistEndpoints.cs:194-211`
Rules: FirstName not empty, LastName not empty, Phone valid, GroupSize 1-4 (when provided).

```csharp
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class AddGolferToWaitlistRequestValidatorTests
{
    private readonly AddGolferToWaitlistRequestValidator validator = new();

    [Fact]
    public void ValidRequest_NoGroupSize_Passes() =>
        Assert.True(validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", null)).IsValid);

    [Fact]
    public void ValidRequest_WithGroupSize_Passes() =>
        Assert.True(validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", 3)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidGroupSize_Fails(int groupSize)
    {
        var result = validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "555-123-4567", groupSize));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "GroupSize");
    }

    [Fact]
    public void EmptyFirstName_Fails() =>
        Assert.False(validator.Validate(new AddGolferToWaitlistRequest("", "Smith", "555-123-4567", null)).IsValid);

    [Fact]
    public void EmptyLastName_Fails() =>
        Assert.False(validator.Validate(new AddGolferToWaitlistRequest("John", "", "555-123-4567", null)).IsValid);

    [Fact]
    public void InvalidPhone_Fails() =>
        Assert.False(validator.Validate(new AddGolferToWaitlistRequest("John", "Smith", "123", null)).IsValid);
}
```

- [ ] **Step 4: Write CreateWalkUpWaitlistRequestRequestValidator tests**

Source: `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpWaitlistEndpoints.cs:223-241`
Rules: Date not empty + yyyy-MM-dd format, TeeTime not empty + HH:mm format, GolfersNeeded 1-4.

```csharp
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateWalkUpWaitlistRequestRequestValidatorTests
{
    private readonly CreateWalkUpWaitlistRequestRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00", 2)).IsValid);

    [Fact]
    public void ValidRequest_WithSeconds_Passes() =>
        Assert.True(validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00:00", 2)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("06/15/2026")]
    [InlineData("2026-13-01")]
    [InlineData("not-a-date")]
    public void InvalidDate_Fails(string date)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest(date, "09:00", 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Date");
    }

    [Theory]
    [InlineData("")]
    [InlineData("9am")]
    [InlineData("25:00")]
    public void InvalidTeeTime_Fails(string teeTime)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", teeTime, 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TeeTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidGolfersNeeded_Fails(int golfersNeeded)
    {
        var result = validator.Validate(new CreateWalkUpWaitlistRequestRequest("2026-06-15", "09:00", golfersNeeded));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "GolfersNeeded");
    }
}
```

- [ ] **Step 5: Run all validator tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~Validators" -v minimal`
Expected: All pass

- [ ] **Step 6: Commit**

```
git add tests/Shadowbrook.Api.Tests/Validators/
git commit -m "test: add unit tests for all FluentValidation validators"
```

---

### Task 3: Golfer Aggregate Unit Tests

The `Golfer.Create()` factory has behavior (name trimming, GUID generation) but no tests.

**Files:**
- Create: `tests/Shadowbrook.Domain.Tests/GolferAggregate/GolferTests.cs`

- [ ] **Step 1: Write tests**

Source: `src/backend/Shadowbrook.Domain/GolferAggregate/Golfer.cs`

```csharp
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Domain.Tests.GolferAggregate;

public class GolferTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");

        Assert.Equal("+15551234567", golfer.Phone);
        Assert.Equal("Jane", golfer.FirstName);
        Assert.Equal("Smith", golfer.LastName);
        Assert.NotEqual(Guid.Empty, golfer.Id);
        Assert.NotEqual(default, golfer.CreatedAt);
        Assert.NotEqual(default, golfer.UpdatedAt);
    }

    [Fact]
    public void Create_TrimsFirstName()
    {
        var golfer = Golfer.Create("+15551234567", "  Jane  ", "Smith");
        Assert.Equal("Jane", golfer.FirstName);
    }

    [Fact]
    public void Create_TrimsLastName()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "  Smith  ");
        Assert.Equal("Smith", golfer.LastName);
    }

    [Fact]
    public void FullName_CombinesFirstAndLast()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        Assert.Equal("Jane Smith", golfer.FullName);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Domain.Tests --filter "FullyQualifiedName~GolferTests" -v minimal`
Expected: All pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Domain.Tests/GolferAggregate/
git commit -m "test: add unit tests for Golfer aggregate"
```

---

### Task 4: Handler Unit Tests — WaitlistOfferAcceptedFillHandler

The most complex handler: 4 distinct return paths.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferAcceptedFillHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferAcceptedFillHandler.cs`

Branches:
1. Entry not found → returns null
2. Request not found → returns `TeeTimeSlotFillFailed`
3. `request.Fill()` fails → returns `TeeTimeSlotFillFailed`
4. Success → returns `TeeTimeSlotFilled`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedFillHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();

    [Fact]
    public async Task Handle_EntryNotFound_ReturnsNull()
    {
        var evt = MakeEvent();

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_RequestNotFound_ReturnsFillFailed()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var failed = Assert.IsType<TeeTimeSlotFillFailed>(result);
        Assert.Equal(evt.TeeTimeRequestId, failed.TeeTimeRequestId);
        Assert.Equal("Tee time request not found.", failed.Reason);
    }

    [Fact]
    public async Task Handle_FillFails_ReturnsFillFailed()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        // Create a request that's already fulfilled so Fill() returns failure
        var request = await CreateRequest();
        request.Fill(Guid.NewGuid(), request.GolfersNeeded, Guid.CreateVersion7()); // fills all slots
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var failed = Assert.IsType<TeeTimeSlotFillFailed>(result);
        Assert.Contains("already been filled", failed.Reason);
    }

    [Fact]
    public async Task Handle_Success_ReturnsSlotFilled()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), groupSize: 1);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var request = await CreateRequest(golfersNeeded: 2);
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var evt = MakeEvent(teeTimeRequestId: request.Id, entryId: entry.Id);

        var result = await WaitlistOfferAcceptedFillHandler.Handle(evt, requestRepo, entryRepo);

        var filled = Assert.IsType<TeeTimeSlotFilled>(result);
        Assert.Equal(request.Id, filled.TeeTimeRequestId);
        Assert.Equal(evt.BookingId, filled.BookingId);
        Assert.Equal(evt.GolferId, filled.GolferId);
    }

    private static WaitlistOfferAccepted MakeEvent(
        Guid? teeTimeRequestId = null,
        Guid? entryId = null) => new()
    {
        WaitlistOfferId = Guid.NewGuid(),
        BookingId = Guid.CreateVersion7(),
        TeeTimeRequestId = teeTimeRequestId ?? Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        GolferId = Guid.NewGuid()
    };

    private static async Task<TeeTimeRequest> CreateRequest(int golfersNeeded = 2)
    {
        var repo = Substitute.For<ITeeTimeRequestRepository>();
        repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>()).Returns(false);
        return await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 6, 15), new TimeOnly(9, 0), golfersNeeded, repo);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~WaitlistOfferAcceptedFillHandlerTests" -v minimal`
Expected: All 4 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferAcceptedFillHandlerTests.cs
git commit -m "test: add unit tests for WaitlistOfferAcceptedFillHandler"
```

---

### Task 5: Handler Unit Tests — TeeTimeSlotFilledBookingHandler

Creates a Booking from event chain data. 4 null-guard paths (request, golfer, offer, entry) + 1 success path.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFilledBookingHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/Bookings/TeeTimeSlotFilledBookingHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeSlotFilledBookingHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();

    [Fact]
    public async Task Handle_RequestNotFound_DoesNothing()
    {
        var evt = MakeEvent();
        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var evt = MakeEvent(teeTimeRequestId: request.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);
        var evt = MakeEvent(teeTimeRequestId: request.Id, golferId: golfer.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_EntryNotFound_DoesNothing()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var offer = WaitlistOffer.Create(request.Id, Guid.NewGuid());
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);
        // entryRepo.GetByIdAsync not set up — returns null

        var evt = MakeEvent(teeTimeRequestId: request.Id, bookingId: offer.BookingId, golferId: golfer.Id);

        await Handle(evt);
        bookingRepo.DidNotReceive().Add(Arg.Any<Booking>());
    }

    [Fact]
    public async Task Handle_Success_CreatesBooking()
    {
        var request = await CreateRequest();
        requestRepo.GetByIdAsync(request.Id).Returns(request);

        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golfer.Id, 2);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var offer = WaitlistOffer.Create(request.Id, entry.Id);
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);

        var evt = MakeEvent(
            teeTimeRequestId: request.Id,
            bookingId: offer.BookingId,
            golferId: golfer.Id);

        await Handle(evt);

        bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.Id == offer.BookingId &&
            b.CourseId == request.CourseId &&
            b.GolferId == golfer.Id &&
            b.GolferName == "Jane Smith" &&
            b.PlayerCount == 2));
    }

    private Task Handle(TeeTimeSlotFilled evt) =>
        TeeTimeSlotFilledBookingHandler.Handle(evt, requestRepo, golferRepo, offerRepo, entryRepo, bookingRepo);

    private static TeeTimeSlotFilled MakeEvent(
        Guid? teeTimeRequestId = null,
        Guid? bookingId = null,
        Guid? golferId = null) => new()
    {
        TeeTimeRequestId = teeTimeRequestId ?? Guid.NewGuid(),
        BookingId = bookingId ?? Guid.CreateVersion7(),
        GolferId = golferId ?? Guid.NewGuid()
    };

    private static async Task<TeeTimeRequest> CreateRequest()
    {
        var repo = Substitute.For<ITeeTimeRequestRepository>();
        repo.ExistsAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>()).Returns(false);
        return await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 6, 15), new TimeOnly(9, 0), 2, repo);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeSlotFilledBookingHandlerTests" -v minimal`
Expected: All 5 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFilledBookingHandlerTests.cs
git commit -m "test: add unit tests for TeeTimeSlotFilledBookingHandler"
```

---

### Task 6: Handler Unit Tests — BookingCreatedRemoveFromWaitlistHandler

Removes a golfer from the waitlist after booking. 2 null-guard paths + 1 success path.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/BookingCreatedRemoveFromWaitlistHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/Bookings/BookingCreatedRemoveFromWaitlistHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingCreatedRemoveFromWaitlistHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var evt = new BookingCreated { BookingId = Guid.CreateVersion7(), GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, offerRepo, entryRepo);
        // No exception — early return
    }

    [Fact]
    public async Task Handle_EntryNotFound_DoesNothing()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);

        var evt = new BookingCreated { BookingId = offer.BookingId, GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, offerRepo, entryRepo);
        // No exception — early return
    }

    [Fact]
    public async Task Handle_Success_RemovesEntry()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(entry.RemovedAt);

        var offer = WaitlistOffer.Create(Guid.NewGuid(), entry.Id);
        offerRepo.GetByBookingIdAsync(offer.BookingId).Returns(offer);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = new BookingCreated { BookingId = offer.BookingId, GolferId = Guid.NewGuid(), CourseId = Guid.NewGuid() };
        await BookingCreatedRemoveFromWaitlistHandler.Handle(evt, offerRepo, entryRepo);

        Assert.NotNull(entry.RemovedAt);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~BookingCreatedRemoveFromWaitlistHandlerTests" -v minimal`
Expected: All 3 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/BookingCreatedRemoveFromWaitlistHandlerTests.cs
git commit -m "test: add unit tests for BookingCreatedRemoveFromWaitlistHandler"
```

---

### Task 7: Handler Unit Tests — TeeTimeRequestFulfilledHandler

Rejects all pending offers when a tee time is fully filled.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeRequestFulfilledHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/Bookings/TeeTimeRequestFulfilledHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeRequestFulfilledHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();

    [Fact]
    public async Task Handle_NoPendingOffers_DoesNothing()
    {
        var requestId = Guid.NewGuid();
        offerRepo.GetPendingByRequestAsync(requestId).Returns(new List<WaitlistOffer>());

        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };
        await TeeTimeRequestFulfilledHandler.Handle(evt, offerRepo);
        // No exception
    }

    [Fact]
    public async Task Handle_PendingOffers_RejectsAll()
    {
        var requestId = Guid.NewGuid();
        var offer1 = WaitlistOffer.Create(requestId, Guid.NewGuid());
        var offer2 = WaitlistOffer.Create(requestId, Guid.NewGuid());
        offerRepo.GetPendingByRequestAsync(requestId).Returns(new List<WaitlistOffer> { offer1, offer2 });

        var evt = new TeeTimeRequestFulfilled { TeeTimeRequestId = requestId };
        await TeeTimeRequestFulfilledHandler.Handle(evt, offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer1.Status);
        Assert.Equal(OfferStatus.Rejected, offer2.Status);
        Assert.Equal("Tee time has been filled.", offer1.RejectionReason);
        Assert.Equal("Tee time has been filled.", offer2.RejectionReason);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeRequestFulfilledHandlerTests" -v minimal`
Expected: All 2 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/TeeTimeRequestFulfilledHandlerTests.cs
git commit -m "test: add unit tests for TeeTimeRequestFulfilledHandler"
```

---

### Task 8: Handler Unit Tests — TeeTimeSlotFillFailedHandler

Rejects the offer when a fill attempt fails.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFillFailedHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/Bookings/TeeTimeSlotFillFailedHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

public class TeeTimeSlotFillFailedHandlerTests
{
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();

    [Fact]
    public async Task Handle_OfferNotFound_DoesNothing()
    {
        var evt = new TeeTimeSlotFillFailed { TeeTimeRequestId = Guid.NewGuid(), OfferId = Guid.NewGuid(), Reason = "test" };
        await TeeTimeSlotFillFailedHandler.Handle(evt, offerRepo);
        // No exception
    }

    [Fact]
    public async Task Handle_OfferFound_RejectsWithReason()
    {
        var offer = WaitlistOffer.Create(Guid.NewGuid(), Guid.NewGuid());
        offerRepo.GetByIdAsync(offer.Id).Returns(offer);

        var evt = new TeeTimeSlotFillFailed { TeeTimeRequestId = Guid.NewGuid(), OfferId = offer.Id, Reason = "Group too large" };
        await TeeTimeSlotFillFailedHandler.Handle(evt, offerRepo);

        Assert.Equal(OfferStatus.Rejected, offer.Status);
        Assert.Equal("Group too large", offer.RejectionReason);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~TeeTimeSlotFillFailedHandlerTests" -v minimal`
Expected: All 2 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/TeeTimeSlotFillFailedHandlerTests.cs
git commit -m "test: add unit tests for TeeTimeSlotFillFailedHandler"
```

---

### Task 9: Handler Unit Tests — WaitlistOfferAcceptedSmsHandler

Sends SMS when an offer is accepted. Tests null guards and message sending.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferAcceptedSmsHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferAcceptedSmsHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferAcceptedSmsHandlerTests
{
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_EntryNotFound_NoSms()
    {
        var evt = MakeEvent();
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_NoSms()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golfer.Id);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferAcceptedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);

        await sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("processing")),
            Arg.Any<CancellationToken>());
    }

    private static WaitlistOfferAccepted MakeEvent(Guid? entryId = null) => new()
    {
        WaitlistOfferId = Guid.NewGuid(),
        BookingId = Guid.CreateVersion7(),
        TeeTimeRequestId = Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        GolferId = Guid.NewGuid()
    };
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~WaitlistOfferAcceptedSmsHandlerTests" -v minimal`
Expected: All 3 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferAcceptedSmsHandlerTests.cs
git commit -m "test: add unit tests for WaitlistOfferAcceptedSmsHandler"
```

---

### Task 10: Handler Unit Tests — WaitlistOfferRejectedSmsHandler

Sends SMS when an offer is rejected, unless the golfer is already removed.

**Files:**
- Create: `tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferRejectedSmsHandlerTests.cs`

Source: `src/backend/Shadowbrook.Api/Features/WaitlistOffers/WaitlistOfferRejectedSmsHandler.cs`

- [ ] **Step 1: Write tests**

```csharp
using NSubstitute;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class WaitlistOfferRejectedSmsHandlerTests
{
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();

    [Fact]
    public async Task Handle_EntryNotFound_NoSms()
    {
        var evt = MakeEvent();
        await WaitlistOfferRejectedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntryAlreadyRemoved_NoSms()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entry.Remove(); // Sets RemovedAt
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GolferNotFound_NoSms()
    {
        var entry = new GolferWaitlistEntry(Guid.NewGuid(), Guid.NewGuid());
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);
        await sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = new GolferWaitlistEntry(Guid.NewGuid(), golfer.Id);
        entryRepo.GetByIdAsync(entry.Id).Returns(entry);

        var evt = MakeEvent(entryId: entry.Id);
        await WaitlistOfferRejectedSmsHandler.Handle(evt, entryRepo, golferRepo, sms, CancellationToken.None);

        await sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("no longer available")),
            Arg.Any<CancellationToken>());
    }

    private static WaitlistOfferRejected MakeEvent(Guid? entryId = null) => new()
    {
        WaitlistOfferId = Guid.NewGuid(),
        GolferWaitlistEntryId = entryId ?? Guid.NewGuid(),
        Reason = "Tee time has been filled."
    };
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~WaitlistOfferRejectedSmsHandlerTests" -v minimal`
Expected: All 4 pass

- [ ] **Step 3: Commit**

```
git add tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferRejectedSmsHandlerTests.cs
git commit -m "test: add unit tests for WaitlistOfferRejectedSmsHandler"
```

---

### Task 11: Skipped Handlers (DbContext-dependent)

**Decision:** Skip these handlers for pure unit testing. They depend on `ApplicationDbContext` directly for queries not behind repository interfaces. They're already covered by integration tests.

- `BookingCreatedConfirmationSmsHandler`
- `GolferJoinedWaitlistSmsHandler`
- `TeeTimeRequestAddedNotifyHandler`
- `WaitlistOfferRejectedNextOfferHandler`

If these handlers are refactored in the future to use repository interfaces for their queries, unit tests should be added at that time.

---

### Task 12: Verify Full Suite and Final Numbers

- [ ] **Step 1: Run all tests**

Run: `dotnet test shadowbrook.slnx -v minimal`
Expected: All tests pass (existing + new)

- [ ] **Step 2: Count new unit tests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~Validators|FullyQualifiedName~Handlers" -v minimal`

Expected: ~32 new unit tests across validators and handlers.

New pyramid (approximate):
- Unit: ~87 tests (~53%)
- Integration: ~151 tests (~47% — unchanged, can be thinned later)

- [ ] **Step 3: Commit (if any fixes were needed)**

```
git add -A
git commit -m "test: verify full suite passes after unit test additions"
```
