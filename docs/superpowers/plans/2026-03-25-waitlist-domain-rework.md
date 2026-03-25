# Waitlist Domain Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework the waitlist/offer domain model with proper naming, aggregate boundaries, inheritance, time windows, and concurrent offer management.

**Architecture:** Domain-first with TDD. Build new aggregates and services in the domain layer, then rewire handlers and policies in the API layer. Fresh EF migration at the end. Spec: `docs/superpowers/specs/2026-03-25-waitlist-domain-rework-design.md`

**Tech Stack:** .NET 10, EF Core 10, WolverineFx (Sagas), xUnit, NSubstitute, FluentValidation

**Conventions:** See `.claude/rules/backend/backend-conventions.md` — `private set`, `Guid.CreateVersion7()`, `var`, braces on control flow, `this.` for fields, `record` events with `IDomainEvent`, NSubstitute for stubs, real domain objects in tests.

---

## File Map

### New Domain Files (src/backend/Shadowbrook.Domain/)

| File | Responsibility |
|------|----------------|
| `CourseWaitlistAggregate/CourseWaitlist.cs` | Abstract base aggregate — Id, CourseId, Date, Join() factory |
| `CourseWaitlistAggregate/WalkUpWaitlist.cs` | Walk-up subtype — ShortCode, Open/Close/Reopen, 30min window |
| `CourseWaitlistAggregate/OnlineWaitlist.cs` | Online subtype — golfer-specified window |
| `CourseWaitlistAggregate/WaitlistStatus.cs` | Enum: Open, Closed |
| `CourseWaitlistAggregate/ICourseWaitlistRepository.cs` | Repository interface |
| `CourseWaitlistAggregate/IShortCodeGenerator.cs` | Short code generation interface (moved from old location) |
| `CourseWaitlistAggregate/Events/GolferJoinedWaitlist.cs` | Event raised on Join() |
| `CourseWaitlistAggregate/Events/WalkUpWaitlistOpened.cs` | Event raised on Open() |
| `CourseWaitlistAggregate/Events/WalkUpWaitlistClosed.cs` | Event raised on Close() |
| `CourseWaitlistAggregate/Events/WalkUpWaitlistReopened.cs` | Event raised on Reopen() |
| `CourseWaitlistAggregate/Exceptions/WaitlistNotOpenException.cs` | Thrown when joining closed waitlist |
| `CourseWaitlistAggregate/Exceptions/WaitlistAlreadyExistsException.cs` | Thrown on duplicate open |
| `CourseWaitlistAggregate/Exceptions/WaitlistNotClosedException.cs` | Thrown on reopen when not closed |
| `CourseWaitlistAggregate/Exceptions/GolferAlreadyOnWaitlistException.cs` | Thrown on duplicate join |
| `GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs` | Abstract base — window, groupSize, Remove() |
| `GolferWaitlistEntryAggregate/WalkUpGolferWaitlistEntry.cs` | Walk-up subtype — ExtendWindow() |
| `GolferWaitlistEntryAggregate/OnlineGolferWaitlistEntry.cs` | Online subtype (empty for now) |
| `GolferWaitlistEntryAggregate/Events/GolferRemovedFromWaitlist.cs` | Existing event (unchanged) |
| `GolferWaitlistEntryAggregate/Events/WalkUpEntryWindowExtended.cs` | New event for window extension |
| `GolferWaitlistEntryAggregate/IGolferWaitlistEntryRepository.cs` | Repository interface (updated with FindEligibleEntries) |
| `TeeTimeOpeningAggregate/TeeTimeOpening.cs` | Opening aggregate — Claim(), Expire() |
| `TeeTimeOpeningAggregate/TeeTimeOpeningStatus.cs` | Enum: Open, Filled, Expired |
| `TeeTimeOpeningAggregate/ITeeTimeOpeningRepository.cs` | Repository interface |
| `TeeTimeOpeningAggregate/Events/TeeTimeOpeningCreated.cs` | Event on creation |
| `TeeTimeOpeningAggregate/Events/TeeTimeOpeningClaimed.cs` | Event on successful claim |
| `TeeTimeOpeningAggregate/Events/TeeTimeOpeningClaimRejected.cs` | Event on failed claim |
| `TeeTimeOpeningAggregate/Events/TeeTimeOpeningFilled.cs` | Event when all slots claimed |
| `TeeTimeOpeningAggregate/Events/TeeTimeOpeningExpired.cs` | Event on expiration |
| `TeeTimeOpeningAggregate/Exceptions/OpeningNotAvailableException.cs` | Thrown on claim when not open |
| `WaitlistServices/WaitlistMatchingService.cs` | Domain service — FindEligibleEntries(opening) |

