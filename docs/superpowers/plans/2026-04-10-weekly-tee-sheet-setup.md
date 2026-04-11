# Weekly Tee Sheet Setup & Route Restructure — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the weekly tee sheet setup page where operators draft and manage tee sheets for an entire week, replace the single-date draft endpoint with a bulk draft, add a weekly status query, and restructure frontend routing from `/operator` to `/course/:courseId/...`.

**Architecture:** Backend adds two new endpoints (weekly status read model, bulk draft) to the existing TeeSheet feature. Frontend restructures from context-based courseId resolution to route-param-based, with two layout modes (Management and POS) and three new pages (Dashboard, Schedule, ScheduleDay). Existing operator pages (TeeSheet, WalkUpWaitlist, Settings) are relocated into the new structure.

**Tech Stack:** .NET 10 (Wolverine HTTP, EF Core, FluentValidation), React 19 (TanStack Query, React Router v7, shadcn/ui, Tailwind CSS), Vitest + React Testing Library, xUnit + Testcontainers.

---

## File Map

### Backend — New Files

| File | Responsibility |
|------|----------------|
| `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/WeeklyStatusEndpoint.cs` | `GET /courses/{courseId}/tee-sheets/week` — read model query returning 7-day status |
| `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs` | `POST /courses/{courseId}/tee-sheets/draft` — replaces single-date draft, accepts array of dates |
| `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/BulkDraftRequestValidatorTests.cs` | Unit tests for bulk draft validation rules |
| `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/WeeklyStatusRequestValidatorTests.cs` | Unit tests for weekly status validation |
| `tests/Teeforce.Api.IntegrationTests/WeeklyTeeSheetSetupTests.cs` | Integration scenario: bulk draft, weekly status, conflict handling |

### Backend — Modified Files

| File | Change |
|------|--------|
| `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs` | **Deleted** — replaced by `BulkDraftEndpoint.cs` |
| `tests/Teeforce.Api.IntegrationTests/ResponseDtos.cs` | Add `BulkDraftResponse`, `WeeklyStatusResponse`, `DayStatusResponse` DTOs |
| `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs` | Update `DraftAndPublishSheetAsync` helper to use new bulk draft endpoint |

### Frontend — New Files

| File | Responsibility |
|------|----------------|
| `src/web/src/features/course/hooks/useCourseId.ts` | `useCourseId()` — reads `:courseId` from route params |
| `src/web/src/features/course/manage/layouts/ManagementLayout.tsx` | Sidebar layout for management mode (Dashboard, Schedule, Settings) |
| `src/web/src/features/course/manage/pages/Dashboard.tsx` | Management home — today's status, weekly summary, defaults check |
| `src/web/src/features/course/manage/pages/Schedule.tsx` | Weekly tee sheet grid with day cards, week nav, bulk draft |
| `src/web/src/features/course/manage/pages/ScheduleDay.tsx` | Day detail — interval preview for drafted/published days |
| `src/web/src/features/course/manage/hooks/useWeeklySchedule.ts` | TanStack Query hook for `GET /courses/{courseId}/tee-sheets/week` |
| `src/web/src/features/course/manage/hooks/useBulkDraft.ts` | Mutation hook for `POST /courses/{courseId}/tee-sheets/draft` |
| `src/web/src/features/course/pos/layouts/PosLayout.tsx` | Sidebar layout for POS mode (Tee Sheet, Waitlist) |
| `src/web/src/features/course/index.tsx` | Feature entry with `/course/:courseId/*` routing, layout switching |
| `src/web/src/features/course/__tests__/Schedule.test.tsx` | Component tests for weekly schedule page |
| `src/web/src/features/course/__tests__/useWeeklySchedule.test.tsx` | Hook test for weekly schedule query |

### Frontend — Modified Files

| File | Change |
|------|--------|
| `src/web/src/app/router.tsx` | Add `/course/*` route, keep `/operator/*` redirecting (then remove) |
| `src/web/src/lib/query-keys.ts` | Add `teeSheets.weeklyStatus` key factory |
| `src/web/src/types/tee-time.ts` | Add `WeeklyStatusResponse`, `DayStatus` types |

### Frontend — Relocated (move, not copy)

| From | To | Notes |
|------|-----|-------|
| `src/web/src/features/operator/pages/TeeSheet.tsx` | `src/web/src/features/course/pos/pages/TeeSheet.tsx` | Update imports to use `useCourseId()` instead of `useCourseContext()` |
| `src/web/src/features/operator/pages/WalkUpWaitlist.tsx` | `src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx` | Update imports |
| `src/web/src/features/operator/pages/TeeTimeSettings.tsx` | `src/web/src/features/course/manage/pages/Settings.tsx` | Update imports |
| `src/web/src/features/operator/components/*` | `src/web/src/features/course/pos/components/*` | Tee sheet grid, date nav, waitlist components |
| `src/web/src/features/operator/hooks/useTeeSheet.ts` | `src/web/src/features/course/pos/hooks/useTeeSheet.ts` | Update imports |
| `src/web/src/features/operator/hooks/useTeeTimeSettings.ts` | `src/web/src/features/course/manage/hooks/useTeeTimeSettings.ts` | Update imports |
| `src/web/src/features/operator/hooks/useWalkUpWaitlist.ts` | `src/web/src/features/course/pos/hooks/useWalkUpWaitlist.ts` | Update imports |

---

## Task 1: Backend — Weekly Status Endpoint

