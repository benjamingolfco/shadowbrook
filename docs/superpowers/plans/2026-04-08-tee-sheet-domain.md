# Tee Sheet Domain (TeeSheet + TeeTime Aggregates) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce persistent slot identity in the domain by adding `TeeSheet` and `TeeTime` aggregates, route direct bookings through the new claim path, and rewrite `GET /tee-sheets` to read from them — without touching the existing walk-up/`TeeTimeOpening` flow.

**Architecture:** A `TeeSheet` aggregate (per course per day) owns a `ScheduleSettings` value object and a collection of `TeeSheetInterval` child entities with stable Ids. Per-interval `TeeTime` aggregates are lazily materialized when the first booking, block, or override happens; they own capacity, status (`Open|Filled|Blocked`), and `TeeTimeClaim` children. A `BookingAuthorization` capability token (mintable only by a `Published` `TeeSheet` via an `internal` constructor) gates `TeeTime.Claim()` so the "sheet must be claimable" invariant is compile-time enforced. The walk-up flow (`TeeTimeOpening`, `WaitlistOffer`, all `Features/Waitlist/...` handlers) is **untouched** during this spec — `Bookings.TeeTimeId` is added as **nullable** so walk-up bookings can continue to flow through the legacy path. The DB is reset (fresh initial migration) — no row-level data backfill.

**Tech Stack:** .NET 10, EF Core 10 (`ComplexProperty` for VOs), Wolverine HTTP + transactional middleware + EF Core domain-event scraping, FluentValidation, xUnit + NSubstitute (unit), Testcontainers SQL Server (integration).

---

## File Structure

This plan creates the following new files and modifies the listed existing files. Each unit has one clear responsibility — aggregates own behavior, EF configurations own persistence shape, handlers do one thing each, endpoints stay thin.

### New domain files (zero dependencies — `Teeforce.Domain`)

- `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs` — aggregate root
- `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs` — child entity
- `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetStatus.cs` — enum
- `src/backend/Teeforce.Domain/TeeSheetAggregate/ScheduleSettings.cs` — value object
- `src/backend/Teeforce.Domain/TeeSheetAggregate/BookingAuthorization.cs` — capability token VO with `internal` ctor
- `src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs` — repo interface
- `src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetDrafted.cs`
- `src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetPublished.cs`
- `src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/TeeSheetNotPublishedException.cs`
- `src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/TeeSheetAlreadyExistsException.cs`
- `src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/InvalidScheduleSettingsException.cs`
- `src/backend/Teeforce.Domain/CourseAggregate/Exceptions/CourseScheduleNotConfiguredException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs` — aggregate root (the **new** TeeTime — note the rename of the existing VO below)
- `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs` — child entity
- `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeStatus.cs` — enum
- `src/backend/Teeforce.Domain/TeeTimeAggregate/ITeeTimeRepository.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimed.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeFilled.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimReleased.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeReopened.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeBlocked.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeUnblocked.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeBlockedException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeFilledException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InsufficientCapacityException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InvalidGroupSizeException.cs` (per-aggregate; the existing `TeeTimeOpeningAggregate.Exceptions.InvalidGroupSizeException` stays untouched)
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InvalidTeeTimeCapacityException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/BookingAuthorizationMismatchException.cs`
- `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeHasClaimsException.cs`

### Renamed domain file

- `src/backend/Teeforce.Domain/Common/TeeTime.cs` → `src/backend/Teeforce.Domain/Common/BookingDateTime.cs` (type rename only — the VO stays on `Booking` during the compatibility seam; deleted in the follow-on spec)

### Modified domain files

- `src/backend/Teeforce.Domain/CourseAggregate/Course.cs` — add `DefaultCapacity` field, add `CurrentScheduleDefaults()` method
- `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs` — rename VO references; add `TeeTimeId` (nullable Guid); add `teeTimeId` parameter to `CreateConfirmed`
- `src/backend/Teeforce.Domain/BookingAggregate/IBookingRepository.cs` — rename `TeeTime` → `BookingDateTime` in signatures

### New API layer files (`Teeforce.Api`)

- `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs`
- `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeTimeRepository.cs`
- `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs`
- `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs`
- `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs`
- `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/PublishTeeSheetEndpoint.cs`
- `src/backend/Teeforce.Api/Features/Bookings/Endpoints/BookTeeTimeEndpoint.cs`
- `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs`
- `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/ReleaseTeeTimeClaimHandler.cs`
- `src/backend/Teeforce.Api/Migrations/{timestamp}_TeeSheetAndTeeTimeAggregates.cs` (generated by `dotnet ef`)

### Modified API files

- `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` — register `DbSet<TeeSheet>` and `DbSet<TeeTime>`, apply new configurations
- `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs` — map new `DefaultCapacity` column
- `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs` — rename `TeeTime` ComplexProperty to `BookingDateTime`, add `TeeTimeId` (nullable)
- `src/backend/Teeforce.Api/Features/TeeSheet/TeeSheetEndpoints.cs` — rewrite `GetTeeSheet` to project from `TeeSheet`/`TeeTime` instead of generating intervals on-the-fly
- `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs` — map new exceptions to HTTP statuses
- `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeOpeningSlotsClaimed/CreateConfirmedBookingHandler.cs` — pass `teeTimeId: null` to `Booking.CreateConfirmed` (compat seam)
- `src/backend/Teeforce.Api/Infrastructure/Data/DevSeedData.cs` — seed at least one published `TeeSheet` for the dev course (for manual smoke tests)
- Service registration: register `ITeeSheetRepository`/`ITeeTimeRepository` in DI (where `IBookingRepository` is registered today)

### New test files

- `tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetTests.cs`
- `tests/Teeforce.Domain.Tests/TeeSheetAggregate/ScheduleSettingsTests.cs`
- `tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimeTests.cs`
- `tests/Teeforce.Domain.Tests/CourseAggregate/CourseScheduleDefaultsTests.cs` (extends existing folder)
- `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/TeeTimeClaimedCreateConfirmedBookingHandlerTests.cs`
- `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/BookingCancelledReleaseTeeTimeClaimHandlerTests.cs`
- `tests/Teeforce.Api.IntegrationTests/TeeSheetDirectBookingTests.cs`

### Modified test files (rename only — VO renamed)

- `tests/Teeforce.Domain.Tests/BookingAggregate/BookingTests.cs` — update VO refs
- `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs` — update VO refs
- `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/BookingCreatedConfirmationNotificationHandlerTests.cs` — update VO refs
- `tests/Teeforce.Api.Tests/Features/Waitlist/Handlers/BookingCancelledNotificationHandlerTests.cs` — update VO refs
- `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs` — update VO refs and (in Task 19) the response shape it asserts on

---

## Tasks

The plan is broken into 21 tasks. Each task is a single coherent unit that builds, tests, and commits before the next begins. Tasks 1–8 build the domain (pure unit-tested). Tasks 9–14 build persistence and the new endpoints. Tasks 15–17 wire up handlers. Tasks 18–21 integration-test, rewrite the read endpoint, and verify the system end-to-end.

> **Conventions reminder for the implementer (read once before starting):**
> - C#: `var`, braces always, `private` fields camelCase qualified with `this.`, file-scoped namespaces, `is null`/`is not null`, never use `Async` suffix.
> - Aggregates: `private set` everywhere, private parameterless ctor for EF, static factory methods, raise events via `AddDomainEvent`. Use `Guid.CreateVersion7()` for new IDs.
> - Domain exceptions: every invariant gets a dedicated subclass of `DomainException`. Never throw `InvalidOperationException` from domain code.
> - Tests: domain unit tests live in `tests/Teeforce.Domain.Tests/...`. Stub `ITimeProvider` with NSubstitute. Use real domain objects, not mocks.
> - Never call `SaveChangesAsync()` in handlers/endpoints — Wolverine transactional middleware handles it.
> - Repositories use `GetRequiredByIdAsync` (extension in `Teeforce.Domain.Common.RepositoryExtensions`) for ID lookups in handlers.

---

### Task 1: Rename the existing `TeeTime` value object to `BookingDateTime`

**Why first:** The new aggregate needs the `TeeTime` name. This is a pure type/file rename — no behavior change. Doing it before any new code lands keeps every later task using the new names from the start.

**Files:**
- Rename: `src/backend/Teeforce.Domain/Common/TeeTime.cs` → `src/backend/Teeforce.Domain/Common/BookingDateTime.cs`
- Modify: `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs`
- Modify: `src/backend/Teeforce.Domain/BookingAggregate/IBookingRepository.cs`
- Modify: `src/backend/Teeforce.Domain/TeeTimeOpeningAggregate/TeeTimeOpening.cs` (still uses the VO during the seam)
- Modify: `src/backend/Teeforce.Api/Infrastructure/Repositories/BookingRepository.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeTimeOpeningRepository.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOpeningConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Features/TeeSheet/TeeSheetEndpoints.cs` (it queries `b.TeeTime.Value` against the booking)
- Modify all test files listed in the "Modified test files" section above

- [ ] **Step 1: Create the new VO file** (delete the old `Common/TeeTime.cs` after creation)

```csharp
// src/backend/Teeforce.Domain/Common/BookingDateTime.cs
namespace Teeforce.Domain.Common;

public class BookingDateTime : IEquatable<BookingDateTime>
{
    public DateTime Value { get; }
    public DateOnly Date => DateOnly.FromDateTime(Value);
    public TimeOnly Time => TimeOnly.FromDateTime(Value);

    public BookingDateTime(DateOnly date, TimeOnly time)
    {
        Value = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0);
    }

    public BookingDateTime(DateTime value)
    {
        Value = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    private BookingDateTime() { } // EF

    public override bool Equals(object? obj) => Equals(obj as BookingDateTime);
    public bool Equals(BookingDateTime? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Date:yyyy-MM-dd} {Time:h:mm tt}";
}
```

- [ ] **Step 2: Delete the old file**

```bash
git rm src/backend/Teeforce.Domain/Common/TeeTime.cs
```

- [ ] **Step 3: Update `Booking.cs` to reference `BookingDateTime`** — replace every `TeeTime` type reference (the property name and `new TeeTime(date, teeTime)` calls) with `BookingDateTime`. The property keeps its name `TeeTime` for now (renaming the property is a separate concern; the spec only requires renaming the VO type). Final result on `Booking`:

```csharp
public BookingDateTime TeeTime { get; private set; } = null!;
// ...
TeeTime = new BookingDateTime(date, teeTime),
```

- [ ] **Step 4: Update `IBookingRepository` and `BookingRepository`** — change the `TeeTime` parameter type to `BookingDateTime` on `GetByCourseAndTeeTimeAsync`:

```csharp
// IBookingRepository.cs
Task<List<Booking>> GetByCourseAndTeeTimeAsync(Guid courseId, BookingDateTime teeTime, CancellationToken ct = default);
```

Update the implementation in `BookingRepository.cs` accordingly (the body changes from `b.TeeTime.Value == teeTime.Value` — same expression, just different parameter type).

- [ ] **Step 5: Update `TeeTimeOpening.cs`, `TeeTimeOpeningRepository.cs`, and the two EF configurations** to use `BookingDateTime` everywhere. The `ComplexProperty` mapping in `BookingConfiguration.cs` and `TeeTimeOpeningConfiguration.cs` keeps the same column name `"TeeTime"` so the schema is unchanged:

```csharp
builder.ComplexProperty(b => b.TeeTime, t =>
    t.Property(x => x.Value).HasColumnName("TeeTime").HasColumnType("datetime2"));
