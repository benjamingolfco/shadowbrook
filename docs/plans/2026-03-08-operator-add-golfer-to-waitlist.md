# Operator Add Golfer to Walk-Up Waitlist — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow course operators to add a golfer to the walk-up waitlist on their behalf via `POST /courses/{courseId}/walkup-waitlist/entries`.

**Architecture:** New operator-scoped endpoint in `WalkUpWaitlistEndpoints.cs` that reuses the existing golfer lookup-or-create pattern from `WalkUpJoinEndpoints.cs`. Adds a `GroupSize` column to `GolferWaitlistEntries` to capture party size. No domain model changes — `GolferWaitlistEntry` is an API-layer model.

**Tech Stack:** .NET 10 minimal API, EF Core 10, FluentValidation, xUnit integration tests with SQLite in-memory.

---

### Task 1: Add GroupSize to GolferWaitlistEntry Model

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Models/GolferWaitlistEntry.cs`
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/GolferWaitlistEntryConfiguration.cs`

**Step 1: Add `GroupSize` property to GolferWaitlistEntry**

In `src/backend/Shadowbrook.Api/Models/GolferWaitlistEntry.cs`, add after line 20 (`IsReady`):

```csharp
public int GroupSize { get; set; } = 1;
```

**Step 2: Configure GroupSize column in EF**

In `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/GolferWaitlistEntryConfiguration.cs`, add after the `GolferPhone` property config (line 16):

```csharp
builder.Property(e => e.GroupSize).HasDefaultValue(1);
```

**Step 3: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

**Step 4: Commit**

```
feat: add GroupSize property to GolferWaitlistEntry
```

---

### Task 2: Add EF Migration for GroupSize

**Step 1: Generate migration**

Run: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations add AddGroupSizeToGolferWaitlistEntry --project src/backend/Shadowbrook.Api`

**Step 2: Inspect migration**

Read the generated migration file. Verify it adds a `GroupSize` int column with default value 1 to the `GolferWaitlistEntries` table.

**Step 3: Build to verify**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

**Step 4: Run existing tests to verify no regressions**

Run: `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "WalkUpWaitlistEndpointsTests" --no-restore`
Expected: All existing tests pass

**Step 5: Commit**

```
feat: add migration for GroupSize column
```

---

### Task 3: Write Failing Integration Tests for Operator Add Golfer Endpoint

**Files:**
- Modify: `src/backend/Shadowbrook.Api.Tests/WalkUpWaitlistEndpointsTests.cs`

Add tests to the existing `WalkUpWaitlistEndpointsTests` class. The endpoint will be `POST /courses/{courseId}/walkup-waitlist/entries`.

**Step 1: Add helper method**

Add to the helpers section at the bottom of the test class:

```csharp
private async Task<HttpResponseMessage> PostAddGolferAsync(Guid courseId, object body) =>
    await this.client.PostAsJsonAsync($"/courses/{courseId}/walkup-waitlist/entries", body);
```

Also add this response record with the other test records:

```csharp
private record AddGolferToWaitlistResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    int Position,
    string CourseName);
```

**Step 2: Write test — valid request returns 201**

```csharp
// -------------------------------------------------------------------------
// POST /entries (operator adds golfer)
// -------------------------------------------------------------------------

[Fact]
public async Task AddGolfer_ValidRequest_Returns201()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 2
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
    Assert.NotNull(body);
    Assert.Equal("John Smith", body!.GolferName);
    Assert.Equal(2, body.GroupSize);
    Assert.Equal(1, body.Position);
    Assert.NotEqual(Guid.Empty, body.EntryId);
}
```

**Step 3: Write test — no open waitlist returns 404**

```csharp
[Fact]
public async Task AddGolfer_NoOpenWaitlist_Returns404()
{
    var (_, courseId) = await CreateTestCourseAsync();

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 1
    });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

**Step 4: Write test — duplicate phone returns 409 with position**

```csharp
[Fact]
public async Task AddGolfer_DuplicatePhone_Returns409WithPosition()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 2
    });

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 2
    });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
}
```

**Step 5: Write test — existing golfer reuses record**

```csharp
[Fact]
public async Task AddGolfer_ExistingGolfer_ReusesGolferRecord()
{
    var (_, courseId1) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId1);

    // First add on course 1
    var r1 = await PostAddGolferAsync(courseId1, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 1
    });
    Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

    // Second add on course 2 with same phone
    var (_, courseId2) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId2);

    var r2 = await PostAddGolferAsync(courseId2, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 1
    });
    Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
}
```

**Step 6: Write test — group size defaults to 1**

```csharp
[Fact]
public async Task AddGolfer_NoGroupSize_DefaultsTo1()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "Jane",
        LastName = "Doe",
        Phone = "555-987-6543"
    });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
    Assert.Equal(1, body!.GroupSize);
}
```