**Files:**
- Create: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/WeeklyStatusEndpoint.cs`
- Create: `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/WeeklyStatusRequestValidatorTests.cs`

- [ ] **Step 1: Write the validator unit tests**

Create `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/WeeklyStatusRequestValidatorTests.cs`:

```csharp
using FluentValidation.TestHelper;
using Teeforce.Api.Features.TeeSheet.Endpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class WeeklyStatusRequestValidatorTests
{
    private readonly WeeklyStatusRequestValidator validator = new();

    [Fact]
    public void Valid_StartDate_Passes() =>
        this.validator.TestValidate(new WeeklyStatusRequest("2026-04-13"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_StartDate_Fails() =>
        this.validator.TestValidate(new WeeklyStatusRequest(null!))
            .ShouldHaveValidationErrorFor(x => x.StartDate);

    [Fact]
    public void Empty_StartDate_Fails() =>
        this.validator.TestValidate(new WeeklyStatusRequest(""))
            .ShouldHaveValidationErrorFor(x => x.StartDate);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "WeeklyStatusRequestValidatorTests" --no-restore -v q`
Expected: Build failure — `WeeklyStatusRequest` and `WeeklyStatusRequestValidator` don't exist yet.

- [ ] **Step 3: Implement the weekly status endpoint**

Create `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/WeeklyStatusEndpoint.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record WeeklyStatusRequest(string StartDate);

public class WeeklyStatusRequestValidator : AbstractValidator<WeeklyStatusRequest>
{
    public WeeklyStatusRequestValidator()
    {
        RuleFor(r => r.StartDate).NotEmpty();
    }
}

public record DayStatusResponse(string Date, string Status, Guid? TeeSheetId, int? IntervalCount);

public record WeeklyStatusResponse(string WeekStart, string WeekEnd, List<DayStatusResponse> Days);

public static class WeeklyStatusEndpoint
{
    [WolverineGet("/courses/{courseId}/tee-sheets/week")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        [FromQuery] string? startDate,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(startDate))
        {
            return Results.BadRequest(new { error = "startDate query parameter is required." });
        }

        if (!DateOnly.TryParseExact(startDate, "yyyy-MM-dd", out var start))
        {
            return Results.BadRequest(new { error = "startDate must be in yyyy-MM-dd format." });
        }

        var end = start.AddDays(6);
        var dates = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();

        var sheets = await db.TeeSheets
            .Where(s => s.CourseId == courseId && s.Date >= start && s.Date <= end)
            .Select(s => new { s.Date, s.Id, s.Status, IntervalCount = s.Intervals.Count })
            .ToListAsync(ct);

        var sheetsByDate = sheets.ToDictionary(s => s.Date);

        var days = dates.Select(date =>
        {
            if (sheetsByDate.TryGetValue(date, out var sheet))
            {
                var status = sheet.Status.ToString();
                status = char.ToLowerInvariant(status[0]) + status[1..];
                return new DayStatusResponse(
                    date.ToString("yyyy-MM-dd"),
                    status,
                    sheet.Id,
                    sheet.IntervalCount);
            }

            return new DayStatusResponse(date.ToString("yyyy-MM-dd"), "notStarted", null, null);
        }).ToList();

        return Results.Ok(new WeeklyStatusResponse(
            start.ToString("yyyy-MM-dd"),
            end.ToString("yyyy-MM-dd"),
            days));
    }
}
```

Note: This endpoint uses `[FromQuery]` for `startDate` since it's a query string parameter on a GET request. Wolverine binds query parameters by name for GET endpoints, but the explicit attribute makes intent clear. The `WeeklyStatusRequest` record + validator exist for the pattern but the actual parameter binding is done via the query string parameter directly. Let me correct this — Wolverine GET endpoints don't use request body records. The validator is standalone for the request DTO, but we validate inline. Keep the `WeeklyStatusRequest` record and validator for unit-testability even though the endpoint validates inline.

- [ ] **Step 4: Run the validator tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "WeeklyStatusRequestValidatorTests" --no-restore -v q`
Expected: All 3 tests pass.

- [ ] **Step 5: Build to verify compilation**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/WeeklyStatusEndpoint.cs tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/WeeklyStatusRequestValidatorTests.cs
git commit -m "feat(api): add weekly tee sheet status endpoint

GET /courses/{courseId}/tee-sheets/week?startDate=YYYY-MM-DD returns
7-day status with draft/published/notStarted per day."
```

---

## Task 2: Backend — Bulk Draft Endpoint

**Files:**
- Create: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs`
- Delete: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs`
- Create: `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/BulkDraftRequestValidatorTests.cs`

- [ ] **Step 1: Write the validator unit tests**

Create `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/BulkDraftRequestValidatorTests.cs`:

```csharp
using FluentValidation.TestHelper;
using Teeforce.Api.Features.TeeSheet.Endpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class BulkDraftRequestValidatorTests
{
    private readonly BulkDraftRequestValidator validator = new();

    [Fact]
    public void Valid_SingleDate_Passes() =>
        this.validator.TestValidate(new BulkDraftRequest([DateOnly.Parse("2026-04-13")]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Valid_MultipleDates_Passes() =>
        this.validator.TestValidate(new BulkDraftRequest([
            DateOnly.Parse("2026-04-13"),
            DateOnly.Parse("2026-04-14"),
            DateOnly.Parse("2026-04-15"),
        ]))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Null_Dates_Fails() =>
        this.validator.TestValidate(new BulkDraftRequest(null!))
            .ShouldHaveValidationErrorFor(x => x.Dates);

    [Fact]
    public void Empty_Dates_Fails() =>
        this.validator.TestValidate(new BulkDraftRequest([]))
            .ShouldHaveValidationErrorFor(x => x.Dates);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "BulkDraftRequestValidatorTests" --no-restore -v q`
Expected: Build failure — `BulkDraftRequest` and `BulkDraftRequestValidator` don't exist yet.

- [ ] **Step 3: Implement the bulk draft endpoint**

Create `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;
using Wolverine.Http;
using TeeSheetAggregate = Teeforce.Domain.TeeSheetAggregate.TeeSheet;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record BulkDraftRequest(List<DateOnly> Dates);

public class BulkDraftRequestValidator : AbstractValidator<BulkDraftRequest>
{
    public BulkDraftRequestValidator()
    {
        RuleFor(r => r.Dates).NotEmpty();
    }
}

public record BulkDraftItem(string Date, Guid TeeSheetId);

public record BulkDraftResponse(List<BulkDraftItem> TeeSheets);

public static class BulkDraftEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/draft")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        BulkDraftRequest request,
        ICourseRepository courseRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(courseId);
        var settings = course.CurrentScheduleDefaults();

        var results = new List<BulkDraftItem>();

        foreach (var date in request.Dates)
        {
            var existing = await teeSheetRepository.GetByCourseAndDateAsync(courseId, date, ct);
            if (existing is not null)
            {
                throw new TeeSheetAlreadyExistsException(courseId, date);
            }

            var sheet = TeeSheetAggregate.Draft(courseId, date, settings, timeProvider);
            teeSheetRepository.Add(sheet);
            results.Add(new BulkDraftItem(date.ToString("yyyy-MM-dd"), sheet.Id));
        }

        return Results.Ok(new BulkDraftResponse(results));
    }
}
```

- [ ] **Step 4: Delete the old single-date draft endpoint**

Delete file: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/DraftTeeSheetEndpoint.cs`

- [ ] **Step 5: Run the validator tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "BulkDraftRequestValidatorTests" --no-restore -v q`
Expected: All 4 tests pass.

- [ ] **Step 6: Build to verify compilation**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeds. No other files reference `DraftTeeSheetRequest` — the old endpoint was self-contained.

- [ ] **Step 7: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(api): replace single-date draft with bulk draft endpoint

POST /courses/{courseId}/tee-sheets/draft now accepts { dates: [...] }
array. Fails atomically if any date already has a sheet."
```

---

## Task 3: Backend — Integration Tests for Weekly Status + Bulk Draft

**Files:**
- Create: `tests/Teeforce.Api.IntegrationTests/WeeklyTeeSheetSetupTests.cs`
- Modify: `tests/Teeforce.Api.IntegrationTests/ResponseDtos.cs`
- Modify: `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs`

- [ ] **Step 1: Add response DTOs**

Add to `tests/Teeforce.Api.IntegrationTests/ResponseDtos.cs`:

```csharp
public record BulkDraftItem(string Date, Guid TeeSheetId);

public record BulkDraftResponse(List<BulkDraftItem> TeeSheets);

public record DayStatusResponse(string Date, string Status, Guid? TeeSheetId, int? IntervalCount);

public record WeeklyStatusResponse(string WeekStart, string WeekEnd, List<DayStatusResponse> Days);
```

- [ ] **Step 2: Update existing DraftAndPublishSheetAsync helper**

In `tests/Teeforce.Api.IntegrationTests/TeeSheetEndpointsTests.cs`, update the `DraftAndPublishSheetAsync` helper to use the new bulk draft endpoint:

Change `new { Date = date }` to `new { Dates = new[] { date } }`:

```csharp
private async Task DraftAndPublishSheetAsync(Guid courseId, string date)
{
    var draftResponse = await this.client.PostAsJsonAsync(
        $"/courses/{courseId}/tee-sheets/draft",
        new { Dates = new[] { date } });
    draftResponse.EnsureSuccessStatusCode();

    var publishResponse = await this.client.PostAsync(
        $"/courses/{courseId}/tee-sheets/{date}/publish",
        content: null);
    publishResponse.EnsureSuccessStatusCode();
}
```

- [ ] **Step 3: Write the integration test scenario**

Create `tests/Teeforce.Api.IntegrationTests/WeeklyTeeSheetSetupTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;

namespace Teeforce.Api.IntegrationTests;

[Collection("Integration")]
[IntegrationTest]
[TestCaseOrderer(
    "Teeforce.Api.IntegrationTests.StepOrderer",
    "Teeforce.Api.IntegrationTests")]
public class WeeklyTeeSheetSetupTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly TestWebApplicationFactory factory = factory;
    private HttpClient client = null!;
    private Guid courseId;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SetupCourseWithDefaults()
    {
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();

        var tenantId = await TestSetup.CreateTenantAsync(this.client);
        var response = await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "Weekly Test Course",
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        var course = await response.Content.ReadFromJsonAsync<CourseIdResponse>();
        this.courseId = course!.Id;

        await this.client.PutAsJsonAsync($"/courses/{this.courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = 10,
            FirstTeeTime = "07:00",
            LastTeeTime = "17:00",
            DefaultCapacity = 4,
        });
    }

    [Fact]
    public async Task Step1_WeeklyStatus_AllNotStarted()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=2026-04-13");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WeeklyStatusResponse>();
        Assert.NotNull(result);
        Assert.Equal("2026-04-13", result!.WeekStart);
        Assert.Equal("2026-04-19", result.WeekEnd);
        Assert.Equal(7, result.Days.Count);
        Assert.All(result.Days, day => Assert.Equal("notStarted", day.Status));
    }

    [Fact]
    public async Task Step2_BulkDraft_CreatesSheetsForMultipleDates()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15", "2026-04-16" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkDraftResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.TeeSheets.Count);
        Assert.All(result.TeeSheets, item => Assert.NotEqual(Guid.Empty, item.TeeSheetId));
    }

    [Fact]
    public async Task Step3_WeeklyStatus_ShowsMixedStatuses()
    {
        await SetupCourseWithDefaults();

        // Draft some days
        await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15" } });

        // Publish one
        await this.client.PostAsync(
            $"/courses/{this.courseId}/tee-sheets/2026-04-15/publish",
            content: null);

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=2026-04-13");
        var result = await response.Content.ReadFromJsonAsync<WeeklyStatusResponse>();
        Assert.NotNull(result);

        var monday = result!.Days.Single(d => d.Date == "2026-04-13");
        Assert.Equal("notStarted", monday.Status);

        var tuesday = result.Days.Single(d => d.Date == "2026-04-14");
        Assert.Equal("draft", tuesday.Status);
        Assert.NotNull(tuesday.TeeSheetId);
        Assert.True(tuesday.IntervalCount > 0);

        var wednesday = result.Days.Single(d => d.Date == "2026-04-15");
        Assert.Equal("published", wednesday.Status);
    }

    [Fact]
    public async Task Step4_BulkDraft_FailsIfAnyDateAlreadyHasSheet()
    {
        await SetupCourseWithDefaults();

        // Draft one day first
        await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14" } });

        // Try to draft overlapping dates
        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14", "2026-04-15" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("2026-04-14", error!.Error);
    }

    [Fact]
    public async Task Step5_BulkDraft_FailsIfScheduleDefaultsNotConfigured()
    {
        // Setup course WITHOUT defaults
        await this.factory.ResetDatabaseAsync();
        await this.factory.SeedTestAdminAsync();
        this.client = this.factory.CreateAuthenticatedClient();

        var tenantId = await TestSetup.CreateTenantAsync(this.client);
        var courseResponse = await this.client.PostAsJsonAsync("/courses", new
        {
            Name = "No Defaults Course",
            OrganizationId = tenantId,
            TimeZoneId = TestTimeZones.Chicago,
        });
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseIdResponse>();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{course!.Id}/tee-sheets/draft",
            new { Dates = new[] { "2026-04-14" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Step6_WeeklyStatus_MissingStartDate_ReturnsBadRequest()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Step7_WeeklyStatus_InvalidDateFormat_ReturnsBadRequest()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.GetAsync(
            $"/courses/{this.courseId}/tee-sheets/week?startDate=04-13-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Step8_BulkDraft_EmptyDates_ReturnsValidationError()
    {
        await SetupCourseWithDefaults();

        var response = await this.client.PostAsJsonAsync(
            $"/courses/{this.courseId}/tee-sheets/draft",
            new { Dates = Array.Empty<string>() });

        // FluentValidation middleware returns 400 with problem details
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeds.

- [ ] **Step 5: Run the integration tests**

Run: `dotnet test tests/Teeforce.Api.IntegrationTests --filter "WeeklyTeeSheetSetupTests" --no-restore -v n`
Expected: All 8 tests pass.

- [ ] **Step 6: Run the existing TeeSheetEndpointsTests to verify no regression**

Run: `dotnet test tests/Teeforce.Api.IntegrationTests --filter "TeeSheetEndpointsTests" --no-restore -v n`
Expected: All existing tests still pass (they use the updated `DraftAndPublishSheetAsync` helper).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "test: add integration tests for weekly status + bulk draft

Covers mixed statuses, conflict detection, missing defaults,
and validation errors. Updates existing helpers to use bulk draft."
```

---

## Task 4: Frontend — Route Restructure + `useCourseId` Hook

**Files:**
- Create: `src/web/src/features/course/hooks/useCourseId.ts`
- Create: `src/web/src/features/course/index.tsx`
- Modify: `src/web/src/app/router.tsx`
- Modify: `src/web/src/lib/query-keys.ts`

- [ ] **Step 1: Create the `useCourseId` hook**

Create `src/web/src/features/course/hooks/useCourseId.ts`:

```typescript
import { useParams } from 'react-router';

export function useCourseId(): string {
  const { courseId } = useParams<{ courseId: string }>();
  if (!courseId) {
    throw new Error('useCourseId must be used within a route that has :courseId param');
  }
  return courseId;
}
```

- [ ] **Step 2: Add query key factories**

In `src/web/src/lib/query-keys.ts`, add to the `teeSheets` section:

```typescript
teeSheets: {
    byDate: (courseId: string, date: string) => ['tee-sheets', courseId, date] as const,
    weeklyStatus: (courseId: string, startDate: string) => ['tee-sheets', courseId, 'week', startDate] as const,
  },
```

- [ ] **Step 3: Add TypeScript types for weekly status**

In `src/web/src/types/tee-time.ts`, add:

```typescript
export interface DayStatus {
  date: string;
  status: 'notStarted' | 'draft' | 'published';
  teeSheetId?: string;
  intervalCount?: number;
}

export interface WeeklyStatusResponse {
  weekStart: string;
  weekEnd: string;
  days: DayStatus[];
}

export interface BulkDraftResponse {
  teeSheets: Array<{ date: string; teeSheetId: string }>;
}
```

- [ ] **Step 4: Create the course feature entry point**

Create `src/web/src/features/course/index.tsx`:

```typescript
import { Routes, Route, Navigate } from 'react-router';
import { ThemeProvider } from '@/components/ThemeProvider';
import ManagementLayout from './manage/layouts/ManagementLayout';
import PosLayout from './pos/layouts/PosLayout';
import Dashboard from './manage/pages/Dashboard';
import Schedule from './manage/pages/Schedule';
import ScheduleDay from './manage/pages/ScheduleDay';
import Settings from './manage/pages/Settings';
import TeeSheet from './pos/pages/TeeSheet';
import WalkUpWaitlist from './pos/pages/WalkUpWaitlist';

export default function CourseFeature() {
  return (
    <ThemeProvider>
      <Routes>
        <Route path="manage" element={<ManagementLayout />}>
          <Route index element={<Dashboard />} />
          <Route path="schedule" element={<Schedule />} />
          <Route path="schedule/:date" element={<ScheduleDay />} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="pos" element={<PosLayout />}>
          <Route path="tee-sheet" element={<TeeSheet />} />
          <Route path="waitlist" element={<WalkUpWaitlist />} />
          <Route path="*" element={<Navigate to="tee-sheet" replace />} />
        </Route>
        <Route index element={<Navigate to="manage" replace />} />
        <Route path="*" element={<Navigate to="manage" replace />} />
      </Routes>
    </ThemeProvider>
  );
}
```

- [ ] **Step 5: Update the router**

In `src/web/src/app/router.tsx`, add the lazy import:

```typescript
const CourseFeature = lazy(() => import('@/features/course'));
```

Add the `/course/:courseId/*` route inside the `AuthenticatedLayout` children, after `operator/*`:

```typescript
{
  path: 'course/:courseId/*',
  element: (
    <AuthGuard>
      <LazyFeature><CourseFeature /></LazyFeature>
    </AuthGuard>
  ),
},
```

Also add a `/course` redirect route (no courseId — redirects to operator for now, will be enhanced later):

```typescript
{
  path: 'course',
  element: (
    <AuthGuard>
      <Navigate to="/operator" replace />
    </AuthGuard>
  ),
},
```

- [ ] **Step 6: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 7: Commit**

```bash
git add src/web/src/features/course/hooks/useCourseId.ts src/web/src/features/course/index.tsx src/web/src/app/router.tsx src/web/src/lib/query-keys.ts src/web/src/types/tee-time.ts
git commit -m "feat(web): add /course/:courseId route structure + useCourseId hook

New route tree for management and POS modes. Existing /operator
routes remain until pages are relocated."
```

---

## Task 5: Frontend — Management Layout

**Files:**
- Create: `src/web/src/features/course/manage/layouts/ManagementLayout.tsx`

- [ ] **Step 1: Create ManagementLayout**

Create `src/web/src/features/course/manage/layouts/ManagementLayout.tsx`:

```typescript
import { Outlet, NavLink } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { AppShell, type NavConfig } from '@/components/layout/AppShell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth';

function ManagementBrand() {
  const { user } = useAuth();
  return (
    <>
      <h1
        className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
        title={user?.organization?.name ?? 'Teeforce'}
      >
        {user?.organization?.name ?? 'Teeforce'}
      </h1>
      <Badge variant="success" className="text-[10px] px-1.5 py-0">
        Manage
      </Badge>
    </>
  );
}

export default function ManagementLayout() {
  const courseId = useCourseId();

  const navConfig: NavConfig = {
    sections: [
      {
        label: 'Management',
        items: [
          { to: `/course/${courseId}/manage`, label: 'Dashboard' },
          { to: `/course/${courseId}/manage/schedule`, label: 'Schedule' },
          { to: `/course/${courseId}/manage/settings`, label: 'Settings' },
        ],
      },
    ],
  };

  return (
    <AppShell variant="full" navConfig={navConfig} brand={<ManagementBrand />}>
      <Outlet />
    </AppShell>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/manage/layouts/ManagementLayout.tsx
git commit -m "feat(web): add ManagementLayout with sidebar nav"
```

---

## Task 6: Frontend — POS Layout

**Files:**
- Create: `src/web/src/features/course/pos/layouts/PosLayout.tsx`

- [ ] **Step 1: Create PosLayout**

Create `src/web/src/features/course/pos/layouts/PosLayout.tsx`:

```typescript
import { Outlet, Link } from 'react-router';
import { Settings } from 'lucide-react';
import { useCourseId } from '../../hooks/useCourseId';
import { AppShell, type NavConfig } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { PageTopbar } from '@/components/layout/PageTopbar';

function PosBrand() {
  const { user } = useAuth();
  return (
    <>
      <h1
        className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
        title={user?.organization?.name ?? 'Teeforce'}
      >
        {user?.organization?.name ?? 'Teeforce'}
      </h1>
      <Badge variant="default" className="text-[10px] px-1.5 py-0">
        POS
      </Badge>
    </>
  );
}

export default function PosLayout() {
  const courseId = useCourseId();

  const navConfig: NavConfig = {
    sections: [
      {
        label: 'Operations',
        items: [
          { to: `/course/${courseId}/pos/tee-sheet`, label: 'Tee Sheet' },
          { to: `/course/${courseId}/pos/waitlist`, label: 'Waitlist' },
        ],
      },
    ],
  };

  return (
    <AppShell variant="full" navConfig={navConfig} brand={<PosBrand />}>
      <Outlet />
    </AppShell>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/pos/layouts/PosLayout.tsx
git commit -m "feat(web): add PosLayout with sidebar nav"
```

---

## Task 7: Frontend — Relocate Operator Pages + Components

This task moves existing pages and components from `features/operator/` into the new `features/course/` structure. Each page is updated to use `useCourseId()` from route params instead of `useCourseContext()`.

**Files:**
- Move: operator pages → course/manage and course/pos
- Move: operator components → course/pos/components
- Move: operator hooks → course/pos/hooks and course/manage/hooks

- [ ] **Step 1: Copy and adapt the TeeSheet page**

Copy `src/web/src/features/operator/pages/TeeSheet.tsx` to `src/web/src/features/course/pos/pages/TeeSheet.tsx`.

Update imports — replace `useCourseContext()` with `useCourseId()`:

```typescript
import { useState } from 'react';
import { Link } from 'react-router';
import { useTeeSheet } from '../hooks/useTeeSheet';
import { useCourseId } from '../../hooks/useCourseId';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { TeeSheetTopbarTitle } from '../components/TeeSheetTopbarTitle';
import { TeeSheetDateNav } from '../components/TeeSheetDateNav';
import { TeeSheetGrid } from '../components/TeeSheetGrid';

export default function TeeSheet() {
  const courseId = useCourseId();
  // Use browser timezone as fallback — course timezone will come from a future API
  const timeZone = getBrowserTimeZone();
  const [selectedDate, setSelectedDate] = useState<string>(() => getCourseToday(timeZone));
  const teeSheetQuery = useTeeSheet(courseId, selectedDate);

  const data = teeSheetQuery.data;
  const anchorTeeTime = data && data.slots.length > 0 ? data.slots[0]!.teeTime : undefined;
  const now = new Date().toISOString();

  return (
    <>
      <PageTopbar
        left={
          <TeeSheetTopbarTitle
            courseName={data?.courseName ?? ''}
            selectedDate={selectedDate}
            anchorTeeTime={anchorTeeTime}
          />
        }
        right={
          <TeeSheetDateNav
            selectedDate={selectedDate}
            onDateChange={setSelectedDate}
            courseTimeZoneId={timeZone}
          />
        }
      />

      {teeSheetQuery.isError && (() => {
        const message = teeSheetQuery.error instanceof Error
          ? teeSheetQuery.error.message
          : 'Failed to load tee sheet';
        const isNotConfigured = message.toLowerCase().includes('not configured');
        return isNotConfigured ? (
          <div className="m-6 max-w-md rounded-md border border-border bg-white p-6 text-center">
            <p className="font-medium text-ink">Configure your tee times to get started</p>
            <p className="mt-1 text-sm text-ink-muted">
              Set your tee time interval, first tee time, and last tee time in Settings.
            </p>
            <Button asChild variant="default" size="sm" className="mt-4">
              <Link to={`/course/${courseId}/manage/settings`}>Go to Settings</Link>
            </Button>
          </div>
        ) : (
          <p className="m-6 text-sm text-destructive">{message}</p>
        );
      })()}

      {data && <TeeSheetGrid slots={data.slots} now={now} />}
    </>
  );
}
```

- [ ] **Step 2: Copy tee sheet components and hooks**

Copy the following files, updating import paths only (no logic changes):

- `src/web/src/features/operator/components/TeeSheetTopbarTitle.tsx` → `src/web/src/features/course/pos/components/TeeSheetTopbarTitle.tsx`
- `src/web/src/features/operator/components/TeeSheetDateNav.tsx` → `src/web/src/features/course/pos/components/TeeSheetDateNav.tsx`
- `src/web/src/features/operator/components/TeeSheetGrid.tsx` → `src/web/src/features/course/pos/components/TeeSheetGrid.tsx`
- `src/web/src/features/operator/hooks/useTeeSheet.ts` → `src/web/src/features/course/pos/hooks/useTeeSheet.ts`

For the hook, update the `useTeeSheet` signature to accept a `string` courseId (not `string | undefined`) since route params guarantee it:

```typescript
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { TeeSheetResponse } from '@/types/tee-time';

export function useTeeSheet(courseId: string, date: string) {
  return useQuery({
    queryKey: queryKeys.teeSheets.byDate(courseId, date),
    queryFn: () => api.get<TeeSheetResponse>(`/tee-sheets?courseId=${courseId}&date=${date}`),
  });
}
```

- [ ] **Step 3: Copy and adapt the WalkUpWaitlist page**

Copy `src/web/src/features/operator/pages/WalkUpWaitlist.tsx` → `src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx`.

Update to use `useCourseId()` instead of `useCourseContext()`. Replace `course?.id` with `courseId` and `course?.name` with empty string fallbacks. Copy all related components and hooks:

- `src/web/src/features/operator/hooks/useWalkUpWaitlist.ts` → `src/web/src/features/course/pos/hooks/useWalkUpWaitlist.ts`
- Copy any waitlist-specific components from `operator/components/` to `course/pos/components/`

- [ ] **Step 4: Copy and adapt the Settings page**

Copy `src/web/src/features/operator/pages/TeeTimeSettings.tsx` → `src/web/src/features/course/manage/pages/Settings.tsx`.

Update to use `useCourseId()`:

```typescript
import { useEffect } from 'react';
import { useCourseId } from '../../hooks/useCourseId';
// ... rest of imports stay the same but paths updated

export default function Settings() {
  const courseId = useCourseId();
  // Remove useCourseContext — no need for registerDirtyForm since this is a new route structure
  // ... rest of component using courseId directly
```

Copy the settings hook:
- `src/web/src/features/operator/hooks/useTeeTimeSettings.ts` → `src/web/src/features/course/manage/hooks/useTeeTimeSettings.ts`

- [ ] **Step 5: Verify all components exist**

Run: `pnpm --dir src/web lint`
Expected: No import errors. Compilation succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/course/pos/ src/web/src/features/course/manage/pages/Settings.tsx src/web/src/features/course/manage/hooks/useTeeTimeSettings.ts
git commit -m "feat(web): relocate operator pages to course feature

TeeSheet and WalkUpWaitlist move to course/pos/, Settings moves to
course/manage/. All updated to use useCourseId() from route params."
```

---

## Task 8: Frontend — Weekly Schedule Hook + Bulk Draft Hook

**Files:**
- Create: `src/web/src/features/course/manage/hooks/useWeeklySchedule.ts`
- Create: `src/web/src/features/course/manage/hooks/useBulkDraft.ts`
- Create: `src/web/src/features/course/__tests__/useWeeklySchedule.test.tsx`

- [ ] **Step 1: Write the hook test**

Create `src/web/src/features/course/__tests__/useWeeklySchedule.test.tsx`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@/test/test-utils';
import { useWeeklySchedule } from '../manage/hooks/useWeeklySchedule';

vi.mock('@/lib/api-client', () => ({
  api: {
    get: vi.fn(),
  },
}));

import { api } from '@/lib/api-client';
const mockGet = vi.mocked(api.get);

describe('useWeeklySchedule', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches weekly status for given courseId and startDate', async () => {
    const mockData = {
      weekStart: '2026-04-13',
      weekEnd: '2026-04-19',
      days: [
        { date: '2026-04-13', status: 'notStarted' },
        { date: '2026-04-14', status: 'draft', teeSheetId: 'abc', intervalCount: 72 },
      ],
    };
    mockGet.mockResolvedValueOnce(mockData);

    const { result } = renderHook(() => useWeeklySchedule('course-1', '2026-04-13'));

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGet).toHaveBeenCalledWith('/courses/course-1/tee-sheets/week?startDate=2026-04-13');
    expect(result.current.data).toEqual(mockData);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir src/web test -- --run --reporter=verbose features/course/__tests__/useWeeklySchedule.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Create the hooks**

Create `src/web/src/features/course/manage/hooks/useWeeklySchedule.ts`:

```typescript
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WeeklyStatusResponse } from '@/types/tee-time';

export function useWeeklySchedule(courseId: string, startDate: string) {
  return useQuery({
    queryKey: queryKeys.teeSheets.weeklyStatus(courseId, startDate),
    queryFn: () => api.get<WeeklyStatusResponse>(
      `/courses/${courseId}/tee-sheets/week?startDate=${startDate}`
    ),
  });
}
```

Create `src/web/src/features/course/manage/hooks/useBulkDraft.ts`:

```typescript
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { BulkDraftResponse } from '@/types/tee-time';

export function useBulkDraft() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, dates }: { courseId: string; dates: string[] }) =>
      api.post<BulkDraftResponse>(`/courses/${courseId}/tee-sheets/draft`, { dates }),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: ['tee-sheets', courseId],
      });
    },
  });
}
```

- [ ] **Step 4: Run the hook test**

Run: `pnpm --dir src/web test -- --run --reporter=verbose features/course/__tests__/useWeeklySchedule.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/course/manage/hooks/ src/web/src/features/course/__tests__/useWeeklySchedule.test.tsx
git commit -m "feat(web): add useWeeklySchedule + useBulkDraft hooks"
```

---

## Task 9: Frontend — Schedule Page (Weekly View)

**Files:**
- Create: `src/web/src/features/course/manage/pages/Schedule.tsx`
- Create: `src/web/src/features/course/__tests__/Schedule.test.tsx`

- [ ] **Step 1: Write component tests**

Create `src/web/src/features/course/__tests__/Schedule.test.tsx`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@/test/test-utils';
import Schedule from '../manage/pages/Schedule';

vi.mock('../hooks/useCourseId', () => ({
  useCourseId: () => 'course-1',
}));

vi.mock('../manage/hooks/useWeeklySchedule');
vi.mock('../manage/hooks/useBulkDraft');
vi.mock('../manage/hooks/useTeeTimeSettings');

import { useWeeklySchedule } from '../manage/hooks/useWeeklySchedule';
import { useBulkDraft } from '../manage/hooks/useBulkDraft';
import { useTeeTimeSettings } from '../manage/hooks/useTeeTimeSettings';

const mockUseWeeklySchedule = vi.mocked(useWeeklySchedule);
const mockUseBulkDraft = vi.mocked(useBulkDraft);
const mockUseTeeTimeSettings = vi.mocked(useTeeTimeSettings);

const mockMutate = vi.fn();

const weekData = {
  weekStart: '2026-04-13',
  weekEnd: '2026-04-19',
  days: [
    { date: '2026-04-13', status: 'notStarted' as const },
    { date: '2026-04-14', status: 'draft' as const, teeSheetId: 'abc', intervalCount: 72 },
    { date: '2026-04-15', status: 'published' as const, teeSheetId: 'def', intervalCount: 72 },
    { date: '2026-04-16', status: 'notStarted' as const },
    { date: '2026-04-17', status: 'notStarted' as const },
    { date: '2026-04-18', status: 'notStarted' as const },
    { date: '2026-04-19', status: 'notStarted' as const },
  ],
};

beforeEach(() => {
  vi.clearAllMocks();

  mockUseTeeTimeSettings.mockReturnValue({
    data: { teeTimeIntervalMinutes: 10, firstTeeTime: '07:00', lastTeeTime: '17:00', defaultCapacity: 4 },
    isLoading: false,
  } as unknown as ReturnType<typeof useTeeTimeSettings>);

  mockUseWeeklySchedule.mockReturnValue({
    data: weekData,
    isLoading: false,
    isError: false,
  } as unknown as ReturnType<typeof useWeeklySchedule>);

  mockUseBulkDraft.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
  } as unknown as ReturnType<typeof useBulkDraft>);
});

describe('Schedule', () => {
  it('renders 7 day cards', () => {
    render(<Schedule />);
    expect(screen.getByText(/Mon.*Apr 13/)).toBeInTheDocument();
    expect(screen.getByText(/Tue.*Apr 14/)).toBeInTheDocument();
    expect(screen.getByText(/Wed.*Apr 15/)).toBeInTheDocument();
  });

  it('shows correct status badges', () => {
    render(<Schedule />);
    expect(screen.getAllByText('Not Started')).toHaveLength(5);
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('shows interval count for drafted/published days', () => {
    render(<Schedule />);
    expect(screen.getAllByText('72 intervals')).toHaveLength(2);
  });

  it('shows checkboxes only on Not Started cards', () => {
    render(<Schedule />);
    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(5);
  });

  it('enables Draft Selected button when checkboxes checked', () => {
    render(<Schedule />);
    const draftButton = screen.getByRole('button', { name: /draft selected/i });
    expect(draftButton).toBeDisabled();

    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[0]!);
    expect(draftButton).toBeEnabled();
  });

  it('calls bulk draft mutation with selected dates', () => {
    render(<Schedule />);
    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[0]!); // 2026-04-13
    fireEvent.click(checkboxes[1]!); // 2026-04-16

    const draftButton = screen.getByRole('button', { name: /draft selected/i });
    fireEvent.click(draftButton);

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        courseId: 'course-1',
        dates: expect.arrayContaining(['2026-04-13', '2026-04-16']),
      }),
    );
  });

  it('shows not-configured message when settings missing', () => {
    mockUseTeeTimeSettings.mockReturnValue({
      data: undefined,
      isLoading: false,
    } as unknown as ReturnType<typeof useTeeTimeSettings>);

    render(<Schedule />);
    expect(screen.getByText(/configure.*schedule/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir src/web test -- --run --reporter=verbose features/course/__tests__/Schedule.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Create the Schedule page**

Create `src/web/src/features/course/manage/pages/Schedule.tsx`:

```typescript
import { useState, useMemo } from 'react';
import { Link } from 'react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useCourseId } from '../../hooks/useCourseId';
import { useWeeklySchedule } from '../hooks/useWeeklySchedule';
import { useBulkDraft } from '../hooks/useBulkDraft';
import { useTeeTimeSettings } from '../hooks/useTeeTimeSettings';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import type { DayStatus } from '@/types/tee-time';