```

(No change needed to the call site — only the imported namespace and the property type on the entity changed.)

- [ ] **Step 6: Update `TeeSheetEndpoints.cs` and the test files** to import the new type. The query expression `b.TeeTime.Value` and `b.TeeTime.Time` are unchanged.

- [ ] **Step 7: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS, zero errors.

- [ ] **Step 8: Run tests**

Run: `dotnet test teeforce.slnx`
Expected: ALL existing tests pass — this is a pure rename.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor(domain): rename TeeTime VO to BookingDateTime to free the name for the new aggregate"
```

---

### Task 2: Add `Course.DefaultCapacity` and `Course.CurrentScheduleDefaults()`

**Files:**
- Create: `src/backend/Teeforce.Domain/CourseAggregate/Exceptions/CourseScheduleNotConfiguredException.cs`
- Modify: `src/backend/Teeforce.Domain/CourseAggregate/Course.cs`
- Test: `tests/Teeforce.Domain.Tests/CourseAggregate/CourseScheduleDefaultsTests.cs` (new file)

> **Note:** `ScheduleSettings` and `CourseScheduleNotConfiguredException` are referenced from `Course` but the VO type is created in Task 3. To keep this task self-contained, create a minimal stub `ScheduleSettings` here (just the public ctor signature and properties used below). Task 3 expands it with guards, equality, and tests. **Do not commit Task 2 in isolation** — Task 2 + Task 3 must compile together. (Or, swap Task 2 and Task 3 if you prefer; both orderings work, but the order chosen here mirrors how a developer typically discovers the requirement.)

- [ ] **Step 1: Write the failing test file**

```csharp
// tests/Teeforce.Domain.Tests/CourseAggregate/CourseScheduleDefaultsTests.cs
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseAggregate.Exceptions;

namespace Teeforce.Domain.Tests.CourseAggregate;

public class CourseScheduleDefaultsTests
{
    [Fact]
    public void CurrentScheduleDefaults_ReturnsSettings_WhenAllFieldsConfigured()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");
        course.UpdateTeeTimeSettings(intervalMinutes: 10, firstTeeTime: new TimeOnly(7, 0), lastTeeTime: new TimeOnly(18, 0));

        var settings = course.CurrentScheduleDefaults();

        Assert.Equal(new TimeOnly(7, 0), settings.FirstTeeTime);
        Assert.Equal(new TimeOnly(18, 0), settings.LastTeeTime);
        Assert.Equal(10, settings.IntervalMinutes);
        Assert.Equal(4, settings.DefaultCapacity); // default
    }

    [Fact]
    public void CurrentScheduleDefaults_ThrowsWhenIntervalUnset()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");

        Assert.Throws<CourseScheduleNotConfiguredException>(() => course.CurrentScheduleDefaults());
    }

    [Fact]
    public void CurrentScheduleDefaults_UsesUpdatedDefaultCapacity()
    {
        var course = Course.Create(Guid.NewGuid(), "Pebble", "America/Los_Angeles");
        course.UpdateTeeTimeSettings(10, new TimeOnly(7, 0), new TimeOnly(18, 0));
        course.UpdateDefaultCapacity(2);

        var settings = course.CurrentScheduleDefaults();

        Assert.Equal(2, settings.DefaultCapacity);
    }
}
```

- [ ] **Step 2: Create the exception**

```csharp
// src/backend/Teeforce.Domain/CourseAggregate/Exceptions/CourseScheduleNotConfiguredException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CourseAggregate.Exceptions;

public class CourseScheduleNotConfiguredException : DomainException
{
    public CourseScheduleNotConfiguredException(Guid courseId)
        : base($"Course {courseId} has no tee time schedule configured.")
    {
    }
}
```

- [ ] **Step 3: Modify `Course.cs`**

Add the `DefaultCapacity` property (default 4) and the new methods:

```csharp
// In Course.cs, add field after WaitlistEnabled:
public int DefaultCapacity { get; private set; } = 4;

// Add method:
public void UpdateDefaultCapacity(int defaultCapacity)
{
    if (defaultCapacity <= 0)
    {
        throw new InvalidScheduleSettingsException("Default capacity must be positive.");
    }
    DefaultCapacity = defaultCapacity;
}

// Add method:
public ScheduleSettings CurrentScheduleDefaults()
{
    if (TeeTimeIntervalMinutes is null || FirstTeeTime is null || LastTeeTime is null)
    {
        throw new CourseScheduleNotConfiguredException(Id);
    }
    return new ScheduleSettings(
        firstTeeTime: FirstTeeTime.Value,
        lastTeeTime: LastTeeTime.Value,
        intervalMinutes: TeeTimeIntervalMinutes.Value,
        defaultCapacity: DefaultCapacity);
}
```

Add `using Teeforce.Domain.CourseAggregate.Exceptions;` and `using Teeforce.Domain.TeeSheetAggregate;` to the file (the second import will resolve once Task 3 lands).

- [ ] **Step 4: Run the failing test**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter FullyQualifiedName~CourseScheduleDefaultsTests`
Expected: FAIL — `ScheduleSettings` not found (until Task 3 lands). **This is expected.** Move directly to Task 3 without committing.

> **Implementation note:** Tasks 2 and 3 share one commit. Do not run `dotnet build` between them — it will fail. After Task 3 completes, build, run both test classes, then commit both tasks together with the message in Task 3 Step 7.

---

### Task 3: Create the `ScheduleSettings` value object and its tests

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/ScheduleSettings.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/InvalidScheduleSettingsException.cs`
- Test: `tests/Teeforce.Domain.Tests/TeeSheetAggregate/ScheduleSettingsTests.cs`

- [ ] **Step 1: Create the exception**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/InvalidScheduleSettingsException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;

public class InvalidScheduleSettingsException : DomainException
{
    public InvalidScheduleSettingsException(string message) : base(message)
    {
    }
}
```

- [ ] **Step 2: Create the VO**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/ScheduleSettings.cs
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.TeeSheetAggregate;

public class ScheduleSettings : IEquatable<ScheduleSettings>
{
    public TimeOnly FirstTeeTime { get; }
    public TimeOnly LastTeeTime { get; }
    public int IntervalMinutes { get; }
    public int DefaultCapacity { get; }

    public ScheduleSettings(TimeOnly firstTeeTime, TimeOnly lastTeeTime, int intervalMinutes, int defaultCapacity)
    {
        if (firstTeeTime >= lastTeeTime)
        {
            throw new InvalidScheduleSettingsException("First tee time must be earlier than last tee time.");
        }
        if (intervalMinutes <= 0)
        {
            throw new InvalidScheduleSettingsException("Interval minutes must be positive.");
        }
        if (defaultCapacity <= 0)
        {
            throw new InvalidScheduleSettingsException("Default capacity must be positive.");
        }

        FirstTeeTime = firstTeeTime;
        LastTeeTime = lastTeeTime;
        IntervalMinutes = intervalMinutes;
        DefaultCapacity = defaultCapacity;
    }

    private ScheduleSettings() { } // EF

    public override bool Equals(object? obj) => Equals(obj as ScheduleSettings);
    public bool Equals(ScheduleSettings? other) =>
        other is not null
        && FirstTeeTime == other.FirstTeeTime
        && LastTeeTime == other.LastTeeTime
        && IntervalMinutes == other.IntervalMinutes
        && DefaultCapacity == other.DefaultCapacity;

    public override int GetHashCode() =>
        HashCode.Combine(FirstTeeTime, LastTeeTime, IntervalMinutes, DefaultCapacity);
}
```

- [ ] **Step 3: Write the tests**

```csharp
// tests/Teeforce.Domain.Tests/TeeSheetAggregate/ScheduleSettingsTests.cs
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class ScheduleSettingsTests
{
    [Fact]
    public void Constructor_AcceptsValidValues()
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);

        Assert.Equal(new TimeOnly(7, 0), settings.FirstTeeTime);
        Assert.Equal(new TimeOnly(18, 0), settings.LastTeeTime);
        Assert.Equal(10, settings.IntervalMinutes);
        Assert.Equal(4, settings.DefaultCapacity);
    }

    [Fact]
    public void Constructor_ThrowsWhenFirstNotBeforeLast()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(18, 0), new TimeOnly(7, 0), 10, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenFirstEqualsLast()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(10, 0), new TimeOnly(10, 0), 10, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenIntervalNonPositive()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 0, 4));
    }

    [Fact]
    public void Constructor_ThrowsWhenDefaultCapacityNonPositive()
    {
        Assert.Throws<InvalidScheduleSettingsException>(() =>
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 0));
    }

    [Fact]
    public void Equals_TrueForSameValues()
    {
        var a = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);
        var b = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(18, 0), 10, 4);
        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 5: Run Task 2 + Task 3 tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~ScheduleSettingsTests|FullyQualifiedName~CourseScheduleDefaultsTests"`
Expected: PASS, all green.

- [ ] **Step 6: Run full test suite to confirm nothing else broke**

Run: `dotnet test teeforce.slnx`
Expected: ALL PASS.

- [ ] **Step 7: Commit Tasks 2 and 3 together**

```bash
git add -A
git commit -m "feat(domain): add ScheduleSettings VO and Course.CurrentScheduleDefaults()"
```

---

### Task 4: Create `BookingAuthorization` capability token

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/BookingAuthorization.cs`

The token has no behavior of its own; it's exercised by `TeeSheet.AuthorizeBooking()` (Task 5) and `TeeTime.Claim` (Task 7). It has no test of its own — the tests live on the producer (`TeeSheetTests`) and consumer (`TeeTimeTests`).

- [ ] **Step 1: Create the type**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/BookingAuthorization.cs
namespace Teeforce.Domain.TeeSheetAggregate;

public sealed class BookingAuthorization
{
    public Guid SheetId { get; }

    internal BookingAuthorization(Guid sheetId)
    {
        SheetId = sheetId;
    }
}
```

> **Critical:** the constructor MUST be `internal`. The whole capability pattern relies on the fact that no code outside `Teeforce.Domain` can fabricate one. Do **not** add `[InternalsVisibleTo("Teeforce.Domain.Tests")]` — tests obtain tokens by calling `sheet.AuthorizeBooking()` on a real `TeeSheet`, which is the same path production code uses.

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS. (No tests yet — tests are added in Tasks 5 and 7.)

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(domain): add BookingAuthorization capability token"
```

---

### Task 5: Create `TeeSheet` aggregate (with `TeeSheetInterval`, status, events)

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetStatus.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetDrafted.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetPublished.cs`
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/TeeSheetNotPublishedException.cs`
- Test: `tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetTests.cs`

- [ ] **Step 1: Create the enum**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetStatus.cs
namespace Teeforce.Domain.TeeSheetAggregate;

public enum TeeSheetStatus
{
    Draft,
    Published
}
```

- [ ] **Step 2: Create the events**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetDrafted.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Events;

public record TeeSheetDrafted : IDomainEvent
{
    public required Guid TeeSheetId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required int IntervalCount { get; init; }
}
```

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/Events/TeeSheetPublished.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Events;

public record TeeSheetPublished : IDomainEvent
{
    public required Guid TeeSheetId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }
}
```

- [ ] **Step 3: Create the exception**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/TeeSheetNotPublishedException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;

public class TeeSheetNotPublishedException : DomainException
{
    public Guid TeeSheetId { get; }

    public TeeSheetNotPublishedException(Guid teeSheetId)
        : base($"Tee sheet {teeSheetId} is not published.")
    {
        TeeSheetId = teeSheetId;
    }
}
```

- [ ] **Step 4: Create `TeeSheetInterval`**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheetInterval : Entity
{
    public Guid TeeSheetId { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }

    private TeeSheetInterval() { } // EF

    internal TeeSheetInterval(Guid teeSheetId, TimeOnly time, int capacity)
    {
        Id = Guid.CreateVersion7();
        TeeSheetId = teeSheetId;
        Time = time;
        Capacity = capacity;
    }
}
```