**Step 7: Write test — invalid phone returns 400**

```csharp
[Fact]
public async Task AddGolfer_InvalidPhone_Returns400()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "123",
        GroupSize = 1
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

**Step 8: Write test — missing name returns 400**

```csharp
[Fact]
public async Task AddGolfer_MissingName_Returns400()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 1
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

**Step 9: Write test — group size out of range returns 400**

```csharp
[Fact]
public async Task AddGolfer_GroupSizeOutOfRange_Returns400()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var r0 = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 0
    });
    Assert.Equal(HttpStatusCode.BadRequest, r0.StatusCode);

    var r5 = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4568",
        GroupSize = 5
    });
    Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);
}
```

**Step 10: Write test — multiple golfers get correct positions**

```csharp
[Fact]
public async Task AddGolfer_MultipleGolfers_CorrectPositions()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    var r1 = await PostAddGolferAsync(courseId, new
    {
        FirstName = "Alice",
        LastName = "A",
        Phone = "555-111-1111",
        GroupSize = 2
    });
    var b1 = await r1.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
    Assert.Equal(1, b1!.Position);

    var r2 = await PostAddGolferAsync(courseId, new
    {
        FirstName = "Bob",
        LastName = "B",
        Phone = "555-222-2222",
        GroupSize = 1
    });
    var b2 = await r2.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
    Assert.Equal(2, b2!.Position);

    var r3 = await PostAddGolferAsync(courseId, new
    {
        FirstName = "Carol",
        LastName = "C",
        Phone = "555-333-3333",
        GroupSize = 4
    });
    var b3 = await r3.Content.ReadFromJsonAsync<AddGolferToWaitlistResponse>();
    Assert.Equal(3, b3!.Position);
}
```

**Step 11: Write test — closed waitlist returns 404**

```csharp
[Fact]
public async Task AddGolfer_ClosedWaitlist_Returns404()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);
    await PostCloseAsync(courseId);

    var response = await PostAddGolferAsync(courseId, new
    {
        FirstName = "John",
        LastName = "Smith",
        Phone = "555-123-4567",
        GroupSize = 1
    });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

**Step 12: Run tests to verify they fail**

Run: `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "WalkUpWaitlistEndpointsTests.AddGolfer" --no-restore`
Expected: All new tests FAIL (endpoint doesn't exist yet — 404)

**Step 13: Commit**

```
test: add integration tests for operator add golfer to waitlist endpoint
```

---

### Task 4: Implement the Operator Add Golfer Endpoint

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpWaitlistEndpoints.cs`

**Step 1: Add route mapping**

In `MapWalkUpWaitlistEndpoints`, add after the `/requests` mapping (line 18):

```csharp
group.MapPost("/entries", AddGolferToWaitlist).AddValidationFilter();
```

**Step 2: Add the endpoint handler**

Add this method to the `WalkUpWaitlistEndpoints` class:

```csharp
private static async Task<IResult> AddGolferToWaitlist(
    Guid courseId,
    AddGolferToWaitlistRequest request,
    IWalkUpWaitlistRepository repo,
    ApplicationDbContext db)
{
    var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);
    if (waitlist is null)
    {
        return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
    }

    var courseName = await db.Courses
        .Where(c => c.Id == courseId)
        .Select(c => c.Name)
        .FirstAsync();

    // Duplicate prevention
    var existingEntry = await db.GolferWaitlistEntries
        .Where(e => e.CourseWaitlistId == waitlist.Id && e.GolferPhone == normalizedPhone && e.RemovedAt == null)
        .FirstOrDefaultAsync();

    if (existingEntry is not null)
    {
        var existingPosition = await CalculatePositionAsync(db, waitlist.Id, existingEntry.JoinedAt);
        return Results.Conflict(new { error = "This golfer is already on the waitlist.", position = existingPosition });
    }

    // Golfer lookup-or-create
    var golfer = await db.Golfers
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

    if (golfer is null)
    {
        var now = DateTimeOffset.UtcNow;
        golfer = new Golfer
        {
            Id = Guid.CreateVersion7(),
            Phone = normalizedPhone!,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Golfers.Add(golfer);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.Golfers.Remove(golfer);
            golfer = await db.Golfers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

            if (golfer is null)
            {
                return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
            }
        }
    }

    var entryNow = DateTimeOffset.UtcNow;
    var entry = new GolferWaitlistEntry(Guid.CreateVersion7())
    {
        CourseWaitlistId = waitlist.Id,
        GolferId = golfer.Id,
        GolferName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
        GolferPhone = normalizedPhone!,
        IsWalkUp = true,
        IsReady = true,
        GroupSize = request.GroupSize ?? 1,
        JoinedAt = entryNow,
        CreatedAt = entryNow,
        UpdatedAt = entryNow
    };

    db.GolferWaitlistEntries.Add(entry);
    await db.SaveChangesAsync();

    var position = await CalculatePositionAsync(db, waitlist.Id, entry.JoinedAt);

    entry.RaiseJoinedEvent(courseName, position);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/courses/{courseId}/walkup-waitlist/entries/{entry.Id}",
        new AddGolferToWaitlistResponse(
            entry.Id, entry.GolferName, entry.GolferPhone, entry.GroupSize, position, courseName));
}
```