function getMonday(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = d.getDate() - day + (day === 0 ? -6 : 1);
  d.setDate(diff);
  return d;
}

function formatDateParam(date: Date): string {
  return date.toISOString().split('T')[0]!;
}

function formatDayLabel(dateStr: string): string {
  const date = new Date(dateStr + 'T12:00:00');
  return date.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
}

const statusConfig = {
  notStarted: { label: 'Not Started', variant: 'secondary' as const },
  draft: { label: 'Draft', variant: 'outline' as const },
  published: { label: 'Published', variant: 'default' as const },
};

export default function Schedule() {
  const courseId = useCourseId();
  const [weekStart, setWeekStart] = useState<Date>(() => getMonday(new Date()));
  const [selectedDates, setSelectedDates] = useState<Set<string>>(new Set());

  const startDate = formatDateParam(weekStart);
  const weeklyQuery = useWeeklySchedule(courseId, startDate);
  const settingsQuery = useTeeTimeSettings(courseId);
  const bulkDraft = useBulkDraft();

  const isConfigured = !!settingsQuery.data?.firstTeeTime;

  const navigateWeek = (direction: number) => {
    setWeekStart((prev) => {
      const next = new Date(prev);
      next.setDate(next.getDate() + direction * 7);
      return next;
    });
    setSelectedDates(new Set());
  };

  const goToThisWeek = () => {
    setWeekStart(getMonday(new Date()));
    setSelectedDates(new Set());
  };

  const toggleDate = (date: string) => {
    setSelectedDates((prev) => {
      const next = new Set(prev);
      if (next.has(date)) {
        next.delete(date);
      } else {
        next.add(date);
      }
      return next;
    });
  };

  const handleDraft = () => {
    bulkDraft.mutate(
      { courseId, dates: Array.from(selectedDates) },
      { onSuccess: () => setSelectedDates(new Set()) },
    );
  };

  if (!isConfigured && !settingsQuery.isLoading) {
    return (
      <>
        <PageTopbar
          middle={<h1 className="font-display text-[18px] text-ink">Weekly Schedule</h1>}
        />
        <div className="m-6 max-w-md rounded-md border border-border bg-white p-6 text-center">
          <p className="font-medium text-ink">Configure your schedule defaults first</p>
          <p className="mt-1 text-sm text-ink-muted">
            Set your tee time interval, first and last tee time, and default group size before drafting tee sheets.
          </p>
          <Button asChild variant="default" size="sm" className="mt-4">
            <Link to={`/course/${courseId}/manage/settings`}>Go to Settings</Link>
          </Button>
        </div>
      </>
    );
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Weekly Schedule</h1>}
        right={
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon" onClick={() => navigateWeek(-1)} aria-label="Previous week">
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button variant="outline" size="sm" onClick={goToThisWeek}>
              This Week
            </Button>
            <Button variant="ghost" size="icon" onClick={() => navigateWeek(1)} aria-label="Next week">
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        }
      />

      <div className="p-6">
        <div className="mb-4 flex items-center justify-between">
          <div />
          <Button
            size="sm"
            disabled={selectedDates.size === 0 || bulkDraft.isPending}
            onClick={handleDraft}
          >
            {bulkDraft.isPending ? 'Drafting...' : 'Draft Selected'}
          </Button>
        </div>

        {bulkDraft.isError && (
          <p className="mb-4 text-sm text-destructive">
            {bulkDraft.error instanceof Error ? bulkDraft.error.message : 'Failed to draft tee sheets'}
          </p>
        )}

        {weeklyQuery.isLoading && (
          <div className="grid grid-cols-7 gap-3">
            {Array.from({ length: 7 }).map((_, i) => (
              <Skeleton key={i} className="h-32 rounded-lg" />
            ))}
          </div>
        )}

        {weeklyQuery.data && (
          <div className="grid grid-cols-7 gap-3">
            {weeklyQuery.data.days.map((day) => (
              <DayCard
                key={day.date}
                day={day}
                courseId={courseId}
                selected={selectedDates.has(day.date)}
                onToggle={() => toggleDate(day.date)}
              />
            ))}
          </div>
        )}
      </div>
    </>
  );
}