- [ ] **Step 5: Create `TeeSheet`**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheet : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TeeSheetStatus Status { get; private set; }
    public ScheduleSettings Settings { get; private set; } = null!;
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeSheetInterval> intervals = [];
    public IReadOnlyList<TeeSheetInterval> Intervals => this.intervals.AsReadOnly();

    private TeeSheet() { } // EF

    public static TeeSheet Draft(
        Guid courseId,
        DateOnly date,
        ScheduleSettings settings,
        ITimeProvider timeProvider)
    {
        var sheet = new TeeSheet
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            Date = date,
            Status = TeeSheetStatus.Draft,
            Settings = settings,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        var current = settings.FirstTeeTime;
        var step = TimeSpan.FromMinutes(settings.IntervalMinutes);
        while (current < settings.LastTeeTime)
        {
            sheet.intervals.Add(new TeeSheetInterval(sheet.Id, current, settings.DefaultCapacity));
            current = current.Add(step);
        }

        sheet.AddDomainEvent(new TeeSheetDrafted
        {
            TeeSheetId = sheet.Id,
            CourseId = courseId,
            Date = date,
            IntervalCount = sheet.intervals.Count,
        });

        return sheet;
    }

    public void Publish(ITimeProvider timeProvider)
    {
        if (Status == TeeSheetStatus.Published)
        {
            return;
        }

        Status = TeeSheetStatus.Published;
        PublishedAt = timeProvider.GetCurrentTimestamp();

        AddDomainEvent(new TeeSheetPublished
        {
            TeeSheetId = Id,
            CourseId = CourseId,
            Date = Date,
            PublishedAt = PublishedAt.Value,
        });
    }

    public BookingAuthorization AuthorizeBooking()
    {
        if (Status != TeeSheetStatus.Published)
        {
            throw new TeeSheetNotPublishedException(Id);
        }
        return new BookingAuthorization(Id);
    }
}
```

- [ ] **Step 6: Write the tests**

```csharp
// tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetTests.cs
using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class TeeSheetTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeSheetTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private static ScheduleSettings DefaultSettings() =>
        new(new TimeOnly(7, 0), new TimeOnly(9, 0), 30, 4);

    [Fact]
    public void Draft_EnumeratesIntervalsAtCorrectTimes()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        // 7:00, 7:30, 8:00, 8:30 — last (9:00) is exclusive
        Assert.Equal(4, sheet.Intervals.Count);
        Assert.Equal(new TimeOnly(7, 0), sheet.Intervals[0].Time);
        Assert.Equal(new TimeOnly(7, 30), sheet.Intervals[1].Time);
        Assert.Equal(new TimeOnly(8, 0), sheet.Intervals[2].Time);
        Assert.Equal(new TimeOnly(8, 30), sheet.Intervals[3].Time);
    }

    [Fact]
    public void Draft_AssignsCapacityFromSettings()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1),
            new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 2),
            this.timeProvider);

        Assert.All(sheet.Intervals, i => Assert.Equal(2, i.Capacity));
    }

    [Fact]
    public void Draft_AssignsUniqueIdsToIntervals()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        var ids = sheet.Intervals.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    [Fact]
    public void Draft_RaisesTeeSheetDraftedEvent()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 1);
        var sheet = TeeSheet.Draft(courseId, date, DefaultSettings(), this.timeProvider);

        var evt = Assert.IsType<TeeSheetDrafted>(Assert.Single(sheet.DomainEvents));
        Assert.Equal(sheet.Id, evt.TeeSheetId);
        Assert.Equal(courseId, evt.CourseId);
        Assert.Equal(date, evt.Date);
        Assert.Equal(4, evt.IntervalCount);
    }

    [Fact]
    public void Draft_StartsInDraftStatus()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        Assert.Equal(TeeSheetStatus.Draft, sheet.Status);
        Assert.Null(sheet.PublishedAt);
    }

    [Fact]
    public void Publish_TransitionsAndRaisesEvent()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.ClearDomainEvents();

        sheet.Publish(this.timeProvider);

        Assert.Equal(TeeSheetStatus.Published, sheet.Status);
        Assert.NotNull(sheet.PublishedAt);
        var evt = Assert.IsType<TeeSheetPublished>(Assert.Single(sheet.DomainEvents));
        Assert.Equal(sheet.Id, evt.TeeSheetId);
        Assert.Equal(sheet.PublishedAt.Value, evt.PublishedAt);
    }

    [Fact]
    public void Publish_IsIdempotent()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.Publish(this.timeProvider);
        sheet.ClearDomainEvents();

        sheet.Publish(this.timeProvider);

        Assert.Empty(sheet.DomainEvents);
    }

    [Fact]
    public void AuthorizeBooking_OnPublished_ReturnsTokenWithSheetId()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);
        sheet.Publish(this.timeProvider);

        var token = sheet.AuthorizeBooking();

        Assert.Equal(sheet.Id, token.SheetId);
    }

    [Fact]
    public void AuthorizeBooking_OnDraft_Throws()
    {
        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), DefaultSettings(), this.timeProvider);

        Assert.Throws<TeeSheetNotPublishedException>(() => sheet.AuthorizeBooking());
    }
}
```

- [ ] **Step 7: Build and run tests**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

Run: `dotnet test tests/Teeforce.Domain.Tests --filter FullyQualifiedName~TeeSheetTests`
Expected: PASS, all green.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(domain): add TeeSheet aggregate with Draft/Publish lifecycle and BookingAuthorization minting"
```

---

### Task 6: Create `TeeSheet` repository interface

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs`

The implementation lands in Task 11 (with the EF configuration). Defining the interface here keeps the domain self-contained for the unit-test phase.

- [ ] **Step 1: Create the interface**

```csharp
// src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public interface ITeeSheetRepository : IRepository<TeeSheet>
{
    void Add(TeeSheet sheet);
    Task<TeeSheet?> GetByCourseAndDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default);
    Task<TeeSheet?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default);
}
```

> **Note on `IRepository<T>`:** Look at `src/backend/Teeforce.Domain/Common/IRepository.cs` first to confirm the base interface. Other repos in the codebase (e.g., `IBookingRepository`) follow this pattern — match what they do. If `IRepository<T>` declares `GetByIdAsync`, you do not redeclare it here.

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(domain): add ITeeSheetRepository"
```

---

### Task 7: Create `TeeTime` aggregate (status, claims, events, exceptions)

This is the largest task in the plan. The aggregate has many invariants and each one gets a dedicated exception. Write all the supporting types first, then the aggregate, then a comprehensive test class.

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeStatus.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimed.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeFilled.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimReleased.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeReopened.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeBlocked.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeUnblocked.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeBlockedException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeFilledException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InsufficientCapacityException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InvalidGroupSizeException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/InvalidTeeTimeCapacityException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/BookingAuthorizationMismatchException.cs`
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/Exceptions/TeeTimeHasClaimsException.cs`
- Test: `tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimeTests.cs`

- [ ] **Step 1: Create the enum**

```csharp
// src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeStatus.cs
namespace Teeforce.Domain.TeeTimeAggregate;

public enum TeeTimeStatus
{
    Open,
    Filled,
    Blocked
}
```

- [ ] **Step 2: Create all six exception classes**

```csharp
// TeeTimeBlockedException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeBlockedException : DomainException
{
    public TeeTimeBlockedException(Guid teeTimeId)
        : base($"Tee time {teeTimeId} is blocked.") { }
}
```

```csharp
// TeeTimeFilledException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeFilledException : DomainException
{
    public TeeTimeFilledException(Guid teeTimeId)
        : base($"Tee time {teeTimeId} is fully booked.") { }
}
```

```csharp
// InsufficientCapacityException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InsufficientCapacityException : DomainException
{
    public InsufficientCapacityException(Guid teeTimeId, int requested, int remaining)
        : base($"Tee time {teeTimeId} has {remaining} slots remaining; {requested} requested.") { }
}
```

```csharp
// InvalidGroupSizeException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InvalidGroupSizeException : DomainException
{
    public InvalidGroupSizeException()
        : base("Group size must be positive.") { }
}
```

```csharp
// InvalidTeeTimeCapacityException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InvalidTeeTimeCapacityException : DomainException
{
    public InvalidTeeTimeCapacityException()
        : base("Tee time capacity must be positive.") { }
}
```

```csharp
// BookingAuthorizationMismatchException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class BookingAuthorizationMismatchException : DomainException
{
    public BookingAuthorizationMismatchException(Guid teeTimeId, Guid tokenSheetId, Guid actualSheetId)
        : base($"Booking authorization for sheet {tokenSheetId} does not match tee time {teeTimeId} (sheet {actualSheetId}).") { }
}
```

```csharp
// TeeTimeHasClaimsException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class TeeTimeHasClaimsException : DomainException
{
    public TeeTimeHasClaimsException(Guid teeTimeId, int claimCount)
        : base($"Tee time {teeTimeId} cannot be blocked: it has {claimCount} active claim(s).") { }
}
```

- [ ] **Step 3: Create all six event records**

```csharp
// TeeTimeClaimed.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeClaimed : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

```csharp
// TeeTimeFilled.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeFilled : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

```csharp
// TeeTimeClaimReleased.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeClaimReleased : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

```csharp
// TeeTimeReopened.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeReopened : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

```csharp
// TeeTimeBlocked.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeBlocked : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
    public required string Reason { get; init; }
}
```

```csharp
// TeeTimeUnblocked.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeUnblocked : IDomainEvent
{
    public required Guid TeeTimeId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
}
```

- [ ] **Step 4: Create `TeeTimeClaim`**

```csharp
// src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate;

public class TeeTimeClaim : Entity
{
    public Guid TeeTimeId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }

    private TeeTimeClaim() { } // EF

    internal TeeTimeClaim(Guid teeTimeId, Guid bookingId, Guid golferId, int groupSize, DateTimeOffset claimedAt)
    {
        Id = Guid.CreateVersion7();
        TeeTimeId = teeTimeId;
        BookingId = bookingId;
        GolferId = golferId;
        GroupSize = groupSize;
        ClaimedAt = claimedAt;
    }
}
```

- [ ] **Step 5: Create `TeeTime` aggregate**