### New Test Files (tests/Shadowbrook.Domain.Tests/)

| File | Responsibility |
|------|----------------|
| `CourseWaitlistAggregate/WalkUpWaitlistTests.cs` | Walk-up waitlist creation, open/close/reopen, join |
| `CourseWaitlistAggregate/OnlineWaitlistTests.cs` | Online waitlist join with custom window |
| `GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs` | Remove, time window, ExtendWindow |
| `TeeTimeOpeningAggregate/TeeTimeOpeningTests.cs` | Create, Claim, Expire, fill lifecycle |
| `WaitlistServices/WaitlistMatchingServiceTests.cs` | Eligibility matching logic |

### Files to Delete (after all new code is working)

| File | Replaced By |
|------|-------------|
| `Domain/WalkUpWaitlistAggregate/` (entire folder) | `Domain/CourseWaitlistAggregate/` |
| `Domain/TeeTimeRequestAggregate/` (entire folder) | `Domain/TeeTimeOpeningAggregate/` |
| `Domain/GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs` | New hierarchy version |
| `Domain.Tests/WalkUpWaitlistAggregate/` | New test files |
| `Domain.Tests/TeeTimeRequestAggregate/` | New test files |
| `Domain.Tests/GolferWaitlistEntryAggregate/` | New test files |
| All files in `Api/Migrations/` | Fresh migration |

### Files to Modify Later (handlers, policies, EF configs — Tasks 7+)

These are listed here for awareness but will be addressed in later tasks after the domain layer is solid. The exact changes depend on what we learn building the domain.

---

## Task 1: CourseWaitlist Base + WalkUpWaitlist Aggregate

**Files:**
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/CourseWaitlist.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/WalkUpWaitlist.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/WaitlistStatus.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/ICourseWaitlistRepository.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/IShortCodeGenerator.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Events/WalkUpWaitlistOpened.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Events/WalkUpWaitlistClosed.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Events/WalkUpWaitlistReopened.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Events/GolferJoinedWaitlist.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Exceptions/WaitlistNotOpenException.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Exceptions/WaitlistAlreadyExistsException.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Exceptions/WaitlistNotClosedException.cs`
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/Exceptions/GolferAlreadyOnWaitlistException.cs`
- Test: `tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/WalkUpWaitlistTests.cs`
- Reference: existing `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/WalkUpWaitlist.cs` for behavior patterns

**Context:** The `CourseWaitlist` is an abstract base. `WalkUpWaitlist` extends it with Open/Close/Reopen and a ShortCode. The waitlist is a factory for `GolferWaitlistEntry` (built in Task 2), but for now `Join()` can't be fully implemented until the entry hierarchy exists. Implement everything except `Join()` in this task.

The existing `WalkUpWaitlist` (`src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/WalkUpWaitlist.cs`) has the current behavior — use it as a reference for patterns but implement the new hierarchy from scratch.

- [ ] **Step 1: Create WaitlistStatus enum**

```csharp
// src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/WaitlistStatus.cs
namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public enum WaitlistStatus
{
    Open,
    Closed
}
```

- [ ] **Step 2: Create event records**

Create four event records following the existing pattern in the codebase (see `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/Events/TeeTimeRequestAdded.cs` for the pattern). Each is a `record` implementing `IDomainEvent` with `EventId` and `OccurredAt` defaults.

`WalkUpWaitlistOpened { CourseWaitlistId }` — raised when operator opens a walk-up waitlist.
`WalkUpWaitlistClosed { CourseWaitlistId }` — raised when operator closes it.
`WalkUpWaitlistReopened { CourseWaitlistId }` — raised when operator reopens.
`GolferJoinedWaitlist { GolferWaitlistEntryId, CourseWaitlistId, GolferId }` — raised on join (same shape as current event).

- [ ] **Step 3: Create exception classes**

Create four exception classes inheriting from `DomainException` (see existing exceptions in `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/Exceptions/` for pattern).

`WaitlistNotOpenException` — thrown when joining/modifying a non-open waitlist.
`WaitlistAlreadyExistsException(WaitlistStatus existingStatus)` — thrown on duplicate open. Include `ExistingStatus` property. Message: "already open" if Open, "already used" if Closed.
`WaitlistNotClosedException` — thrown on reopen when not closed.
`GolferAlreadyOnWaitlistException(string phone)` — thrown on duplicate join. Include phone in message.

- [ ] **Step 4: Create IShortCodeGenerator and ICourseWaitlistRepository interfaces**

```csharp
// src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/IShortCodeGenerator.cs
namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
```