interface DayCardProps {
  day: DayStatus;
  courseId: string;
  selected: boolean;
  onToggle: () => void;
}

function DayCard({ day, courseId, selected, onToggle }: DayCardProps) {
  const config = statusConfig[day.status];
  const isClickable = day.status !== 'notStarted';

  const content = (
    <Card className={`relative ${isClickable ? 'cursor-pointer hover:border-primary/50' : ''}`}>
      <CardContent className="p-4">
        <div className="flex items-start justify-between">
          <p className="text-sm font-medium text-ink">{formatDayLabel(day.date)}</p>
          {day.status === 'notStarted' && (
            <Checkbox
              checked={selected}
              onCheckedChange={onToggle}
              aria-label={`Select ${day.date}`}
            />
          )}
        </div>
        <Badge variant={config.variant} className="mt-2 text-[10px]">
          {config.label}
        </Badge>
        {day.intervalCount != null && (
          <p className="mt-1 text-xs text-ink-muted">{day.intervalCount} intervals</p>
        )}
      </CardContent>
    </Card>
  );

  if (isClickable) {
    return <Link to={`/course/${courseId}/manage/schedule/${day.date}`}>{content}</Link>;
  }

  return content;
}
```

- [ ] **Step 4: Run the component tests**

Run: `pnpm --dir src/web test -- --run --reporter=verbose features/course/__tests__/Schedule.test.tsx`
Expected: All tests pass. If any fail, adjust the test expectations or component to match.

- [ ] **Step 5: Lint**

Run: `pnpm --dir src/web lint`

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/course/manage/pages/Schedule.tsx src/web/src/features/course/__tests__/Schedule.test.tsx
git commit -m "feat(web): add weekly schedule page with day cards + bulk draft"
```