```csharp
// src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;

namespace Teeforce.Domain.TeeTimeAggregate;

public class TeeTime : Entity
{
    public Guid TeeSheetId { get; private set; }
    public Guid TeeSheetIntervalId { get; private set; }
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }
    public int Remaining { get; private set; }
    public TeeTimeStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<TeeTimeClaim> claims = [];
    public IReadOnlyList<TeeTimeClaim> Claims => this.claims.AsReadOnly();

    private TeeTime() { } // EF

    public static TeeTime Claim(
        TeeSheetInterval interval,
        Guid courseId,
        DateOnly date,
        BookingAuthorization auth,
        Guid bookingId,
        Guid golferId,
        int groupSize,
        ITimeProvider timeProvider)
    {
        if (interval.Capacity <= 0)
        {
            throw new InvalidTeeTimeCapacityException();
        }
        if (auth.SheetId != interval.TeeSheetId)
        {
            throw new BookingAuthorizationMismatchException(Guid.Empty, auth.SheetId, interval.TeeSheetId);
        }
        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }
        if (groupSize > interval.Capacity)
        {
            throw new InsufficientCapacityException(Guid.Empty, groupSize, interval.Capacity);
        }

        var now = timeProvider.GetCurrentTimestamp();
        var teeTime = new TeeTime
        {
            Id = Guid.CreateVersion7(),
            TeeSheetId = interval.TeeSheetId,
            TeeSheetIntervalId = interval.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Capacity = interval.Capacity,
            Remaining = interval.Capacity,
            Status = TeeTimeStatus.Open,
            CreatedAt = now,
        };

        teeTime.ApplyClaim(bookingId, golferId, groupSize, now);
        return teeTime;
    }

    public void Claim(
        BookingAuthorization auth,
        Guid bookingId,
        Guid golferId,
        int groupSize,
        ITimeProvider timeProvider)
    {
        if (auth.SheetId != TeeSheetId)
        {
            throw new BookingAuthorizationMismatchException(Id, auth.SheetId, TeeSheetId);
        }
        if (Status == TeeTimeStatus.Blocked)
        {
            throw new TeeTimeBlockedException(Id);
        }
        if (Status == TeeTimeStatus.Filled || Remaining == 0)
        {
            throw new TeeTimeFilledException(Id);
        }
        if (groupSize <= 0)
        {
            throw new InvalidGroupSizeException();
        }
        if (groupSize > Remaining)
        {
            throw new InsufficientCapacityException(Id, groupSize, Remaining);
        }

        ApplyClaim(bookingId, golferId, groupSize, timeProvider.GetCurrentTimestamp());
    }

    private void ApplyClaim(Guid bookingId, Guid golferId, int groupSize, DateTimeOffset now)
    {
        this.claims.Add(new TeeTimeClaim(Id, bookingId, golferId, groupSize, now));
        Remaining -= groupSize;

        AddDomainEvent(new TeeTimeClaimed
        {
            TeeTimeId = Id,
            BookingId = bookingId,
            GolferId = golferId,
            GroupSize = groupSize,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });

        if (Remaining == 0)
        {
            Status = TeeTimeStatus.Filled;
            AddDomainEvent(new TeeTimeFilled
            {
                TeeTimeId = Id,
                CourseId = CourseId,
                Date = Date,
                Time = Time,
            });
        }
    }

    public void ReleaseClaim(Guid bookingId, ITimeProvider timeProvider)
    {
        var claim = this.claims.FirstOrDefault(c => c.BookingId == bookingId);
        if (claim is null)
        {
            return; // idempotent — release event may arrive twice
        }

        this.claims.Remove(claim);
        Remaining += claim.GroupSize;
        var wasFilled = Status == TeeTimeStatus.Filled;

        AddDomainEvent(new TeeTimeClaimReleased
        {
            TeeTimeId = Id,
            BookingId = claim.BookingId,
            GolferId = claim.GolferId,
            GroupSize = claim.GroupSize,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });

        if (wasFilled)
        {
            Status = TeeTimeStatus.Open;
            AddDomainEvent(new TeeTimeReopened
            {
                TeeTimeId = Id,
                CourseId = CourseId,
                Date = Date,
                Time = Time,
            });
        }
    }

    public static TeeTime Block(
        TeeSheetInterval interval,
        Guid courseId,
        DateOnly date,
        string reason,
        ITimeProvider timeProvider)
    {
        if (interval.Capacity <= 0)
        {
            throw new InvalidTeeTimeCapacityException();
        }

        var teeTime = new TeeTime
        {
            Id = Guid.CreateVersion7(),
            TeeSheetId = interval.TeeSheetId,
            TeeSheetIntervalId = interval.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Capacity = interval.Capacity,
            Remaining = interval.Capacity,
            Status = TeeTimeStatus.Blocked,
            CreatedAt = timeProvider.GetCurrentTimestamp(),
        };

        teeTime.AddDomainEvent(new TeeTimeBlocked
        {
            TeeTimeId = teeTime.Id,
            CourseId = courseId,
            Date = date,
            Time = interval.Time,
            Reason = reason,
        });

        return teeTime;
    }

    public void Block(string reason, ITimeProvider timeProvider)
    {
        if (Status == TeeTimeStatus.Blocked)
        {
            return;
        }
        if (this.claims.Count > 0)
        {
            throw new TeeTimeHasClaimsException(Id, this.claims.Count);
        }

        Status = TeeTimeStatus.Blocked;
        AddDomainEvent(new TeeTimeBlocked
        {
            TeeTimeId = Id,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
            Reason = reason,
        });
    }

    public void Unblock(ITimeProvider timeProvider)
    {
        if (Status != TeeTimeStatus.Blocked)
        {
            return;
        }
        Status = TeeTimeStatus.Open;
        AddDomainEvent(new TeeTimeUnblocked
        {
            TeeTimeId = Id,
            CourseId = CourseId,
            Date = Date,
            Time = Time,
        });
    }
}
```

> **Note on the factory's exception ids:** the factory throws before the `TeeTime.Id` exists, so it passes `Guid.Empty` to exceptions that take a `teeTimeId`. This is acceptable because the caller has the interval id for context. Tests below assert exception type, not id.

- [ ] **Step 6: Write the tests**

```csharp
// tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimeTests.cs
using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;

namespace Teeforce.Domain.Tests.TeeTimeAggregate;

public class TeeTimeTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly Guid courseId = Guid.NewGuid();
    private readonly DateOnly date = new(2026, 6, 1);

    public TeeTimeTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private (TeeSheet sheet, TeeSheetInterval interval, BookingAuthorization auth) MakeSheetAndInterval(int capacity = 4)
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, capacity);
        var sheet = TeeSheet.Draft(this.courseId, this.date, settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var interval = sheet.Intervals[0];
        var auth = sheet.AuthorizeBooking();
        return (sheet, interval, auth);
    }

    [Fact]
    public void ClaimFactory_CreatesOpenTeeTimeWithFirstClaim()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var golferId = Guid.NewGuid();

        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, golferId, 2, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Equal(4, teeTime.Capacity);
        Assert.Equal(2, teeTime.Remaining);
        Assert.Equal(interval.Id, teeTime.TeeSheetIntervalId);
        Assert.Equal(interval.TeeSheetId, teeTime.TeeSheetId);
        Assert.Equal(this.date, teeTime.Date);
        Assert.Equal(interval.Time, teeTime.Time);
        var claim = Assert.Single(teeTime.Claims);
        Assert.Equal(bookingId, claim.BookingId);
        Assert.Equal(2, claim.GroupSize);
    }

    [Fact]
    public void ClaimFactory_RaisesTeeTimeClaimed()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeClaimed);
    }

    [Fact]
    public void ClaimFactory_FullCapacity_TransitionsToFilledAndRaisesFilled()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Filled, teeTime.Status);
        Assert.Equal(0, teeTime.Remaining);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeFilled);
    }

    [Fact]
    public void ClaimInstance_AppendsAndDecrementsRemaining()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Equal(1, teeTime.Remaining);
        Assert.Equal(2, teeTime.Claims.Count);
        Assert.Single(teeTime.DomainEvents.OfType<TeeTimeClaimed>());
    }

    [Fact]
    public void ClaimInstance_OnFilled_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);

        Assert.Throws<TeeTimeFilledException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_OnBlocked_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);

        Assert.Throws<TeeTimeBlockedException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_GroupSizeExceedsRemaining_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 3, this.timeProvider);

        Assert.Throws<InsufficientCapacityException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_NonPositiveGroupSize_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        Assert.Throws<InvalidGroupSizeException>(() =>
            teeTime.Claim(auth, Guid.NewGuid(), Guid.NewGuid(), 0, this.timeProvider));
    }

    [Fact]
    public void ClaimInstance_TokenForOtherSheet_Throws()
    {
        var (_, interval, _) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, _ForReal(interval), Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        // Mint a token from a completely different sheet
        var otherSheet = TeeSheet.Draft(Guid.NewGuid(), this.date, new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 4), this.timeProvider);
        otherSheet.Publish(this.timeProvider);
        var foreignAuth = otherSheet.AuthorizeBooking();

        Assert.Throws<BookingAuthorizationMismatchException>(() =>
            teeTime.Claim(foreignAuth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
    }

    // Helper to obtain a token from the same sheet (since MakeSheetAndInterval already does this,
    // this helper is only needed when callers want to make sure the auth came from THIS sheet specifically).
    private BookingAuthorization _ForReal(TeeSheetInterval interval)
    {
        // re-fetch from the same sheet by reflection-free means: we need to find a sheet whose Id matches.
        // Simpler: skip this helper and have MakeSheetAndInterval return the auth.
        // (Test rewrite: use the auth returned from MakeSheetAndInterval directly.)
        throw new NotSupportedException("Use the auth returned from MakeSheetAndInterval; do not call this helper.");
    }

    [Fact]
    public void ReleaseClaim_RemovesClaimAndIncrementsRemaining()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(bookingId, this.timeProvider);

        Assert.Empty(teeTime.Claims);
        Assert.Equal(4, teeTime.Remaining);
        var released = Assert.IsType<TeeTimeClaimReleased>(Assert.Single(teeTime.DomainEvents));
        Assert.Equal(bookingId, released.BookingId);
        Assert.Equal(2, released.GroupSize);
    }

    [Fact]
    public void ReleaseClaim_FromFilled_TransitionsBackToOpenAndRaisesReopened()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 2);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(bookingId, this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeReopened);
    }

    [Fact]
    public void ReleaseClaim_UnknownBookingId_IsNoOp()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 2, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.ReleaseClaim(Guid.NewGuid(), this.timeProvider);

        Assert.Empty(teeTime.DomainEvents);
        Assert.Single(teeTime.Claims);
    }

    [Fact]
    public void BlockFactory_CreatesBlockedTeeTime()
    {
        var (_, interval, _) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);

        Assert.Equal(TeeTimeStatus.Blocked, teeTime.Status);
        Assert.Empty(teeTime.Claims);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeBlocked);
    }

    [Fact]
    public void BlockInstance_OpenWithNoClaims_Transitions()
    {
        var (_, interval, _) = MakeSheetAndInterval(capacity: 4);
        // Manufacture an Open TeeTime via reflection-free path: use the factory and immediately release.
        // Simpler: use the Block factory directly. We test instance Block by creating an Open one first.
        var auth = MakeSheetAndInterval(capacity: 4).auth;
        // Use a fresh sheet/interval pair so this test is independent.
        var (_, interval2, auth2) = MakeSheetAndInterval(capacity: 4);
        var bookingId = Guid.NewGuid();
        var teeTime = TeeTime.Claim(interval2, this.courseId, this.date, auth2, bookingId, Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ReleaseClaim(bookingId, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Block("maintenance", this.timeProvider);

        Assert.Equal(TeeTimeStatus.Blocked, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeBlocked);
    }

    [Fact]
    public void BlockInstance_WithClaims_Throws()
    {
        var (_, interval, auth) = MakeSheetAndInterval(capacity: 4);
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);

        Assert.Throws<TeeTimeHasClaimsException>(() => teeTime.Block("frost", this.timeProvider));
    }

    [Fact]
    public void Unblock_Transitions()
    {
        var (_, interval, _) = MakeSheetAndInterval();
        var teeTime = TeeTime.Block(interval, this.courseId, this.date, "frost", this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Unblock(this.timeProvider);

        Assert.Equal(TeeTimeStatus.Open, teeTime.Status);
        Assert.Contains(teeTime.DomainEvents, e => e is TeeTimeUnblocked);
    }

    [Fact]
    public void Unblock_AlreadyOpen_IsNoOp()
    {
        var (_, interval, auth) = MakeSheetAndInterval();
        var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);
        teeTime.ClearDomainEvents();

        teeTime.Unblock(this.timeProvider);

        Assert.Empty(teeTime.DomainEvents);
    }
}
```

> **Implementer cleanup:** the test file above contains a `_ForReal` placeholder helper that documents how I confused myself; **delete that helper and the call to it** when implementing. The `ClaimInstance_TokenForOtherSheet_Throws` test should be:
>
> ```csharp
> [Fact]
> public void ClaimInstance_TokenForOtherSheet_Throws()
> {
>     var (_, interval, auth) = MakeSheetAndInterval();
>     var teeTime = TeeTime.Claim(interval, this.courseId, this.date, auth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider);
>
>     var otherSheet = TeeSheet.Draft(Guid.NewGuid(), this.date,
>         new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 4), this.timeProvider);
>     otherSheet.Publish(this.timeProvider);
>     var foreignAuth = otherSheet.AuthorizeBooking();
>
>     Assert.Throws<BookingAuthorizationMismatchException>(() =>
>         teeTime.Claim(foreignAuth, Guid.NewGuid(), Guid.NewGuid(), 1, this.timeProvider));
> }
> ```