```csharp
// src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/ICourseWaitlistRepository.cs
namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public interface ICourseWaitlistRepository
{
    Task<CourseWaitlist?> GetByIdAsync(Guid id);
    Task<CourseWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<CourseWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(CourseWaitlist waitlist);
}
```

- [ ] **Step 5: Create CourseWaitlist abstract base class**

```csharp
// src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/CourseWaitlist.cs
namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public abstract class CourseWaitlist : Entity
{
    public Guid CourseId { get; protected set; }
    public DateOnly Date { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }

    protected CourseWaitlist() { } // EF
}
```

Note: `Join()` is deferred to Task 3 when the entry hierarchy is ready.

- [ ] **Step 6: Create WalkUpWaitlist subclass**

Extends `CourseWaitlist` with ShortCode, Status, OpenedAt, ClosedAt. Factory method `OpenAsync()` checks for duplicates via repository. `Close()`, `Reopen()` with state guards and events.

Reference the existing `WalkUpWaitlist.cs` for exact behavior but implement under the new namespace `Shadowbrook.Domain.CourseWaitlistAggregate`.

- [ ] **Step 7: Write test class with helper method**

```csharp
// tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/WalkUpWaitlistTests.cs
using NSubstitute;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate.Events;
using Shadowbrook.Domain.CourseWaitlistAggregate.Exceptions;

namespace Shadowbrook.Domain.Tests.CourseWaitlistAggregate;

public class WalkUpWaitlistTests
{
    private readonly IShortCodeGenerator shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository repository = Substitute.For<ICourseWaitlistRepository>();

    public WalkUpWaitlistTests()
    {
        this.shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    private async Task<WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator, this.repository);
    }
}
```

- [ ] **Step 8: Write failing tests for OpenAsync**

Tests:
- `OpenAsync_CreatesOpenWaitlist` — verifies Id, CourseId, Date, ShortCode, Status=Open, ClosedAt=null, raises `WalkUpWaitlistOpened`
- `OpenAsync_WhenOpenWaitlistExists_Throws` — returns existing from repo, expects `WaitlistAlreadyExistsException` with `ExistingStatus == Open`
- `OpenAsync_WhenClosedWaitlistExists_Throws` — close existing, return from repo, expects exception with `ExistingStatus == Closed`

- [ ] **Step 9: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WalkUpWaitlistTests" -v minimal`
Expected: FAIL (tests reference methods not yet fully wired)

- [ ] **Step 10: Make OpenAsync tests pass**

Implement `WalkUpWaitlist.OpenAsync()` until all three tests pass.

- [ ] **Step 11: Write failing tests for Close and Reopen**

Tests:
- `Close_TransitionsToClosedStatus` — Status=Closed, ClosedAt set, raises `WalkUpWaitlistClosed`
- `Close_WhenAlreadyClosed_Throws` — `WaitlistNotOpenException`
- `Reopen_WhenClosed_TransitionsToOpenStatus` — Status=Open, ClosedAt=null, raises `WalkUpWaitlistReopened`
- `Reopen_WhenAlreadyOpen_Throws` — `WaitlistNotClosedException`

- [ ] **Step 12: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WalkUpWaitlistTests" -v minimal`

- [ ] **Step 13: Make Close/Reopen tests pass**

- [ ] **Step 14: Run all tests, verify build**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WalkUpWaitlistTests" -v minimal`
Expected: All pass.

- [ ] **Step 15: Commit**

```bash
git add src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/ tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/
git commit -m "feat: add CourseWaitlist hierarchy with WalkUpWaitlist aggregate"
```

---

## Task 2: GolferWaitlistEntry Hierarchy

**Files:**
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs` (rewrite existing)
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/WalkUpGolferWaitlistEntry.cs`
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/OnlineGolferWaitlistEntry.cs`
- Create: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/Events/WalkUpEntryWindowExtended.cs`
- Modify: `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/IGolferWaitlistEntryRepository.cs`
- Test: `tests/Shadowbrook.Domain.Tests/GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs` (rewrite)
- Reference: existing `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs`

**Context:** `GolferWaitlistEntry` becomes abstract with `IsWalkUp` as TPH discriminator. Remove `IsReady`. Add `WindowStart`/`WindowEnd` (TimeOnly). Two concrete subtypes: `WalkUpGolferWaitlistEntry` (has `ExtendWindow()`) and `OnlineGolferWaitlistEntry` (empty for now). Constructor stays `internal` — called by waitlist's `Join()` method.

- [ ] **Step 1: Create WalkUpEntryWindowExtended event**

```csharp
// src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/Events/WalkUpEntryWindowExtended.cs
```