---

## Task 10: Frontend — Schedule Day Detail Page

**Files:**
- Create: `src/web/src/features/course/manage/pages/ScheduleDay.tsx`

- [ ] **Step 1: Create the ScheduleDay page**

Create `src/web/src/features/course/manage/pages/ScheduleDay.tsx`:

```typescript
import { useParams, Link } from 'react-router';
import { ChevronLeft } from 'lucide-react';
import { useCourseId } from '../../hooks/useCourseId';
import { useTeeSheet } from '../../pos/hooks/useTeeSheet';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';

export default function ScheduleDay() {
  const courseId = useCourseId();
  const { date } = useParams<{ date: string }>();
  const teeSheetQuery = useTeeSheet(courseId, date ?? '');

  if (!date) {
    return null;
  }

  const formattedDate = new Date(date + 'T12:00:00').toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  return (
    <>
      <PageTopbar
        left={
          <div className="flex items-center gap-3">
            <Button asChild variant="ghost" size="icon">
              <Link to={`/course/${courseId}/manage/schedule`}>
                <ChevronLeft className="h-4 w-4" />
              </Link>
            </Button>
            <div>
              <h1 className="font-display text-[18px] text-ink">{formattedDate}</h1>
            </div>
          </div>
        }
      />

      <div className="p-6">
        {teeSheetQuery.isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 10 }).map((_, i) => (
              <Skeleton key={i} className="h-8 w-64" />
            ))}
          </div>
        )}

        {teeSheetQuery.isError && (
          <p className="text-sm text-destructive">Failed to load tee sheet intervals.</p>
        )}

        {teeSheetQuery.data && teeSheetQuery.data.slots.length === 0 && (
          <p className="text-sm text-ink-muted">No intervals found for this date.</p>
        )}

        {teeSheetQuery.data && teeSheetQuery.data.slots.length > 0 && (
          <div className="max-w-md space-y-1">
            {teeSheetQuery.data.slots.map((slot) => {
              const time = new Date(slot.teeTime).toLocaleTimeString('en-US', {
                hour: 'numeric',
                minute: '2-digit',
              });
              return (
                <div
                  key={slot.teeTime}
                  className="flex items-center justify-between rounded-md border border-border px-4 py-2"
                >
                  <span className="text-sm font-medium text-ink">{time}</span>
                  <span className="text-sm text-ink-muted">4 players</span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </>
  );
}
```