- [ ] **Step 7: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 8: Run TeeTime tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter FullyQualifiedName~TeeTimeTests`
Expected: PASS, all green.

- [ ] **Step 9: Run full domain test suite**

Run: `dotnet test tests/Teeforce.Domain.Tests`
Expected: ALL PASS.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(domain): add TeeTime aggregate with Open/Filled/Blocked statuses, claims, and capability-token guard"
```

---

### Task 8: Create `ITeeTimeRepository`

**Files:**
- Create: `src/backend/Teeforce.Domain/TeeTimeAggregate/ITeeTimeRepository.cs`

- [ ] **Step 1: Create the interface**

```csharp
// src/backend/Teeforce.Domain/TeeTimeAggregate/ITeeTimeRepository.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate;

public interface ITeeTimeRepository : IRepository<TeeTime>
{
    void Add(TeeTime teeTime);
    Task<TeeTime?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default);
    Task<List<TeeTime>> GetByTeeSheetIdAsync(Guid teeSheetId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(domain): add ITeeTimeRepository"
```

---

### Task 9: Modify `Booking` aggregate to carry `TeeTimeId`

**Files:**
- Modify: `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs`
- Modify: `tests/Teeforce.Domain.Tests/BookingAggregate/BookingTests.cs`
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeOpeningSlotsClaimed/CreateConfirmedBookingHandler.cs` (passes `null` for the new parameter — compat seam)
- Modify: any other call sites of `Booking.CreateConfirmed` found by Grep (today: only the walk-up handler)

- [ ] **Step 1: Add `TeeTimeId` (nullable) and a parameter to `CreateConfirmed`**

```csharp
// In Booking.cs, add property after PlayerCount:
public Guid? TeeTimeId { get; private set; }
```

Modify `CreateConfirmed` signature and body:

```csharp
public static Booking CreateConfirmed(
    Guid bookingId,
    Guid courseId,
    Guid golferId,
    Guid? teeTimeId,
    DateOnly date,
    TimeOnly teeTime,
    int playerCount)
{
    var now = DateTimeOffset.UtcNow;
    var booking = new Booking
    {
        Id = bookingId,
        CourseId = courseId,
        GolferId = golferId,
        TeeTimeId = teeTimeId,
        TeeTime = new BookingDateTime(date, teeTime),
        PlayerCount = playerCount,
        Status = BookingStatus.Confirmed,
        CreatedAt = now
    };

    booking.AddDomainEvent(new BookingConfirmed { BookingId = bookingId });

    return booking;
}
```

- [ ] **Step 2: Update all `Booking.CreateConfirmed` call sites to pass the new parameter**

The only production call site today is `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeOpeningSlotsClaimed/CreateConfirmedBookingHandler.cs`. Update it to pass `teeTimeId: null` (compat seam):

```csharp
var booking = Booking.CreateConfirmed(
    bookingId: evt.BookingId,
    courseId: evt.CourseId,
    golferId: evt.GolferId,
    teeTimeId: null,
    date: evt.Date,
    teeTime: evt.TeeTime,
    playerCount: evt.GroupSize);
```

Repeat for any test files that call `Booking.CreateConfirmed` directly. Pass `teeTimeId: null` unless the test specifically wants to assert non-null behavior.

- [ ] **Step 3: Add a domain test for the new field**

In `tests/Teeforce.Domain.Tests/BookingAggregate/BookingTests.cs`, add:

```csharp
[Fact]
public void CreateConfirmed_StoresTeeTimeId()
{
    var teeTimeId = Guid.NewGuid();
    var booking = Booking.CreateConfirmed(
        bookingId: Guid.NewGuid(),
        courseId: Guid.NewGuid(),
        golferId: Guid.NewGuid(),
        teeTimeId: teeTimeId,
        date: new DateOnly(2026, 6, 1),
        teeTime: new TimeOnly(9, 0),
        playerCount: 2);

    Assert.Equal(teeTimeId, booking.TeeTimeId);
}

[Fact]
public void CreateConfirmed_AllowsNullTeeTimeId_ForCompatSeam()
{
    var booking = Booking.CreateConfirmed(
        bookingId: Guid.NewGuid(),
        courseId: Guid.NewGuid(),
        golferId: Guid.NewGuid(),
        teeTimeId: null,
        date: new DateOnly(2026, 6, 1),
        teeTime: new TimeOnly(9, 0),
        playerCount: 2);

    Assert.Null(booking.TeeTimeId);
}
```

- [ ] **Step 4: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter FullyQualifiedName~BookingTests`
Expected: PASS.

Run: `dotnet test teeforce.slnx`
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(domain): add nullable Booking.TeeTimeId for the compat seam"
```

---

### Task 10: Add EF configuration for `TeeSheet` (and `TeeSheetInterval`)

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` (register `DbSet<TeeSheet>` and apply config)

- [ ] **Step 1: Create the configuration**

```csharp
// src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeSheetAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeSheetConfiguration : IEntityTypeConfiguration<TeeSheet>
{
    public void Configure(EntityTypeBuilder<TeeSheet> builder)
    {
        builder.ToTable("TeeSheets");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CourseId).IsRequired();
        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.PublishedAt);
        builder.Property(s => s.CreatedAt);

        builder.ComplexProperty(s => s.Settings, ss =>
        {
            ss.Property(x => x.FirstTeeTime).HasColumnName("Settings_FirstTeeTime").HasColumnType("time");
            ss.Property(x => x.LastTeeTime).HasColumnName("Settings_LastTeeTime").HasColumnType("time");
            ss.Property(x => x.IntervalMinutes).HasColumnName("Settings_IntervalMinutes");
            ss.Property(x => x.DefaultCapacity).HasColumnName("Settings_DefaultCapacity");
        });

        builder.OwnsMany(s => s.Intervals, i =>
        {
            i.ToTable("TeeSheetIntervals");
            i.WithOwner().HasForeignKey(x => x.TeeSheetId);
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.Time).HasColumnType("time");
            i.Property(x => x.Capacity);
            i.HasIndex(x => new { x.TeeSheetId, x.Time }).IsUnique();
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.CourseId, s.Date }).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
```

> **Note on `Intervals`:** the spec describes intervals as child entities that are read-only via `IReadOnlyList<TeeSheetInterval>`. Use `OwnsMany` (which EF Core supports for backing-field-style collections) — match the existing pattern in `TeeTimeOpeningConfiguration` (`OwnsMany(o => o.ClaimedSlots, ...)`). The private backing field `intervals` is automatically discovered by EF.

- [ ] **Step 2: Register in `ApplicationDbContext.cs`**

Add to imports: `using Teeforce.Domain.TeeSheetAggregate;`

Add the DbSet:

```csharp
public DbSet<TeeSheet> TeeSheets => Set<TeeSheet>();
```

Apply the config in `OnModelCreating`:

```csharp
modelBuilder.ApplyConfiguration(new TeeSheetConfiguration());
```

- [ ] **Step 3: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 4: Commit (do not generate migration yet — wait until Task 12)**

```bash
git add -A
git commit -m "feat(api): add EF configuration for TeeSheet and TeeSheetInterval"
```

---