Shape: `WalkUpEntryWindowExtended { GolferWaitlistEntryId, NewEnd (TimeOnly) }`

- [ ] **Step 2: Rewrite GolferWaitlistEntry as abstract base**

Replace existing file. Key changes from current:
- Make class `abstract`
- Remove `IsReady` property
- Keep `IsWalkUp` as `bool` (TPH discriminator, `protected set`)
- Add `WindowStart` (TimeOnly, `protected set`)
- Add `WindowEnd` (TimeOnly, `protected set`)
- Change constructor to `protected internal` with parameters: `(Guid courseWaitlistId, Guid golferId, int groupSize, bool isWalkUp, TimeOnly windowStart, TimeOnly windowEnd)`
- Keep `Remove()` method and `GolferRemovedFromWaitlist` event

- [ ] **Step 3: Create WalkUpGolferWaitlistEntry**

```csharp
// src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/WalkUpGolferWaitlistEntry.cs
namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class WalkUpGolferWaitlistEntry : GolferWaitlistEntry
{
    private WalkUpGolferWaitlistEntry() { } // EF

    internal WalkUpGolferWaitlistEntry(Guid courseWaitlistId, Guid golferId, int groupSize, TimeOnly windowStart, TimeOnly windowEnd)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: true, windowStart, windowEnd)
    {
    }

    public void ExtendWindow(TimeOnly newEnd)
    {
        WindowEnd = newEnd;
        AddDomainEvent(new Events.WalkUpEntryWindowExtended
        {
            GolferWaitlistEntryId = Id,
            NewEnd = newEnd
        });
    }
}
```

- [ ] **Step 4: Create OnlineGolferWaitlistEntry**

```csharp
// src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/OnlineGolferWaitlistEntry.cs
namespace Shadowbrook.Domain.GolferWaitlistEntryAggregate;

public class OnlineGolferWaitlistEntry : GolferWaitlistEntry
{
    private OnlineGolferWaitlistEntry() { } // EF

    internal OnlineGolferWaitlistEntry(Guid courseWaitlistId, Guid golferId, int groupSize, TimeOnly windowStart, TimeOnly windowEnd)
        : base(courseWaitlistId, golferId, groupSize, isWalkUp: false, windowStart, windowEnd)
    {
    }
}
```

- [ ] **Step 5: Update IGolferWaitlistEntryRepository**

Add `FindEligibleEntries` method signature. Keep existing methods. The parameters should support the matching service needs:

```csharp
Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
    Guid courseId, DateOnly date, TimeOnly teeTime, int maxGroupSize, CancellationToken ct = default);
```

- [ ] **Step 6: Write failing tests for entry hierarchy**

Test file: `tests/Shadowbrook.Domain.Tests/GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs`