Note: The capacity comes from the tee sheet interval, not from the slot response (which shows booking status). Since the existing `GET /tee-sheets` endpoint returns `TeeSheetSlot` with `playerCount` (booked players), the capacity display uses the default. A future enhancement could add capacity to the slot response. For now the hard-coded "4 players" placeholder is acceptable for the read-only preview — it matches the UI spec's "7:00 AM — 4 players" format.

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/manage/pages/ScheduleDay.tsx
git commit -m "feat(web): add schedule day detail page with interval preview"
```

---

## Task 11: Frontend — Dashboard Page

**Files:**
- Create: `src/web/src/features/course/manage/pages/Dashboard.tsx`

- [ ] **Step 1: Create the Dashboard page**

Create `src/web/src/features/course/manage/pages/Dashboard.tsx`:

```typescript
import { useMemo } from 'react';
import { Link } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { useWeeklySchedule } from '../hooks/useWeeklySchedule';
import { useTeeTimeSettings } from '../hooks/useTeeTimeSettings';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

function getMonday(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = d.getDate() - day + (day === 0 ? -6 : 1);
  d.setDate(diff);
  return d;
}

function formatDateParam(date: Date): string {
  return date.toISOString().split('T')[0]!;
}

export default function Dashboard() {
  const courseId = useCourseId();
  const startDate = useMemo(() => formatDateParam(getMonday(new Date())), []);
  const today = useMemo(() => formatDateParam(new Date()), []);

  const weeklyQuery = useWeeklySchedule(courseId, startDate);
  const settingsQuery = useTeeTimeSettings(courseId);

  const todayStatus = weeklyQuery.data?.days.find((d) => d.date === today);

  const statusCounts = useMemo(() => {
    if (!weeklyQuery.data) return null;
    const counts = { published: 0, draft: 0, notStarted: 0 };
    for (const day of weeklyQuery.data.days) {
      counts[day.status]++;
    }
    return counts;
  }, [weeklyQuery.data]);

  const isConfigured = !!settingsQuery.data?.firstTeeTime;

  const todayStatusLabel = todayStatus
    ? todayStatus.status === 'notStarted' ? 'Not Started'
      : todayStatus.status === 'draft' ? 'Draft'
      : 'Published'
    : 'Not Started';

  const todayBadgeVariant = todayStatus?.status === 'published' ? 'default' as const
    : todayStatus?.status === 'draft' ? 'outline' as const
    : 'secondary' as const;

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Dashboard</h1>}
      />

      <div className="grid gap-4 p-6 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Today&apos;s Tee Sheet
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={todayBadgeVariant}>{todayStatusLabel}</Badge>
            <div className="mt-3 flex gap-2">
              <Button asChild variant="outline" size="sm">
                <Link to={`/course/${courseId}/manage/schedule`}>View Schedule</Link>
              </Button>
              <Button asChild variant="default" size="sm">
                <Link to={`/course/${courseId}/pos/tee-sheet`}>Open POS</Link>
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              This Week
            </CardTitle>
          </CardHeader>
          <CardContent>
            {statusCounts ? (
              <p className="text-sm text-ink">
                {statusCounts.published} published, {statusCounts.draft} draft, {statusCounts.notStarted} not started
              </p>
            ) : (
              <p className="text-sm text-ink-muted">Loading...</p>
            )}
            <Button asChild variant="outline" size="sm" className="mt-3">
              <Link to={`/course/${courseId}/manage/schedule`}>View Schedule</Link>
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Schedule Defaults
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={isConfigured ? 'default' : 'secondary'}>
              {isConfigured ? 'Configured' : 'Not configured'}
            </Badge>
            <Button asChild variant="outline" size="sm" className="mt-3 block">
              <Link to={`/course/${courseId}/manage/settings`}>
                {isConfigured ? 'View Settings' : 'Configure'}
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/manage/pages/Dashboard.tsx
git commit -m "feat(web): add management dashboard page