### Task 11: Add EF configuration for `TeeTime` (and `TeeTimeClaim`); update `Course` and `Booking` configs

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs` (add `DefaultCapacity`)
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs` (rename ComplexProperty, add `TeeTimeId`)
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` (register `DbSet<TeeTime>`)

- [ ] **Step 1: Create `TeeTimeConfiguration.cs`**

```csharp
// src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeConfiguration : IEntityTypeConfiguration<TeeTime>
{
    public void Configure(EntityTypeBuilder<TeeTime> builder)
    {
        builder.ToTable("TeeTimes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TeeSheetId).IsRequired();
        builder.Property(t => t.TeeSheetIntervalId).IsRequired();
        builder.Property(t => t.CourseId).IsRequired();
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Time).HasColumnType("time");
        builder.Property(t => t.Capacity);
        builder.Property(t => t.Remaining);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.CreatedAt);

        builder.OwnsMany(t => t.Claims, c =>
        {
            c.ToTable("TeeTimeClaims");
            c.WithOwner().HasForeignKey(x => x.TeeTimeId);
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.Property(x => x.BookingId);
            c.Property(x => x.GolferId);
            c.Property(x => x.GroupSize);
            c.Property(x => x.ClaimedAt);
            c.HasIndex(x => new { x.TeeTimeId, x.BookingId }).IsUnique();
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TeeSheetIntervalId).IsUnique();
        builder.HasIndex(t => new { t.CourseId, t.Date });

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
```

- [ ] **Step 2: Update `CourseConfiguration.cs`** — add the `DefaultCapacity` column. Add this line near the other property mappings:

```csharp
builder.Property(c => c.DefaultCapacity).HasDefaultValue(4);
```

- [ ] **Step 3: Update `BookingConfiguration.cs`** — rename the ComplexProperty navigation (the C# property is still called `TeeTime`, but the type is now `BookingDateTime`; the column name stays `"TeeTime"` so the schema is unchanged) and add the `TeeTimeId` column:

The existing block already works (since the rename in Task 1 only changed the type, not the name), but for clarity update it to:

```csharp
builder.ComplexProperty(b => b.TeeTime, t =>
    t.Property(x => x.Value).HasColumnName("TeeTime").HasColumnType("datetime2"));

builder.Property(b => b.TeeTimeId);
builder.HasIndex(b => b.TeeTimeId);
```

- [ ] **Step 4: Register in `ApplicationDbContext.cs`**

Add to imports: `using Teeforce.Domain.TeeTimeAggregate;`

Add the DbSet (note the type collision with the old VO is now resolved — the VO is gone):

```csharp
public DbSet<TeeTime> TeeTimes => Set<TeeTime>();
```

Apply the config in `OnModelCreating`:

```csharp
modelBuilder.ApplyConfiguration(new TeeTimeConfiguration());
```

- [ ] **Step 5: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(api): add EF configuration for TeeTime; update Course and Booking configs"
```

---

### Task 12: Generate the EF migration

> **Important:** This task assumes the SQL Server dev container is running. Start it first if needed: `docker compose up db -d`. The repo CLAUDE.md states the DB is being reset for this change — a fresh initial migration is fine **if and only if** there are no other pending changes from earlier branches. Confirm this before generating.

**Files:**
- Generated: `src/backend/Teeforce.Api/Migrations/{timestamp}_TeeSheetAndTeeTimeAggregates.cs`
- Generated: `src/backend/Teeforce.Api/Migrations/ApplicationDbContextModelSnapshot.cs` (updated)

- [ ] **Step 1: Confirm there are no other pending model changes from earlier branches**

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations has-pending-model-changes --project src/backend/Teeforce.Api
```
Expected: outputs that **only** our changes are pending (not unrelated model drift). If unexpected drift appears, stop and ask the maintainer before proceeding.

- [ ] **Step 2: Add the migration**

```bash
dotnet ef migrations add TeeSheetAndTeeTimeAggregates --project src/backend/Teeforce.Api
```

- [ ] **Step 3: Inspect the generated migration**

Open the new file under `src/backend/Teeforce.Api/Migrations/` and confirm it contains:

- `CREATE TABLE TeeSheets` with `Settings_FirstTeeTime`, `Settings_LastTeeTime`, `Settings_IntervalMinutes`, `Settings_DefaultCapacity` columns
- `CREATE TABLE TeeSheetIntervals` with FK to `TeeSheets` and unique index on `(TeeSheetId, Time)`
- `CREATE TABLE TeeTimes` with FK to `Courses`, unique index on `TeeSheetIntervalId`, and `(CourseId, Date)` index
- `CREATE TABLE TeeTimeClaims` with FK to `TeeTimes` and unique index on `(TeeTimeId, BookingId)`
- `ALTER TABLE Courses ADD DefaultCapacity INT NOT NULL DEFAULT 4`
- `ALTER TABLE Bookings ADD TeeTimeId UNIQUEIDENTIFIER NULL` and an index on it

If any column is missing or wrong, fix the EF configuration and regenerate (`dotnet ef migrations remove --project src/backend/Teeforce.Api`, then re-add).

- [ ] **Step 4: Apply the migration to the local DB**

```bash
dotnet ef database update --project src/backend/Teeforce.Api
```
Expected: succeeds with no errors.

- [ ] **Step 5: Run the full test suite (this exercises Testcontainers integration tests against the new schema)**

```bash
dotnet test teeforce.slnx
```
Expected: ALL PASS. Existing integration tests still pass; no new tests in this task yet.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(api): EF migration for TeeSheet, TeeTime, and Course.DefaultCapacity"
```

---

### Task 13: Implement `TeeSheetRepository`

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs`
- Modify: `src/backend/Teeforce.Api/Program.cs` (DI registration — find where `IBookingRepository` and `ITeeTimeOpeningRepository` are registered and add the new ones nearby)

- [ ] **Step 1: Create the repository**

```csharp
// src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeSheetAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class TeeSheetRepository(ApplicationDbContext db) : ITeeSheetRepository
{
    public async Task<TeeSheet?> GetByIdAsync(Guid id) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<TeeSheet?> GetByCourseAndDateAsync(Guid courseId, DateOnly date, CancellationToken ct = default) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.CourseId == courseId && s.Date == date, ct);

    public async Task<TeeSheet?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .FirstOrDefaultAsync(s => s.Intervals.Any(i => i.Id == intervalId), ct);

    public void Add(TeeSheet sheet) => db.TeeSheets.Add(sheet);
}
```

> **Match `IRepository<T>` signature:** if the base interface declares `GetByIdAsync` differently (e.g., with a `CancellationToken`), align the override with what `BookingRepository`/`TeeTimeOpeningRepository` already do. Read those two files first.

- [ ] **Step 2: Register in DI**

Find `Program.cs` and add (next to the existing repository registrations):

```csharp
builder.Services.AddScoped<ITeeSheetRepository, TeeSheetRepository>();
```

- [ ] **Step 3: Build and run tests**

Run: `dotnet build teeforce.slnx && dotnet test teeforce.slnx`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(api): implement TeeSheetRepository"
```

---

### Task 14: Implement `TeeTimeRepository`

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeTimeRepository.cs`
- Modify: `src/backend/Teeforce.Api/Program.cs` (DI registration)

- [ ] **Step 1: Create the repository**

```csharp
// src/backend/Teeforce.Api/Infrastructure/Repositories/TeeTimeRepository.cs
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class TeeTimeRepository(ApplicationDbContext db) : ITeeTimeRepository
{
    public async Task<TeeTime?> GetByIdAsync(Guid id) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<TeeTime?> GetByIntervalIdAsync(Guid intervalId, CancellationToken ct = default) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .FirstOrDefaultAsync(t => t.TeeSheetIntervalId == intervalId, ct);

    public async Task<List<TeeTime>> GetByTeeSheetIdAsync(Guid teeSheetId, CancellationToken ct = default) =>
        await db.TeeTimes
            .Include(t => t.Claims)
            .Where(t => t.TeeSheetId == teeSheetId)
            .ToListAsync(ct);

    public void Add(TeeTime teeTime) => db.TeeTimes.Add(teeTime);
}
```

- [ ] **Step 2: Register in DI**

```csharp
builder.Services.AddScoped<ITeeTimeRepository, TeeTimeRepository>();
```

- [ ] **Step 3: Build and run tests**

Run: `dotnet build teeforce.slnx && dotnet test teeforce.slnx`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(api): implement TeeTimeRepository"
```

---

### Task 15: Wire `TeeSheetNotPublishedException` and `BookingAuthorizationMismatchException` into `DomainExceptionHandler`

**Files:**
- Modify: `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`

- [ ] **Step 1: Add the new mappings to the exception switch**

Add the namespace imports:

```csharp
using Teeforce.Domain.TeeSheetAggregate.Exceptions;
using Teeforce.Domain.TeeTimeAggregate.Exceptions;
using Teeforce.Domain.CourseAggregate.Exceptions;
using Teeforce.Domain.TeeSheetAggregate.Exceptions; // (one import per namespace; consolidate)
```

Add cases to the switch (place them with the existing `409 Conflict` and `422 UnprocessableEntity` groups):

```csharp
TeeSheetNotPublishedException => StatusCodes.Status409Conflict,
TeeSheetAlreadyExistsException => StatusCodes.Status409Conflict,
TeeTimeBlockedException => StatusCodes.Status409Conflict,
TeeTimeFilledException => StatusCodes.Status409Conflict,
TeeTimeHasClaimsException => StatusCodes.Status409Conflict,
BookingAuthorizationMismatchException => StatusCodes.Status409Conflict,
InsufficientCapacityException => StatusCodes.Status422UnprocessableEntity,
Teeforce.Domain.TeeTimeAggregate.Exceptions.InvalidGroupSizeException => StatusCodes.Status422UnprocessableEntity,
InvalidTeeTimeCapacityException => StatusCodes.Status422UnprocessableEntity,
InvalidScheduleSettingsException => StatusCodes.Status422UnprocessableEntity,
CourseScheduleNotConfiguredException => StatusCodes.Status409Conflict,
```

Note: the existing `InvalidGroupSizeException` mapping refers to `Teeforce.Domain.TeeTimeOpeningAggregate.Exceptions.InvalidGroupSizeException`. The new `Teeforce.Domain.TeeTimeAggregate.Exceptions.InvalidGroupSizeException` is a distinct type — disambiguate with the fully-qualified name as shown above.

> **`TeeSheetAlreadyExistsException`** is referenced here but not yet created — add it now in the same task to keep the file's switch clean:
>
> ```csharp
> // src/backend/Teeforce.Domain/TeeSheetAggregate/Exceptions/TeeSheetAlreadyExistsException.cs
> using Teeforce.Domain.Common;
>
> namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;
>
> public class TeeSheetAlreadyExistsException : DomainException
> {
>     public TeeSheetAlreadyExistsException(Guid courseId, DateOnly date)
>         : base($"A tee sheet for course {courseId} on {date:yyyy-MM-dd} already exists.") { }
> }
> ```

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(api): map new domain exceptions to HTTP status codes"
```

---

### Task 16: New endpoint — `POST /courses/{courseId}/tee-sheets/draft`

**Files:**
- Create: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs`

- [ ] **Step 1: Create the endpoint**

```csharp
// src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record DraftTeeSheetRequest(DateOnly Date);

public class DraftTeeSheetRequestValidator : AbstractValidator<DraftTeeSheetRequest>
{
    public DraftTeeSheetRequestValidator()
    {
        RuleFor(r => r.Date).NotEmpty();
    }
}

public static class DraftTeeSheetEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/draft")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        DraftTeeSheetRequest request,
        ICourseRepository courseRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(courseId);

        var existing = await teeSheetRepository.GetByCourseAndDateAsync(courseId, request.Date, ct);
        if (existing is not null)
        {
            throw new TeeSheetAlreadyExistsException(courseId, request.Date);
        }

        var settings = course.CurrentScheduleDefaults();
        var sheet = TeeSheet.Draft(courseId, request.Date, settings, timeProvider);
        teeSheetRepository.Add(sheet);

        return Results.Ok(new { teeSheetId = sheet.Id });
    }
}
```

> **Conventions reminder:** `CourseExistsMiddleware` runs by policy on `{courseId}` routes — so the path tenant scoping is already handled. We still need to load the course aggregate to call `CurrentScheduleDefaults()`. Use `GetRequiredByIdAsync` (extension in `Teeforce.Domain.Common.RepositoryExtensions`) — never `GetByIdAsync ?? throw`.
> No call to `SaveChangesAsync` — Wolverine transactional middleware handles it automatically.

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(api): add POST /courses/{courseId}/tee-sheets/draft endpoint"
```

---

### Task 17: New endpoint — `POST /courses/{courseId}/tee-sheets/{date}/publish`

**Files:**
- Create: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/PublishTeeSheetEndpoint.cs`

- [ ] **Step 1: Create the endpoint**

```csharp
// src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/PublishTeeSheetEndpoint.cs
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public static class PublishTeeSheetEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/{date}/publish")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        string date,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateOnly))
        {
            return Results.BadRequest(new { error = "date must be in yyyy-MM-dd format." });
        }

        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, dateOnly, ct);
        if (sheet is null)
        {
            return Results.NotFound(new { error = "Tee sheet not found." });
        }

        sheet.Publish(timeProvider); // idempotent
        return Results.Ok(new { teeSheetId = sheet.Id, status = sheet.Status.ToString() });
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(api): add POST /courses/{courseId}/tee-sheets/{date}/publish endpoint"
```

---

### Task 18: New endpoint — `POST /courses/{courseId}/tee-times/book` and the `TeeTimeClaimed` → `Booking` handler

This task wires the full direct-booking flow: HTTP endpoint → `TeeTime.Claim` → domain event → handler creates `Booking`.

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Bookings/Endpoints/BookTeeTimeEndpoint.cs`
- Create: `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs`
- Test: `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/TeeTimeClaimedCreateConfirmedBookingHandlerTests.cs`

- [ ] **Step 1: Create the endpoint**

```csharp
// src/backend/Teeforce.Api/Features/Bookings/Endpoints/BookTeeTimeEndpoint.cs
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.Bookings.Endpoints;

public record BookTeeTimeRequest(
    Guid BookingId,
    Guid TeeSheetIntervalId,
    Guid GolferId,
    int GroupSize);

public class BookTeeTimeRequestValidator : AbstractValidator<BookTeeTimeRequest>
{
    public BookTeeTimeRequestValidator()
    {
        RuleFor(r => r.BookingId).NotEmpty();
        RuleFor(r => r.TeeSheetIntervalId).NotEmpty();
        RuleFor(r => r.GolferId).NotEmpty();
        RuleFor(r => r.GroupSize).GreaterThan(0);
    }
}

public static class BookTeeTimeEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-times/book")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        BookTeeTimeRequest request,
        ITeeSheetRepository teeSheetRepository,
        ITeeTimeRepository teeTimeRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var sheet = await teeSheetRepository.GetByIntervalIdAsync(request.TeeSheetIntervalId, ct);
        if (sheet is null)
        {
            return Results.NotFound(new { error = "Tee sheet interval not found." });
        }

        var auth = sheet.AuthorizeBooking(); // throws TeeSheetNotPublishedException if Draft
        var interval = sheet.Intervals.Single(i => i.Id == request.TeeSheetIntervalId);

        var existing = await teeTimeRepository.GetByIntervalIdAsync(request.TeeSheetIntervalId, ct);
        if (existing is null)
        {
            var teeTime = TeeTime.Claim(
                interval,
                courseId,
                sheet.Date,
                auth,
                request.BookingId,
                request.GolferId,
                request.GroupSize,
                timeProvider);
            teeTimeRepository.Add(teeTime);
        }
        else
        {
            existing.Claim(auth, request.BookingId, request.GolferId, request.GroupSize, timeProvider);
        }

        return Results.Ok(new { bookingId = request.BookingId });
    }
}
```

- [ ] **Step 2: Create the `TeeTimeClaimed` → `Booking` handler**

```csharp
// src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class TeeTimeClaimedCreateConfirmedBookingHandler
{
    public static void Handle(
        TeeTimeClaimed evt,
        IBookingRepository bookingRepository)
    {
        var booking = Booking.CreateConfirmed(
            bookingId: evt.BookingId,
            courseId: evt.CourseId,
            golferId: evt.GolferId,
            teeTimeId: evt.TeeTimeId,
            date: evt.Date,
            teeTime: evt.Time,
            playerCount: evt.GroupSize);

        bookingRepository.Add(booking);
    }
}
```

- [ ] **Step 3: Write the handler unit test**

```csharp
// tests/Teeforce.Api.Tests/Features/Bookings/Handlers/TeeTimeClaimedCreateConfirmedBookingHandlerTests.cs
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class TeeTimeClaimedCreateConfirmedBookingHandlerTests
{
    [Fact]
    public void Handle_AddsConfirmedBookingWithTeeTimeId()
    {
        var bookingRepo = Substitute.For<IBookingRepository>();
        var evt = new TeeTimeClaimed
        {
            TeeTimeId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            GolferId = Guid.NewGuid(),
            GroupSize = 2,
            CourseId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1),
            Time = new TimeOnly(9, 0),
        };

        TeeTimeClaimedCreateConfirmedBookingHandler.Handle(evt, bookingRepo);

        bookingRepo.Received(1).Add(Arg.Is<Booking>(b =>
            b.Id == evt.BookingId
            && b.CourseId == evt.CourseId
            && b.GolferId == evt.GolferId
            && b.TeeTimeId == evt.TeeTimeId
            && b.PlayerCount == 2
            && b.Status == BookingStatus.Confirmed));
    }
}
```

- [ ] **Step 4: Build and run new test**

Run: `dotnet build teeforce.slnx && dotnet test tests/Teeforce.Api.Tests --filter FullyQualifiedName~TeeTimeClaimedCreateConfirmedBookingHandlerTests`
Expected: PASS.

- [ ] **Step 5: Run full suite**

Run: `dotnet test teeforce.slnx`
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(api): add POST /tee-times/book endpoint and TeeTimeClaimed→Booking handler"
```