Since constructors are `internal`, test through a helper that mimics what `CourseWaitlist.Join()` will do — directly instantiate `WalkUpGolferWaitlistEntry` and `OnlineGolferWaitlistEntry` (tests are in the same solution, add `InternalsVisibleTo` on the Domain project for the test project, or make the constructors `public` for the subtypes since they're sealed by convention).

**Wait — per conventions, no `InternalsVisibleTo`.** Instead, we'll test entry behavior through the `CourseWaitlist.Join()` factory in Task 3. For this task, test only the public methods that don't require construction through the factory:

Tests for `WalkUpGolferWaitlistEntry`:
- `ExtendWindow_UpdatesWindowEnd` — needs a walk-up entry; defer to Task 3.

Tests for base `GolferWaitlistEntry`:
- `Remove_SetsRemovedAt_RaisesEvent` — needs an entry; defer to Task 3.

**Revised approach:** Since entries are created by the waitlist factory and constructors are internal, we'll write entry-specific tests as part of Task 3 after `Join()` is wired up. For now, just ensure the code compiles.

- [ ] **Step 7: Verify build compiles**

Run: `dotnet build shadowbrook.slnx`
Expected: Success (there may be warnings from old code referencing the old `GolferWaitlistEntry` — that's expected and will be cleaned up later).

Note: The old `GolferWaitlistEntry.cs` still exists. The new abstract version replaces it in the same location. If there are compilation conflicts, rename the old file temporarily or delete it (the old tests will break — that's fine, they'll be replaced).

- [ ] **Step 8: Commit**

```bash
git add src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/
git commit -m "feat: add GolferWaitlistEntry hierarchy with walk-up and online subtypes"
```

---

## Task 3: Wire Up Join() on CourseWaitlist + Entry Tests

**Files:**
- Modify: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/CourseWaitlist.cs` — add abstract Join()
- Modify: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/WalkUpWaitlist.cs` — implement Join() override
- Create: `src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/OnlineWaitlist.cs` — implement Join() override
- Modify: `tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/WalkUpWaitlistTests.cs` — add Join tests
- Create: `tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/OnlineWaitlistTests.cs`
- Test: `tests/Shadowbrook.Domain.Tests/GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs` — entry behavior tests

**Context:** `WalkUpWaitlist.Join()` creates a `WalkUpGolferWaitlistEntry` with `WindowStart = now` and `WindowEnd = now + 30min` (TimeOnly, using course local time or UTC — for now use the time portion of `DateTimeOffset.UtcNow`). `OnlineWaitlist.Join()` takes golfer-specified `windowStart` and `windowEnd`.

- [ ] **Step 1: Add abstract Join to CourseWaitlist base**

The base class defines the Join signature. Each subtype provides its own implementation because they create different entry types with different window defaults.

- [ ] **Step 2: Implement WalkUpWaitlist.Join()**

Override in `WalkUpWaitlist`:
- Guard: Status must be Open (throw `WaitlistNotOpenException`)
- Guard: No duplicate active entry (check via `entryRepository.GetActiveByWaitlistAndGolferAsync()`)
- Create `WalkUpGolferWaitlistEntry` with `WindowStart = TimeOnly.FromDateTime(DateTime.UtcNow)`, `WindowEnd = WindowStart + 30 minutes`
- Raise `GolferJoinedWaitlist` event on the waitlist (not the entry)
- Return the entry

- [ ] **Step 3: Implement OnlineWaitlist**

```csharp
// src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/OnlineWaitlist.cs
```

Simple aggregate — no Open/Close lifecycle. Factory: `Create(courseId, date)`. Join override creates `OnlineGolferWaitlistEntry` with golfer-specified window.

- [ ] **Step 4: Write failing Join tests for WalkUpWaitlist**

Add to `WalkUpWaitlistTests.cs`:
- `Join_WhenOpen_CreatesWalkUpEntry` — verify entry type, IsWalkUp=true, GroupSize, CourseWaitlistId, GolferId, WindowStart/WindowEnd set (WindowEnd ~ WindowStart + 30min)
- `Join_WhenOpen_RaisesGolferJoinedWaitlist` — verify event on waitlist
- `Join_WhenClosed_Throws` — `WaitlistNotOpenException`
- `Join_WhenDuplicate_Throws` — `GolferAlreadyOnWaitlistException`

- [ ] **Step 5: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WalkUpWaitlistTests.Join" -v minimal`

- [ ] **Step 6: Make Join tests pass**

- [ ] **Step 7: Write failing OnlineWaitlist tests**

`tests/Shadowbrook.Domain.Tests/CourseWaitlistAggregate/OnlineWaitlistTests.cs`:
- `Join_CreatesOnlineEntry` — verify IsWalkUp=false, custom WindowStart/WindowEnd
- `Join_RaisesGolferJoinedWaitlist`
- `Join_WhenDuplicate_Throws`

- [ ] **Step 8: Run tests to verify they fail, then make them pass**

- [ ] **Step 9: Write entry behavior tests**

`tests/Shadowbrook.Domain.Tests/GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs`:

Create entries via the waitlist factory (instantiate a `WalkUpWaitlist`, call `Join()`):
- `Remove_SetsRemovedAt` — call `entry.Remove()`, verify `RemovedAt` is set
- `Remove_RaisesGolferRemovedFromWaitlist` — verify event
- `ExtendWindow_UpdatesWindowEnd` — cast to `WalkUpGolferWaitlistEntry`, call `ExtendWindow()`, verify
- `ExtendWindow_RaisesWalkUpEntryWindowExtended` — verify event

- [ ] **Step 10: Run all entry tests, make them pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~GolferWaitlistEntryTests" -v minimal`

- [ ] **Step 11: Run full domain test suite**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~CourseWaitlistAggregate|FullyQualifiedName~GolferWaitlistEntryTests" -v minimal`

- [ ] **Step 12: Commit**

```bash
git add src/backend/Shadowbrook.Domain/CourseWaitlistAggregate/ src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/ tests/Shadowbrook.Domain.Tests/
git commit -m "feat: wire up Join() on CourseWaitlist hierarchy with entry creation and tests"
```

---

## Task 4: TeeTimeOpening Aggregate

**Files:**
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/TeeTimeOpening.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/TeeTimeOpeningStatus.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/ITeeTimeOpeningRepository.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningCreated.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningClaimed.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningClaimRejected.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningFilled.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Events/TeeTimeOpeningExpired.cs`
- Create: `src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/Exceptions/OpeningNotAvailableException.cs`
- Test: `tests/Shadowbrook.Domain.Tests/TeeTimeOpeningAggregate/TeeTimeOpeningTests.cs`

**Context:** `TeeTimeOpening` is the waitlist domain's record of an available tee time. Operator creates it with SlotsAvailable. `Claim(bookingId, golferId, groupSize)` is the atomic reservation — decrements SlotsRemaining, raises `TeeTimeOpeningClaimed`. If `SlotsRemaining == 0` after claim, also raises `TeeTimeOpeningFilled`. If not enough slots, raises `TeeTimeOpeningClaimRejected`. `Expire()` transitions to Expired.

- [ ] **Step 1: Create TeeTimeOpeningStatus enum**

```csharp
namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public enum TeeTimeOpeningStatus
{
    Open,
    Filled,
    Expired
}
```

- [ ] **Step 2: Create event records**

Five events:
- `TeeTimeOpeningCreated { OpeningId, CourseId, Date, TeeTime, SlotsAvailable }`
- `TeeTimeOpeningClaimed { OpeningId, BookingId, GolferId, CourseId, Date, TeeTime }`
- `TeeTimeOpeningClaimRejected { OpeningId, BookingId, GolferId }`
- `TeeTimeOpeningFilled { OpeningId }`
- `TeeTimeOpeningExpired { OpeningId }`

- [ ] **Step 3: Create exception**

`OpeningNotAvailableException` — thrown when claiming on a non-Open opening.

- [ ] **Step 4: Create ITeeTimeOpeningRepository**

```csharp
namespace Shadowbrook.Domain.TeeTimeOpeningAggregate;

public interface ITeeTimeOpeningRepository
{
    Task<TeeTimeOpening?> GetByIdAsync(Guid id);
    Task<TeeTimeOpening?> GetActiveByCourseDateTimeAsync(Guid courseId, DateOnly date, TimeOnly teeTime);
    void Add(TeeTimeOpening opening);
}
```

- [ ] **Step 5: Create TeeTimeOpening aggregate**

Properties: Id, CourseId, Date, TeeTime, SlotsAvailable, SlotsRemaining, OperatorOwned (bool), Status (TeeTimeOpeningStatus), CreatedAt, FilledAt?, ExpiredAt?.

Factory: `Create(courseId, date, teeTime, slotsAvailable, operatorOwned)` — sets SlotsRemaining = SlotsAvailable, Status = Open, raises `TeeTimeOpeningCreated`.

Methods:
- `Claim(bookingId, golferId, groupSize)` — guard Status == Open, check SlotsRemaining >= groupSize. If yes: decrement, raise Claimed. If SlotsRemaining == 0: set Filled, raise Filled. If no: raise ClaimRejected.
- `Expire()` — guard Status == Open, set Expired, raise Expired.

- [ ] **Step 6: Write failing tests**

```csharp
// tests/Shadowbrook.Domain.Tests/TeeTimeOpeningAggregate/TeeTimeOpeningTests.cs
```

Tests:
- `Create_SetsPropertiesAndRaisesCreatedEvent`
- `Claim_WhenSlotsAvailable_DecrementsSlotsRemaining`
- `Claim_WhenSlotsAvailable_RaisesClaimedEvent`
- `Claim_WhenLastSlot_RaisesFilledEvent`
- `Claim_WhenGroupTooLarge_RaisesClaimRejectedEvent`
- `Claim_WhenAlreadyFilled_ThrowsOpeningNotAvailableException`
- `Claim_WhenExpired_ThrowsOpeningNotAvailableException`
- `Expire_WhenOpen_TransitionsToExpired`
- `Expire_WhenOpen_RaisesExpiredEvent`
- `Expire_WhenAlreadyFilled_IsIdempotent` (or throws — decide based on what makes sense)

- [ ] **Step 7: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~TeeTimeOpeningTests" -v minimal`

- [ ] **Step 8: Make tests pass incrementally**

Implement Create, then Claim, then Expire. Run tests after each.

- [ ] **Step 9: Run full new domain test suite**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~CourseWaitlistAggregate|FullyQualifiedName~GolferWaitlistEntryTests|FullyQualifiedName~TeeTimeOpeningTests" -v minimal`

- [ ] **Step 10: Commit**

```bash
git add src/backend/Shadowbrook.Domain/TeeTimeOpeningAggregate/ tests/Shadowbrook.Domain.Tests/TeeTimeOpeningAggregate/
git commit -m "feat: add TeeTimeOpening aggregate with Claim/Expire lifecycle"
```

---

## Task 5: WaitlistMatchingService

**Files:**
- Create: `src/backend/Shadowbrook.Domain/WaitlistServices/WaitlistMatchingService.cs`
- Test: `tests/Shadowbrook.Domain.Tests/WaitlistServices/WaitlistMatchingServiceTests.cs`

**Context:** Domain service that finds eligible waitlist entries for an opening. Takes a `TeeTimeOpening`, delegates to `IGolferWaitlistEntryRepository.FindEligibleEntriesAsync()`. The service itself is thin — it extracts the right parameters from the opening and calls the repository. The real filtering logic lives in the repository implementation (infrastructure), but the service defines what "eligible" means.

- [ ] **Step 1: Create WaitlistMatchingService**

```csharp
// src/backend/Shadowbrook.Domain/WaitlistServices/WaitlistMatchingService.cs
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Domain.WaitlistServices;

public class WaitlistMatchingService(IGolferWaitlistEntryRepository entryRepository)
{
    public async Task<List<GolferWaitlistEntry>> FindEligibleEntriesAsync(
        TeeTimeOpening opening, CancellationToken ct = default)
    {
        return await entryRepository.FindEligibleEntriesAsync(
            opening.CourseId,
            opening.Date,
            opening.TeeTime,
            opening.SlotsRemaining,
            ct);
    }
}
```

- [ ] **Step 2: Write tests**

Tests verify the service passes the right parameters to the repository:
- `FindEligibleEntries_PassesOpeningParametersToRepository` — verify repository called with correct courseId, date, teeTime, maxGroupSize = opening.SlotsRemaining
- `FindEligibleEntries_ReturnsRepositoryResults` — mock repository to return a list, verify service returns same list

- [ ] **Step 3: Run tests, make them pass**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~WaitlistMatchingServiceTests" -v minimal`

- [ ] **Step 4: Commit**

```bash
git add src/backend/Shadowbrook.Domain/WaitlistServices/ tests/Shadowbrook.Domain.Tests/WaitlistServices/
git commit -m "feat: add WaitlistMatchingService domain service"
```

---

## Task 6: Checkpoint — Review Domain Model

**No files changed in this task.**

This is a review checkpoint. Before moving to the application layer (handlers, policies, EF configs), stop and verify the domain model makes sense.

- [ ] **Step 1: Run all new domain tests**

Run: `dotnet test tests/Shadowbrook.Domain.Tests/ --filter "FullyQualifiedName~CourseWaitlistAggregate|FullyQualifiedName~GolferWaitlistEntryTests|FullyQualifiedName~TeeTimeOpeningTests|FullyQualifiedName~WaitlistMatchingServiceTests" -v minimal`

All should pass.

- [ ] **Step 2: Review with user**

Present the domain model as built and ask the user if it looks right before proceeding to the application layer. Key questions:
- Do the aggregate boundaries feel right?
- Does the Claim/Expire lifecycle on TeeTimeOpening make sense?
- Any concerns about the entry hierarchy?
- Ready to move to handlers and policies, or need adjustments?

**Do NOT proceed past this task without user confirmation.**

---

## Task 7: EF Core Configuration for New Aggregates

**Files:**
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/CourseWaitlistConfiguration.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/GolferWaitlistEntryConfiguration.cs` (rewrite)
- Create: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/TeeTimeOpeningConfiguration.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs` — update DbSets
- Create: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/CourseWaitlistRepository.cs`
- Create: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/TeeTimeOpeningRepository.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Repositories/GolferWaitlistEntryRepository.cs` — rewrite for new hierarchy + FindEligibleEntries

**Context:** Configure EF Core for the new aggregate hierarchy. TPH for both CourseWaitlist and GolferWaitlistEntry hierarchies. Reference existing configurations (e.g., `WalkUpWaitlistConfiguration.cs`, `GolferWaitlistEntryConfiguration.cs`) for patterns like `HasShadowRowVersion()`, `HasShadowAuditProperties()`, and FK relationships.

- [ ] **Step 1: Create CourseWaitlistConfiguration**

TPH with discriminator. Table: `CourseWaitlists`. Map `WalkUpWaitlist` and `OnlineWaitlist` as subtypes. ShortCode on WalkUpWaitlist only. Unique index `(CourseId, Date)`. FK to Course.

- [ ] **Step 2: Rewrite GolferWaitlistEntryConfiguration**

TPH with `IsWalkUp` as discriminator:
```csharp
builder.HasDiscriminator(e => e.IsWalkUp)
    .HasValue<WalkUpGolferWaitlistEntry>(true)
    .HasValue<OnlineGolferWaitlistEntry>(false);
```
Add `WindowStart` and `WindowEnd` columns. Remove `IsReady`. Keep existing indexes and FKs. Update FK from `WalkUpWaitlist` to `CourseWaitlist`.

- [ ] **Step 3: Create TeeTimeOpeningConfiguration**

Table: `TeeTimeOpenings`. Status as string. OperatorOwned bool. Indexes: `(CourseId, Date, TeeTime)` (not unique — multiple opening cycles possible). FK to Course.

- [ ] **Step 4: Update ApplicationDbContext**

Replace old DbSets with new ones:
- Remove: `TeeTimeRequests`, `TeeTimeSlotFills`, `TeeTimeOfferPolicies`, `TeeTimeRequestExpirationPolicies`
- Add: `TeeTimeOpenings`
- Rename/update: `WalkUpWaitlists` → keep as `CourseWaitlists` (or add both for TPH)

- [ ] **Step 5: Create CourseWaitlistRepository**

Implement `ICourseWaitlistRepository`. `GetOpenByCourseDateAsync` filters by Status == Open. Follow existing repository patterns.

- [ ] **Step 6: Create TeeTimeOpeningRepository**

Implement `ITeeTimeOpeningRepository`. `GetActiveByCourseDateTimeAsync` filters by Status == Open.

- [ ] **Step 7: Rewrite GolferWaitlistEntryRepository**

Update for new types. Add `FindEligibleEntriesAsync` implementation:
- Filter: `RemovedAt IS NULL`
- Filter: `WindowStart <= teeTime && WindowEnd >= teeTime`
- Filter: `GroupSize <= maxGroupSize`
- Filter: no pending offer for this opening (LEFT JOIN on WaitlistOffers where Status == Pending — or pass excluded entry IDs)
- Order: `JoinedAt` ascending
- Return: `List<GolferWaitlistEntry>`

Note: The "no pending offer" filter requires joining against WaitlistOffers. Since the offer aggregate may change, for now filter by entry IDs that don't have a pending offer — this can be a subquery.

- [ ] **Step 8: Register new repositories in DI**

Find where repositories are registered (likely `Program.cs` or a service registration extension) and add new registrations.

- [ ] **Step 9: Verify build**

Run: `dotnet build shadowbrook.slnx`

- [ ] **Step 10: Commit**

```bash
git add src/backend/Shadowbrook.Api/Infrastructure/ src/backend/Shadowbrook.Api/Infrastructure/Data/
git commit -m "feat: add EF Core configuration and repositories for new waitlist aggregates"
```

---

## Task 8+: Application Layer (Handlers, Policies, Endpoints)

**This task is intentionally left as a placeholder.** The exact handler/policy changes depend on:
1. What we learn building the domain (Tasks 1-5)
2. The WaitlistOffer aggregate design (deferred in spec)
3. User feedback at the Task 6 checkpoint

After the Task 6 checkpoint and user confirmation, create detailed sub-tasks for:
- Rewiring existing handlers to use new aggregates
- Creating new policies (TeeTimeOpeningOfferPolicy, WaitlistOfferResponsePolicy, BookingConfirmationPolicy, TeeTimeOpeningExpirationPolicy)
- Updating API endpoints
- Updating validators
- Handler and policy unit tests
- Deleting old code
- Creating fresh EF migration

---

## Task 9: Cleanup and Fresh Migration

**This task runs after all application layer work is complete.**

- [ ] **Step 1: Delete old domain aggregates**

Remove entire folders:
- `src/backend/Shadowbrook.Domain/WalkUpWaitlistAggregate/`
- `src/backend/Shadowbrook.Domain/TeeTimeRequestAggregate/`

- [ ] **Step 2: Delete old tests**

Remove:
- `tests/Shadowbrook.Domain.Tests/WalkUpWaitlistAggregate/`
- `tests/Shadowbrook.Domain.Tests/TeeTimeRequestAggregate/`
- Old handler/policy tests that were replaced

- [ ] **Step 3: Delete old EF configurations**

Remove:
- `WalkUpWaitlistConfiguration.cs`
- `TeeTimeRequestConfiguration.cs`
- `TeeTimeSlotFillConfiguration.cs`
- `TeeTimeOfferPolicyConfiguration.cs`
- `TeeTimeRequestExpirationPolicyConfiguration.cs`

- [ ] **Step 4: Delete all existing migrations**

```bash
rm -rf src/backend/Shadowbrook.Api/Migrations/
```

- [ ] **Step 5: Create fresh migration**

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add InitialCreate --project src/backend/Shadowbrook.Api
```

- [ ] **Step 6: Verify full build and all tests pass**

```bash
dotnet build shadowbrook.slnx && dotnet test shadowbrook.slnx -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: remove old waitlist/teetime domain, create fresh migration"
```