**Step 3: Add the CalculatePositionAsync helper**

Copy the same pattern from `WalkUpJoinEndpoints`:

```csharp
private static async Task<int> CalculatePositionAsync(ApplicationDbContext db, Guid courseWaitlistId, DateTimeOffset joinedAt)
{
    var joinedAtValues = await db.GolferWaitlistEntries
        .Where(e => e.CourseWaitlistId == courseWaitlistId && e.RemovedAt == null)
        .Select(e => e.JoinedAt)
        .ToListAsync();

    return joinedAtValues.Count(t => t <= joinedAt);
}
```

**Step 4: Add request/response records and validator**

Add at the bottom of `WalkUpWaitlistEndpoints.cs`:

```csharp
public record AddGolferToWaitlistRequest(
    string FirstName,
    string LastName,
    string Phone,
    int? GroupSize);

public class AddGolferToWaitlistRequestValidator : AbstractValidator<AddGolferToWaitlistRequest>
{
    public AddGolferToWaitlistRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Phone)
            .Must(PhoneNormalizer.IsValid).WithMessage("A valid US phone number is required.");

        RuleFor(x => x.GroupSize)
            .InclusiveBetween(1, 4)
            .When(x => x.GroupSize.HasValue)
            .WithMessage("Group size must be between 1 and 4.");
    }
}

public record AddGolferToWaitlistResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    int Position,
    string CourseName);
```

**Step 5: Add required using statements**

Add to the top of `WalkUpWaitlistEndpoints.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Api.Models;
```

**Step 6: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

**Step 7: Run all tests**

Run: `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "WalkUpWaitlistEndpointsTests" --no-restore`
Expected: All tests pass (both existing and new)

**Step 8: Commit**

```
feat: add operator endpoint to add golfer to walk-up waitlist (#210)
```

---

### Task 5: Update GetToday to Include Entries and GroupSize

The existing `GetToday` endpoint returns an empty entries list (hardcoded `new List<WalkUpWaitlistEntryResponse>()`). Now that operators can add golfers, it should return actual entries with group size.

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Endpoints/WalkUpWaitlistEndpoints.cs`

**Step 1: Write a failing test**

Add to `WalkUpWaitlistEndpointsTests.cs`:

```csharp
[Fact]
public async Task Today_WithEntries_ReturnsEntriesWithGroupSize()
{
    var (_, courseId) = await CreateTestCourseAsync();
    await PostOpenAsync(courseId);

    await PostAddGolferAsync(courseId, new
    {
        FirstName = "Alice",
        LastName = "A",
        Phone = "555-111-1111",
        GroupSize = 3
    });

    var response = await GetTodayAsync(courseId);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<WalkUpWaitlistTodayResponse>();
    Assert.NotNull(body);
    Assert.Single(body!.Entries);
    Assert.Equal("Alice A", body.Entries[0].GolferName);
}
```

Update the existing `WalkUpWaitlistEntryResponse` test record to include `GroupSize`:

```csharp
private record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "WalkUpWaitlistEndpointsTests.Today_WithEntries" --no-restore`
Expected: FAIL (entries list is empty)

**Step 3: Update GetToday handler to query actual entries**

Replace the `GetToday` method in `WalkUpWaitlistEndpoints.cs`:

```csharp
private static async Task<IResult> GetToday(
    Guid courseId,
    IWalkUpWaitlistRepository repo,
    ApplicationDbContext db)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var waitlist = await repo.GetByCourseDateAsync(courseId, today);

    var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;

    var entries = waitlist is not null
        ? await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
            .OrderBy(e => e.JoinedAt)
            .Select(e => new WalkUpWaitlistEntryResponse(e.Id, e.GolferName, e.GroupSize, e.JoinedAt))
            .ToListAsync()
        : new List<WalkUpWaitlistEntryResponse>();

    return Results.Ok(new WalkUpWaitlistTodayResponse(waitlistResponse, entries));
}
```

**Step 4: Update the `WalkUpWaitlistEntryResponse` record**

```csharp
public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);
```

**Step 5: Run all tests**

Run: `dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "WalkUpWaitlistEndpointsTests" --no-restore`
Expected: All tests pass

**Step 6: Commit**

```
feat: return actual waitlist entries with group size from today endpoint (#210)
```