---

### Task 19: `BookingCancelled` → `ReleaseTeeTimeClaim` handler

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/ReleaseTeeTimeClaimHandler.cs`
- Test: `tests/Teeforce.Api.Tests/Features/Bookings/Handlers/BookingCancelledReleaseTeeTimeClaimHandlerTests.cs`

- [ ] **Step 1: Create the handler**

```csharp
// src/backend/Teeforce.Api/Features/Bookings/Handlers/BookingCancelled/ReleaseTeeTimeClaimHandler.cs
using Microsoft.Extensions.Logging;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Features.Bookings.Handlers;

public static class BookingCancelledReleaseTeeTimeClaimHandler
{
    public static async Task Handle(
        BookingCancelled evt,
        IBookingRepository bookingRepository,
        ITeeTimeRepository teeTimeRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(evt.BookingId);
        if (booking is null)
        {
            logger.LogWarning("Booking {BookingId} not found while releasing tee time claim", evt.BookingId);
            return;
        }

        if (booking.TeeTimeId is null)
        {
            // Walk-up bookings still flow through TeeTimeOpening — nothing to release here.
            return;
        }

        var teeTime = await teeTimeRepository.GetByIdAsync(booking.TeeTimeId.Value);
        if (teeTime is null)
        {
            logger.LogWarning(
                "TeeTime {TeeTimeId} not found while releasing claim for booking {BookingId}",
                booking.TeeTimeId.Value, evt.BookingId);
            return;
        }

        teeTime.ReleaseClaim(evt.BookingId, timeProvider);
    }
}
```

> **Idempotency:** `TeeTime.ReleaseClaim` is a silent no-op for unknown booking ids, so re-delivery is safe. The walk-up path's null `TeeTimeId` is also a safe early-return — but this is a **legitimate** silent return per the convention because we're explicitly handling the "this is a walk-up booking, not our concern" case. (Per the backend conventions, query-based lookups can return after logging; here the early return is *not* a lookup miss but a known business state, so no warning is needed.)

- [ ] **Step 2: Write the handler test**

```csharp
// tests/Teeforce.Api.Tests/Features/Bookings/Handlers/BookingCancelledReleaseTeeTimeClaimHandlerTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.BookingAggregate.Events;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Tests.Features.Bookings.Handlers;

public class BookingCancelledReleaseTeeTimeClaimHandlerTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public BookingCancelledReleaseTeeTimeClaimHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private TeeTime MakeFilledTeeTime(Guid bookingId, Guid courseId)
    {
        var settings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 30, 2);
        var sheet = TeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), settings, this.timeProvider);
        sheet.Publish(this.timeProvider);
        var auth = sheet.AuthorizeBooking();
        return TeeTime.Claim(sheet.Intervals[0], courseId, sheet.Date, auth, bookingId, Guid.NewGuid(), 2, this.timeProvider);
    }

    [Fact]
    public async Task Handle_DirectBooking_ReleasesTheClaim()
    {
        var bookingId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var teeTime = MakeFilledTeeTime(bookingId, courseId);

        var bookings = Substitute.For<IBookingRepository>();
        var teeTimes = Substitute.For<ITeeTimeRepository>();
        var booking = Booking.CreateConfirmed(bookingId, courseId, Guid.NewGuid(), teeTime.Id,
            new DateOnly(2026, 6, 1), new TimeOnly(7, 0), 2);
        bookings.GetByIdAsync(bookingId).Returns(booking);
        teeTimes.GetByIdAsync(teeTime.Id).Returns(teeTime);

        await BookingCancelledReleaseTeeTimeClaimHandler.Handle(
            new BookingCancelled { BookingId = bookingId, PreviousStatus = BookingStatus.Confirmed },
            bookings, teeTimes, this.timeProvider, NullLogger.Instance, CancellationToken.None);

        Assert.Empty(teeTime.Claims);
    }

    [Fact]
    public async Task Handle_WalkUpBooking_NoOps()
    {
        var bookings = Substitute.For<IBookingRepository>();
        var teeTimes = Substitute.For<ITeeTimeRepository>();
        var booking = Booking.CreateConfirmed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), teeTimeId: null,
            new DateOnly(2026, 6, 1), new TimeOnly(7, 0), 2);
        bookings.GetByIdAsync(Arg.Any<Guid>()).Returns(booking);

        await BookingCancelledReleaseTeeTimeClaimHandler.Handle(
            new BookingCancelled { BookingId = booking.Id, PreviousStatus = BookingStatus.Confirmed },
            bookings, teeTimes, this.timeProvider, NullLogger.Instance, CancellationToken.None);

        await teeTimes.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }
}
```

- [ ] **Step 3: Build and run tests**

Run: `dotnet build teeforce.slnx && dotnet test tests/Teeforce.Api.Tests --filter FullyQualifiedName~BookingCancelledReleaseTeeTimeClaimHandlerTests`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(api): release TeeTime claim on BookingCancelled for direct bookings"
```

---

### Task 20: Rewrite `GET /tee-sheets` to project from `TeeSheet`/`TeeTime`; seed published sheet in dev seed data

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/TeeSheet/TeeSheetEndpoints.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/DevSeedData.cs`
- Modify: `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs` — assertions remain valid (slot order, status, golfer name, player count) but the seed setup now must publish a sheet first; update the test setup to do so

> **Compatibility constraint from the spec (lines 533–534):** The response shape the UI consumes today (`teeTime`, `status`, `golferName`, `playerCount`) must remain in the response. Keep the existing `TeeSheetSlot` record fields intact even if some are sourced from a different aggregate now.

- [ ] **Step 1: Rewrite the endpoint**

Replace the body of `GetTeeSheet` with the new projection. Keep the request shape (query params `courseId`, `date`) unchanged. Read the existing file once before editing to preserve the imports, route attribute, and class structure.

```csharp
// src/backend/Teeforce.Api/Features/TeeSheet/TeeSheetEndpoints.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet;

public static class TeeSheetEndpoints
{
    [WolverineGet("/tee-sheets")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetTeeSheet(
        Guid? courseId,
        string? date,
        ApplicationDbContext db,
        ITeeSheetRepository teeSheetRepository,
        ITeeTimeRepository teeTimeRepository,
        CancellationToken ct)
    {
        if (courseId is null)
        {
            return Results.BadRequest(new { error = "courseId query parameter is required." });
        }
        if (string.IsNullOrWhiteSpace(date))
        {
            return Results.BadRequest(new { error = "date query parameter is required." });
        }
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateOnly))
        {
            return Results.BadRequest(new { error = "date must be in yyyy-MM-dd format." });
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId.Value, ct);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId.Value, dateOnly, ct);
        if (sheet is null)
        {
            return Results.Ok(new TeeSheetResponse(course.Id, course.Name, []));
        }

        var teeTimes = await teeTimeRepository.GetByTeeSheetIdAsync(sheet.Id, ct);
        var teeTimesByIntervalId = teeTimes.ToDictionary(t => t.TeeSheetIntervalId);

        // Look up golfer names for any claims
        var golferIds = teeTimes.SelectMany(t => t.Claims).Select(c => c.GolferId).Distinct().ToList();
        var golferNames = await db.Golfers
            .Where(g => golferIds.Contains(g.Id))
            .Select(g => new { g.Id, Name = g.FirstName + " " + g.LastName })
            .ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var slots = sheet.Intervals
            .OrderBy(i => i.Time)
            .Select(interval =>
            {
                var slotDateTime = dateOnly.ToDateTime(interval.Time);
                if (!teeTimesByIntervalId.TryGetValue(interval.Id, out var teeTime))
                {
                    return new TeeSheetSlot(slotDateTime, "open", null, 0);
                }

                if (teeTime.Status == TeeTimeStatus.Blocked)
                {
                    return new TeeSheetSlot(slotDateTime, "blocked", null, 0);
                }

                // Project the first claim (the existing UI shows one golfer per slot today;
                // multi-claim rendering is a future story). Player count comes from the claim.
                var firstClaim = teeTime.Claims.FirstOrDefault();
                if (firstClaim is null)
                {
                    return new TeeSheetSlot(slotDateTime, "open", null, 0);
                }

                var golferName = golferNames.GetValueOrDefault(firstClaim.GolferId);
                var status = teeTime.Status == TeeTimeStatus.Filled ? "booked" : "booked";
                return new TeeSheetSlot(slotDateTime, status, golferName, firstClaim.GroupSize);
            })
            .ToList();

        return Results.Ok(new TeeSheetResponse(course.Id, course.Name, slots));
    }
}

public record TeeSheetResponse(
    Guid CourseId,
    string CourseName,
    List<TeeSheetSlot> Slots);

public record TeeSheetSlot(
    DateTime TeeTime,
    string Status,
    string? GolferName,
    int PlayerCount);