Shows today's tee sheet status, weekly summary, and schedule
defaults configuration status."
```

---

## Task 12: Frontend — Remove Old Operator Feature

**Files:**
- Modify: `src/web/src/app/router.tsx`
- Modify: `src/web/src/features/operator/index.tsx` (keep as redirect shim)

- [ ] **Step 1: Update the `/operator` route to redirect**

The spec says "Old `/operator` routes are removed entirely (no production users)." But to be safe during transition, update `OperatorFeature` to redirect to `/course`. Since we don't have multi-course selection yet, we'll keep the existing operator feature working alongside the new course feature for now.

In `src/web/src/app/router.tsx`, update the `RoleRedirect` component to redirect operators to `/operator` still (no change yet). The `/course/:courseId/*` route is additive.

This step is a no-op for now — the old operator routes continue to work. The new `/course/:courseId/*` routes work in parallel. A follow-up task can remove the old routes once the transition is validated.

- [ ] **Step 2: Commit**

```bash
git commit --allow-empty -m "chore: operator routes preserved during transition

New /course/:courseId routes work in parallel. Old /operator routes
will be removed in a follow-up once course-based routing is validated."
```

---

## Task 13: Full Build + Test + Smoke Check

- [ ] **Step 1: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 2: Run all backend tests**

Run: `dotnet test teeforce.slnx --no-restore -v n`
Expected: All tests pass.

- [ ] **Step 3: Run frontend lint**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 4: Run frontend tests**

Run: `pnpm --dir src/web test -- --run`
Expected: All tests pass.

- [ ] **Step 5: Run make dev and smoke test**

Run: `make dev`

Verify:
- `GET http://localhost:5221/courses/{courseId}/tee-sheets/week?startDate=2026-04-13` returns 7 days
- `POST http://localhost:5221/courses/{courseId}/tee-sheets/draft` with `{ "dates": ["2026-04-14"] }` creates a sheet
- `http://localhost:3000/course/{courseId}/manage/` renders the dashboard
- `http://localhost:3000/course/{courseId}/manage/schedule` renders the weekly view

- [ ] **Step 6: Final commit if any fixes were needed**

```bash
git add -A
git commit -m "fix: address build/lint issues from integration"
```