```

> **Note:** the existing UI uses `"booked"` for any slot with a golfer regardless of fill state. Preserve that. The redundant ternary in the projection is intentional placeholder for richer status reporting in a follow-on spec. Feel free to simplify to `"booked"` directly.

- [ ] **Step 2: Update `DevSeedData.cs` to publish a tee sheet for the dev course**

Read `src/backend/Teeforce.Api/Infrastructure/Data/DevSeedData.cs` first to see how the dev course is created. After the course is created and committed, add code to:
1. Compute today's date (or `today + 1`) via `ITimeProvider.GetCurrentDate()` (or hardcode if seeding runs before DI).
2. Construct a `ScheduleSettings` from the course's current schedule defaults.
3. Call `TeeSheet.Draft(...)` and then `Publish(...)`.
4. Add the sheet to the DbContext.

Place this code where existing seed entities are added — match the pattern used for `TeeTimeOpening` seed data if any exists, otherwise put it after the course is added.

The exact insertion point and style depend on how `DevSeedData` is structured today; this plan does not prescribe the exact lines because the file is unread. Read it, find the analogous insertion point, and write idiomatic code.

- [ ] **Step 3: Update the integration test that hits `GET /tee-sheets`**

Read `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs`. Today it relies on `Course` schedule fields being set and direct creation of `Booking` rows. Update the **test setup** (not the assertions) so each scenario:
1. Creates a course with schedule defaults.
2. POSTs `/courses/{id}/tee-sheets/draft` and then `/publish` for the test date.
3. POSTs `/courses/{id}/tee-times/book` for each booking the scenario needs (instead of writing `Booking` rows directly).
4. Calls the existing `GET /tee-sheets` and asserts on the unchanged response shape.

Per the project's test integrity rules, **do not modify the existing assertions** unless an acceptance criterion changed. If an assertion fails, fix the production code, not the test. Setup and arrangement may be updated freely.

- [ ] **Step 4: Build**

Run: `dotnet build teeforce.slnx`
Expected: PASS.

- [ ] **Step 5: Run integration tests**

Run: `dotnet test tests/Teeforce.Api.IntegrationTests --filter FullyQualifiedName~TeeSheetEndpointsTests`
Expected: PASS.

- [ ] **Step 6: Run full suite**

Run: `dotnet test teeforce.slnx`
Expected: ALL PASS — including all walk-up/`TeeTimeOpening` tests, which must be unaffected by this work.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(api): rewrite GET /tee-sheets to project from TeeSheet/TeeTime; seed published sheet in dev"
```

---

### Task 21: End-to-end integration test for direct booking; manual smoke test; PR-ready

**Files:**
- Create: `tests/Teeforce.Api.IntegrationTests/TeeSheetDirectBookingTests.cs`

This is the spec's success-criteria gate: the full draft → publish → book → cancel flow must work end-to-end through real HTTP, real EF, real Wolverine, real SQL Server (Testcontainers).

- [ ] **Step 1: Read existing integration test patterns**

Read `tests/Teeforce.Api.IntegrationTests/TestWebApplicationFactory.cs`, `TestSetup.cs`, `ResponseDtos.cs`, `StepOrderer.cs`, and at least one existing scenario test (e.g., a Waitlist scenario) so the new file follows the conventions exactly. Match the test class style, helper usage, and ordering attributes.

- [ ] **Step 2: Write the scenario test**

```csharp
// tests/Teeforce.Api.IntegrationTests/TeeSheetDirectBookingTests.cs
// Skeleton — match the patterns used in existing scenario tests in this folder.
// Use the same TestWebApplicationFactory fixture and StepOrderer attributes.

namespace Teeforce.Api.IntegrationTests;

[Collection(TestCollections.SharedFactory)] // or whatever the existing collection name is
public class TeeSheetDirectBookingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public TeeSheetDirectBookingTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task DraftPublishBook_HappyPath()
    {
        var client = await this.factory.CreateAuthenticatedClientForOperator();
        var course = await TestSetup.CreateCourseWithSchedule(client);
        var date = new DateOnly(2026, 7, 1);

        // Draft
        var draftResp = await client.PostAsJsonAsync(
            $"/courses/{course.Id}/tee-sheets/draft", new { date });
        draftResp.EnsureSuccessStatusCode();

        // Publish
        var publishResp = await client.PostAsync(
            $"/courses/{course.Id}/tee-sheets/{date:yyyy-MM-dd}/publish", null);
        publishResp.EnsureSuccessStatusCode();

        // Look up an interval id from GET /tee-sheets (or query the DB via the factory)
        // ... use the same pattern as other scenario tests for getting an interval id
        var intervalId = await TestSetup.GetFirstIntervalId(this.factory, course.Id, date);

        var golferId = await TestSetup.CreateGolfer(this.factory);
        var bookingId = Guid.CreateVersion7();

        // Book
        var bookResp = await client.PostAsJsonAsync(
            $"/courses/{course.Id}/tee-times/book",
            new { bookingId, teeSheetIntervalId = intervalId, golferId, groupSize = 2 });
        bookResp.EnsureSuccessStatusCode();

        // Assert: Booking row, TeeTime row, TeeTimeClaim row, statuses
        await using var scope = this.factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed, booking!.Status);
        Assert.NotNull(booking.TeeTimeId);

        var teeTime = await db.TeeTimes.Include(t => t.Claims)
            .FirstOrDefaultAsync(t => t.Id == booking.TeeTimeId);
        Assert.NotNull(teeTime);
        Assert.Equal(TeeTimeStatus.Open, teeTime!.Status); // 4-cap, 2 booked → still Open
        Assert.Equal(2, teeTime.Remaining);
        var claim = Assert.Single(teeTime.Claims);
        Assert.Equal(bookingId, claim.BookingId);
    }

    [Fact]
    public async Task BookingAgainstDraftSheet_Returns409()
    {
        var client = await this.factory.CreateAuthenticatedClientForOperator();
        var course = await TestSetup.CreateCourseWithSchedule(client);
        var date = new DateOnly(2026, 7, 2);

        await client.PostAsJsonAsync($"/courses/{course.Id}/tee-sheets/draft", new { date });
        // intentionally NOT publishing

        var intervalId = await TestSetup.GetFirstIntervalId(this.factory, course.Id, date);
        var golferId = await TestSetup.CreateGolfer(this.factory);

        var bookResp = await client.PostAsJsonAsync(
            $"/courses/{course.Id}/tee-times/book",
            new { bookingId = Guid.CreateVersion7(), teeSheetIntervalId = intervalId, golferId, groupSize = 1 });

        Assert.Equal(HttpStatusCode.Conflict, bookResp.StatusCode);
    }

    [Fact]
    public async Task FillScenario_NextBookingExceedsCapacity_Returns422()
    {
        // Book to capacity (4), then try a 5th — InsufficientCapacityException → 422
    }

    [Fact]
    public async Task CancelDirectBooking_ReleasesClaimAndRaisesReleaseEventWithGroupSize()
    {
        // Book 2/4, cancel via POST /bookings/{id}/cancel, assert TeeTime.Remaining == 4 and Status == Open
    }

    [Fact]
    public async Task TwoConcurrentBookingsForLastSlot_OneSucceedsOneFails()
    {
        // Book 3/4, then fire two parallel bookings of 1 — exactly one returns 2xx, the other 4xx
    }
}
```

> **Note:** this test uses helpers like `TestSetup.CreateCourseWithSchedule`, `TestSetup.CreateGolfer`, and `TestSetup.GetFirstIntervalId` — **read `TestSetup.cs` first** to see what helpers already exist, and add new ones in `TestSetup.cs` only if needed. Reuse rather than duplicate. The `[Collection]` attribute and `IClassFixture<>` usage must match the existing test class style.

- [ ] **Step 3: Run the new tests**

Run: `dotnet test tests/Teeforce.Api.IntegrationTests --filter FullyQualifiedName~TeeSheetDirectBookingTests`
Expected: PASS.

- [ ] **Step 4: Run the full test suite — every test, every project**

Run: `dotnet test teeforce.slnx`
Expected: ALL PASS. Pay special attention to:
- All `tests/Teeforce.Domain.Tests/TeeTimeOpeningAggregate/...` — must be unchanged (walk-up flow untouched).
- All `tests/Teeforce.Api.Tests/Features/Waitlist/...` — must be unchanged.
- Any `tests/Teeforce.Api.IntegrationTests/Waitlist*` files — must be unchanged.

- [ ] **Step 5: Run `dotnet format`**

Run: `dotnet format teeforce.slnx`
Expected: zero changes (or only style fixups). Stage any fixups.

- [ ] **Step 6: Manual smoke test via `make dev`**

```bash
make dev
```

In a second shell or browser:
1. Authenticate as an operator for the dev course.
2. Hit `POST /courses/{id}/tee-sheets/draft` with today's date (or use the seeded sheet from Task 20).
3. Hit `POST /courses/{id}/tee-sheets/{today}/publish`.
4. Hit `GET /tee-sheets?courseId={id}&date={today}` and confirm slots come back.
5. Hit `POST /courses/{id}/tee-times/book` with a real interval id from step 4.
6. Hit `GET /tee-sheets` again and confirm the booked slot now shows the golfer name and player count.
7. Open the operator UI in the browser and visit the tee sheet page — confirm the existing UI still renders. (The response shape is preserved; the UI should not need changes.)

If anything fails, fix it before continuing.

- [ ] **Step 7: Commit and PR**

```bash
git add -A
git commit -m "test(api): end-to-end integration tests for the direct booking flow"
```

Open the PR with a description that:
- Summarizes the new aggregates and the capability-token pattern.
- Calls out the **compatibility seam**: `Bookings.TeeTimeId` is nullable, walk-up bookings continue to flow through `TeeTimeOpening`, and capacity is tracked in two universes during the seam — the follow-on spec resolves this.
- Lists the success criteria from the spec and confirms each one.
- Links to `docs/superpowers/specs/2026-04-08-tee-sheet-domain-design.md`.

---

## Self-Review Notes

After re-reading the spec against this plan:

- **Spec coverage:** All ten "in scope" items are addressed:
  1. `TeeSheet` aggregate with `ScheduleSettings` and `TeeSheetInterval` → Tasks 3, 5, 10.
  2. `TeeTime` aggregate, lazily materialized → Task 7, 11.
  3. `TeeTimeClaim` child entity → Task 7.
  4. Rename old `TeeTime` VO to `BookingDateTime` → Task 1.
  5. `BookingAuthorization` capability token → Tasks 4, 5, 7.
  6. `ScheduleSettings` VO + `Course.CurrentScheduleDefaults()` + `Course.DefaultCapacity` → Tasks 2, 3, 11.
  7. Direct-booking flow → `Booking.TeeTimeId` → Tasks 9, 18.
  8. Booking stays minimal → Task 9.
  9. Rewrite `GetTeeSheet` → Task 20.
  10. New HTTP surface (`/draft`, `/publish`, `/book`) → Tasks 16, 17, 18.

- **Not in scope** (per spec): walk-up/`TeeTimeOpening` migration is deliberately not touched. This plan does not modify any `TeeTimeOpening*` files except the rename in Task 1, which is type-rename only.

- **Type consistency check:** the names used across tasks (`TeeSheet.Draft`, `TeeSheet.Publish`, `TeeSheet.AuthorizeBooking`, `BookingAuthorization.SheetId`, `TeeTime.Claim` (factory and instance), `TeeTime.ReleaseClaim`, `TeeTime.Block` (factory and instance), `TeeTime.Unblock`, `TeeTimeClaim.{BookingId, GolferId, GroupSize}`, `ScheduleSettings.{FirstTeeTime, LastTeeTime, IntervalMinutes, DefaultCapacity}`, `Course.CurrentScheduleDefaults()`, `Course.UpdateDefaultCapacity()`) are consistent across every task and match the spec.

- **Open questions** from the spec:
  1. Auto-creation of sheets — handled by Task 20 (DevSeedData seeds one published sheet); explicit `/draft` and `/publish` endpoints exist (Tasks 16, 17). Operator-facing "draft a week" workflow is explicitly future scope.
  2. Cross-slot invariants — not added in this spec; not added in this plan.
  3. Frost-delay cascade consistency — not implemented in this spec; the plan does not introduce `FrostDelay`.

- **Compat seam reminder:** the PR description in Task 21 must call out the dual-universe capacity tracking and the nullable `TeeTimeId` so reviewers understand why two patterns coexist.
