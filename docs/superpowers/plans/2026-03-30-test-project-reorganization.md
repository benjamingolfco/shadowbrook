# Test Project Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split integration tests into a dedicated project with flow-based scenarios, and reorganize unit tests to mirror the API feature folder structure.

**Architecture:** New `Shadowbrook.Api.IntegrationTests` project owns all integration test infrastructure (TestWebApplicationFactory, Testcontainers, Respawn). Existing `Shadowbrook.Api.Tests` becomes pure unit tests organized by feature. A custom `StepOrderer` guarantees test execution order for dependent scenario tests.

**Tech Stack:** xUnit 2.9, Testcontainers.MsSql, Respawn, Microsoft.AspNetCore.Mvc.Testing, NSubstitute

---

### Task 1: Create the integration test project

**Files:**
- Create: `tests/Shadowbrook.Api.IntegrationTests/Shadowbrook.Api.IntegrationTests.csproj`
- Modify: `shadowbrook.slnx`

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Respawn" Version="7.0.0" />
    <PackageReference Include="Testcontainers.MsSql" Version="4.5.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\backend\Shadowbrook.Api\Shadowbrook.Api.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add project to solution**

```bash
# Edit shadowbrook.slnx to add the new project under /tests/ folder
```

Add this line inside the `<Folder Name="/tests/">` element:

```xml
<Project Path="tests/Shadowbrook.Api.IntegrationTests/Shadowbrook.Api.IntegrationTests.csproj" />
```

- [ ] **Step 3: Verify the project builds**

```bash
dotnet build shadowbrook.slnx
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add tests/Shadowbrook.Api.IntegrationTests/Shadowbrook.Api.IntegrationTests.csproj shadowbrook.slnx
git commit -m "chore: create Shadowbrook.Api.IntegrationTests project"
```

---

### Task 2: Move integration test infrastructure

Move shared infrastructure files from `Shadowbrook.Api.Tests` to the new project. Update namespaces.

**Files:**
- Move: `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs` → `tests/Shadowbrook.Api.IntegrationTests/TestWebApplicationFactory.cs`
- Move: `tests/Shadowbrook.Api.Tests/IntegrationTestAttribute.cs` → `tests/Shadowbrook.Api.IntegrationTests/IntegrationTestAttribute.cs`
- Move: `tests/Shadowbrook.Api.Tests/IntegrationTestCollection.cs` → `tests/Shadowbrook.Api.IntegrationTests/IntegrationTestCollection.cs`
- Move: `tests/Shadowbrook.Api.Tests/TestTimeZones.cs` → `tests/Shadowbrook.Api.IntegrationTests/TestTimeZones.cs`

- [ ] **Step 1: Copy files to new project and update namespaces**

For each file, copy to the new location and change the namespace from `Shadowbrook.Api.Tests` to `Shadowbrook.Api.IntegrationTests`. Also change `internal static class TestTimeZones` to `public static class TestTimeZones` (needs to be accessible from test classes).

`TestWebApplicationFactory.cs` — namespace change only:
```csharp
namespace Shadowbrook.Api.IntegrationTests;
```

`IntegrationTestAttribute.cs` — namespace + discoverer assembly name:
```csharp
namespace Shadowbrook.Api.IntegrationTests;

[TraitDiscoverer("Shadowbrook.Api.IntegrationTests.IntegrationTestDiscoverer", "Shadowbrook.Api.IntegrationTests")]
```

`IntegrationTestCollection.cs` — namespace change only:
```csharp
namespace Shadowbrook.Api.IntegrationTests;
```

`TestTimeZones.cs` — namespace change + make public:
```csharp
namespace Shadowbrook.Api.IntegrationTests;

public static class TestTimeZones
```

- [ ] **Step 2: Delete original files from Shadowbrook.Api.Tests**

Delete `TestWebApplicationFactory.cs`, `IntegrationTestAttribute.cs`, `IntegrationTestCollection.cs`, and `TestTimeZones.cs` from the old project.

- [ ] **Step 3: Remove integration-only packages from Shadowbrook.Api.Tests csproj**

Remove these PackageReferences from `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`:
- `Microsoft.AspNetCore.Mvc.Testing`
- `Respawn`
- `Testcontainers.MsSql`

Keep: `coverlet.collector`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.NET.Test.Sdk`, `NSubstitute`, `WolverineFx.Http`, `xunit`, `WolverineFx`, `xunit.runner.visualstudio`

- [ ] **Step 4: Verify build**

```bash
dotnet build shadowbrook.slnx
```

Expected: Build will fail because integration test files in `Shadowbrook.Api.Tests` still reference `TestWebApplicationFactory`. That's fine — we'll move them in the next tasks.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: move integration test infrastructure to new project"
```

---

### Task 3: Add StepOrderer

**Files:**
- Create: `tests/Shadowbrook.Api.IntegrationTests/StepOrderer.cs`

- [ ] **Step 1: Create the StepOrderer**

```csharp
using System.Text.RegularExpressions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Shadowbrook.Api.IntegrationTests;

public class StepOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(
        IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(tc =>
        {
            var name = tc.TestMethod.Method.Name;
            var match = Regex.Match(name, @"^Step(\d+)_");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        });
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add tests/Shadowbrook.Api.IntegrationTests/StepOrderer.cs
git commit -m "chore: add StepOrderer for ordered integration test execution"
```

---

### Task 4: Add TestSetup helper and shared response DTOs

**Files:**
- Create: `tests/Shadowbrook.Api.IntegrationTests/TestSetup.cs`
- Create: `tests/Shadowbrook.Api.IntegrationTests/ResponseDtos.cs`

- [ ] **Step 1: Create TestSetup helper**

Extract the common setup patterns found across all existing integration tests:

```csharp
using System.Net;
using System.Net.Http.Json;

namespace Shadowbrook.Api.IntegrationTests;

public static class TestSetup
{
    public static async Task<Guid> CreateTenantAsync(HttpClient client, string? orgName = null)
    {
        var response = await client.PostAsJsonAsync("/tenants", new
        {
            OrganizationName = orgName ?? $"Test Tenant {Guid.NewGuid()}",
            ContactName = "Test Contact",
            ContactEmail = "test@tenant.com",
            ContactPhone = "555-0000"
        });
        response.EnsureSuccessStatusCode();

        var tenant = await response.Content.ReadFromJsonAsync<TenantIdResponse>();
        return tenant!.Id;
    }

    public static async Task<(Guid TenantId, Guid CourseId)> CreateCourseAsync(
        HttpClient client,
        string? courseName = null,
        string timeZoneId = "America/Chicago")
    {
        var tenantId = await CreateTenantAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/courses");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        request.Content = JsonContent.Create(new
        {
            Name = courseName ?? $"Test Course {Guid.NewGuid()}",
            TimeZoneId = timeZoneId
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var course = await response.Content.ReadFromJsonAsync<CourseIdResponse>();
        return (tenantId, course!.Id);
    }

    public static async Task<(Guid TenantId, Guid CourseId)> CreateCourseWithSettingsAsync(
        HttpClient client,
        int intervalMinutes = 10,
        string firstTeeTime = "07:00",
        string lastTeeTime = "17:00")
    {
        var (tenantId, courseId) = await CreateCourseAsync(client);

        await client.PutAsJsonAsync($"/courses/{courseId}/tee-time-settings", new
        {
            TeeTimeIntervalMinutes = intervalMinutes,
            FirstTeeTime = firstTeeTime,
            LastTeeTime = lastTeeTime
        });

        return (tenantId, courseId);
    }

    public static async Task<(Guid WaitlistId, string ShortCode)> OpenWaitlistAsync(
        HttpClient client,
        Guid courseId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/courses/{courseId}/walkup-waitlist/open");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<WaitlistResponse>();
        return (body!.Id, body.ShortCode);
    }

    public static async Task<Guid> AddGolferToWaitlistAsync(
        HttpClient client,
        Guid courseId,
        string firstName = "Jane",
        string lastName = "Smith",
        string phone = "555-867-5309",
        int groupSize = 1)
    {
        var response = await client.PostAsJsonAsync(
            $"/courses/{courseId}/walkup-waitlist/entries",
            new { FirstName = firstName, LastName = lastName, Phone = phone, GroupSize = groupSize });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AddGolferResponse>();
        return body!.EntryId;
    }
}
```

- [ ] **Step 2: Create shared response DTOs**

```csharp
namespace Shadowbrook.Api.IntegrationTests;

// Shared response records used across multiple scenario test classes.
// Scenario-specific DTOs can still be defined as private records in the test class.

public record TenantIdResponse(Guid Id);

public record TenantResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantListResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    int CourseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantDetailResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    List<CourseInfo> Courses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CourseInfo(Guid Id, string Name, string? City, string? State);

public record CourseIdResponse(Guid Id);

public record CourseResponse(
    Guid Id,
    string Name,
    string? StreetAddress,
    string? City,
    string? State,
    string? ZipCode,
    string? ContactEmail,
    string? ContactPhone,
    string TimeZoneId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TenantSummary? Tenant = null);

public record TenantSummary(Guid Id, string OrganizationName);

public record WaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WaitlistTodayResponse(
    WaitlistResponse? Waitlist,
    List<WaitlistEntryResponse> Entries);

public record WaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);

public record AddGolferResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    string CourseName);

public record ErrorResponse(string Error);

public record TeeTimeSettingsResponse(
    int TeeTimeIntervalMinutes,
    string FirstTeeTime,
    string LastTeeTime);

public record PricingResponse(decimal FlatRatePrice);

public record TeeSheetResponse(
    Guid CourseId,
    string CourseName,
    List<TeeSheetSlot> Slots);

public record TeeSheetSlot(
    DateTime TeeTime,
    string Status,
    string? GolferName,
    int PlayerCount);

public record VerifyCodeResponse(Guid CourseWaitlistId, string CourseName, string ShortCode);

public record JoinWaitlistResponse(Guid EntryId, Guid GolferId, string GolferName, int Position, string CourseName);

public record CurrentUserResponse(Guid? TenantId);
```

- [ ] **Step 3: Commit**

```bash
git add tests/Shadowbrook.Api.IntegrationTests/TestSetup.cs tests/Shadowbrook.Api.IntegrationTests/ResponseDtos.cs
git commit -m "chore: add TestSetup helper and shared response DTOs"
```

---

### Task 5: Migrate integration tests to new project

Move all integration test files from `Shadowbrook.Api.Tests` to `Shadowbrook.Api.IntegrationTests`. Update namespaces. Replace private response record definitions with shared DTOs where they match. Keep test logic unchanged — this is a structural move, not a rewrite.

**Files to move (update namespace to `Shadowbrook.Api.IntegrationTests`):**
- `SmokeTests.cs`
- `TenantEndpointsTests.cs`
- `TenantClaimMiddlewareTests.cs`
- `TenantCourseIsolationTests.cs`
- `CourseEndpointsTests.cs`
- `CoursePricingTests.cs`
- `TeeTimeSettingsTests.cs`
- `TeeSheetEndpointsTests.cs`
- `FeatureEndpointsTests.cs` — split: move integration test (`GetFeatures_ReturnsOk_WithAllKnownKeys`) to new project; keep unit test (`GetFeatures_ReflectsConfigOverrides`) in old project
- `WalkUpWaitlistEndpointsTests.cs`
- `WalkUpJoinEndpointsTests.cs`
- `WalkUpQrEndpointsTests.cs`
- `RemoveWaitlistEntryTests.cs`
- `Features/Dev/DevSmsEndpointsTests.cs`
- `Repositories/GolferWaitlistEntryRepositoryEligibilityTests.cs`
- `Policies/TeeTimeOpeningOfferPolicyIntegrationTests.cs`

- [ ] **Step 1: Move each file to the new project**

For each file:
1. Copy to `tests/Shadowbrook.Api.IntegrationTests/`
2. Change namespace to `Shadowbrook.Api.IntegrationTests`
3. Remove private response record definitions that exist in `ResponseDtos.cs` (use the shared versions)
4. Keep private records that are test-class-specific and don't exist in the shared DTOs
5. Delete the original file from `Shadowbrook.Api.Tests`

**Special case — `FeatureEndpointsTests.cs`:** This file has one integration test and one unit test. Split it:
- Move `GetFeatures_ReturnsOk_WithAllKnownKeys` to `tests/Shadowbrook.Api.IntegrationTests/FeatureEndpointsTests.cs` with `[Collection("Integration")]` and `[IntegrationTest]`
- Keep `GetFeatures_ReflectsConfigOverrides` in `tests/Shadowbrook.Api.Tests/` (it's a pure unit test that calls the endpoint method directly)

**Special case — `GolferWaitlistEntryRepositoryEligibilityTests.cs`:** Contains `TestTimeProvider` class at the bottom. Move the entire file. The `TestTimeProvider` class can live in the integration test project since it's only used there.

- [ ] **Step 2: Verify build**

```bash
dotnet build shadowbrook.slnx
```

Expected: Build succeeded. The old project should have no references to `TestWebApplicationFactory` or `[IntegrationTest]`.

- [ ] **Step 3: Run all tests**

```bash
dotnet test shadowbrook.slnx
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: migrate integration tests to dedicated project"
```

---

### Task 6: Reorganize unit tests into feature folders

Restructure `Shadowbrook.Api.Tests` to mirror the API feature folder structure.

**Files to move (update namespaces accordingly):**

Handlers → Features/Waitlist/Handlers/:
- `Handlers/BookingCancelledSmsHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/ExpireTeeTimeOpeningHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/FindAndOfferEligibleGolfersHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/MarkOfferStaleHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/TeeTimeOpeningCancelledRejectOffersHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/TeeTimeOpeningFilledRejectOffersHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandlerTests.cs` → `Features/Bookings/Handlers/`
- `Handlers/TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs` → `Features/Waitlist/Handlers/`
- `Handlers/WaitlistOfferAcceptedRemoveFromWaitlistHandlerTests.cs` → `Features/Waitlist/Handlers/`

Policies → Features/Waitlist/Policies/:
- `Policies/TeeTimeOpeningExpirationPolicyTests.cs` → `Features/Waitlist/Policies/`
- `Policies/TeeTimeOpeningOfferPolicyTests.cs` → `Features/Waitlist/Policies/`
- `Policies/WaitlistOfferResponsePolicyTests.cs` → `Features/Waitlist/Policies/`

Validators → Feature folders:
- `Validators/AddGolferToWaitlistRequestValidatorTests.cs` → `Features/Waitlist/Validators/`
- `Validators/CreateTeeTimeOpeningRequestValidatorTests.cs` → `Features/Waitlist/Validators/`
- `Validators/JoinWaitlistRequestValidatorTests.cs` → `Features/Waitlist/Validators/`
- `Validators/CreateCourseRequestValidatorTests.cs` → `Features/Courses/Validators/`
- `Validators/PricingRequestValidatorTests.cs` → `Features/Courses/Validators/`
- `Validators/CreateTenantRequestValidatorTests.cs` → `Features/Tenants/Validators/`
- `Validators/VerifyCodeRequestValidatorTests.cs` → `Features/Waitlist/Validators/`
- `Validators/TeeTimeSettingsRequestValidatorTests.cs` → `Features/TeeSheet/Validators/`

Root-level unit tests:
- `PhoneNormalizerTests.cs` → `Services/PhoneNormalizerTests.cs` (already in correct logical place)
- `CourseTimeTests.cs` → `Services/CourseTimeTests.cs`

Keep in place:
- `Services/FeatureKeysTests.cs` — already correctly placed
- `Services/FeatureServiceTests.cs` — already correctly placed

Remaining from FeatureEndpointsTests split:
- Keep `FeatureEndpointsTests.cs` in root or move to `Features/FeatureFlags/FeatureEndpointsTests.cs`

- [ ] **Step 1: Create the feature folder structure and move files**

For each file, move it to the new location and update its namespace to match:
- `Shadowbrook.Api.Tests.Features.Waitlist.Handlers`
- `Shadowbrook.Api.Tests.Features.Waitlist.Policies`
- `Shadowbrook.Api.Tests.Features.Waitlist.Validators`
- `Shadowbrook.Api.Tests.Features.Bookings.Handlers`
- `Shadowbrook.Api.Tests.Features.Courses.Validators`
- `Shadowbrook.Api.Tests.Features.Tenants.Validators`
- `Shadowbrook.Api.Tests.Features.TeeSheet.Validators`
- `Shadowbrook.Api.Tests.Features.FeatureFlags`
- `Shadowbrook.Api.Tests.Services`

`TestTimeZones` reference: `CourseTimeTests.cs` references `TestTimeZones` which is now in the integration test project. Either:
- Add a `TestTimeZones.cs` to the unit test project too (simple duplication — it's 6 lines), or
- Move it to a shared location

Recommended: duplicate — it's a trivial constant class and avoids a cross-project dependency.

Create `tests/Shadowbrook.Api.Tests/TestTimeZones.cs`:
```csharp
namespace Shadowbrook.Api.Tests;

internal static class TestTimeZones
{
    public const string Chicago = "America/Chicago";
    public const string NewYork = "America/New_York";
    public const string Phoenix = "America/Phoenix";
    public const string Utc = "UTC";
}
```

- [ ] **Step 2: Delete old empty directories**

After moving all files, delete the now-empty `Handlers/`, `Validators/`, `Policies/` directories from the root of `Shadowbrook.Api.Tests`. Also delete `Features/Dev/` (the dev SMS test was an integration test and moved to the new project).

- [ ] **Step 3: Verify build**

```bash
dotnet build shadowbrook.slnx
```

Expected: Build succeeded.

- [ ] **Step 4: Run all unit tests**

```bash
dotnet test tests/Shadowbrook.Api.Tests
```

Expected: All unit tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: reorganize unit tests into feature folders"
```

---

### Task 7: Clean up Shadowbrook.Api.Tests csproj

Remove packages that are no longer needed by the unit test project.

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`

- [ ] **Step 1: Remove unused package references**

If not already removed in Task 2, ensure these are gone from the csproj:
- `Microsoft.AspNetCore.Mvc.Testing` — only needed by integration tests
- `Respawn` — only needed by integration tests
- `Testcontainers.MsSql` — only needed by integration tests

The remaining packages should be:
```xml
<PackageReference Include="coverlet.collector" Version="6.0.4" />
<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.4.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
<PackageReference Include="NSubstitute" Version="5.3.0" />
<PackageReference Include="WolverineFx.Http" Version="5.20.1" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="WolverineFx" Version="5.20.1" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
```

- [ ] **Step 2: Verify build and tests**

```bash
dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Api.Tests
```

Expected: Build succeeded, all unit tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj
git commit -m "chore: remove integration-only packages from unit test project"
```

---

### Task 8: Write integration test conventions rules file

**Files:**
- Create: `.claude/rules/backend/integration-test-conventions.md`

- [ ] **Step 1: Create the rules file**

```markdown
---
paths:
  - "tests/Shadowbrook.Api.IntegrationTests/**/*.cs"
---

# Integration Test Conventions

## Project Structure

Integration tests live in `tests/Shadowbrook.Api.IntegrationTests/`. Unit tests live in `tests/Shadowbrook.Api.Tests/`.

An integration test is anything that needs the real HTTP pipeline, SQL Server (Testcontainers), or `TestWebApplicationFactory`. If it uses NSubstitute or calls methods directly without HTTP, it's a unit test.

## Scenario Pattern

Integration tests are structured as **scenarios** — each file tells the story of a user flow. Tests within a scenario are **dependent**: each step builds on state from the previous one.

### File Naming

Name files for the flow they describe, not the endpoint they hit:
- `GolferJoinsTheWaitlistTests.cs` — not `WaitlistEndpointsTests.cs`
- `OperatorManagesCoursesTests.cs` — not `CourseEndpointsTests.cs`

### Test Method Naming

Use `Step{N}_{Actor}_{Action}` format:

```csharp
[Fact] public async Task Step1_Operator_CreatesTenantAndCourse() { ... }
[Fact] public async Task Step2_Operator_OpensWaitlist() { ... }
[Fact] public async Task Step3_Golfer_JoinsWaitlist() { ... }
```

The numeric prefix makes execution order unambiguous in all test runners.

### Required Attributes

Every scenario class needs all three:

```csharp
[Collection("Integration")]                           // shares TestWebApplicationFactory
[IntegrationTest]                                      // CI category filtering
[TestCaseOrderer(
    "Shadowbrook.Api.IntegrationTests.StepOrderer",
    "Shadowbrook.Api.IntegrationTests")]               // guarantees step order
public class MyScenarioTests(TestWebApplicationFactory factory) : IAsyncLifetime
```

### Shared State

Scenario classes hold mutable instance fields that steps populate and later steps consume:

```csharp
private Guid tenantId;
private Guid courseId;

[Fact]
public async Task Step1_Operator_CreatesTenant()
{
    this.tenantId = await TestSetup.CreateTenantAsync(this.client);
    Assert.NotEqual(Guid.Empty, this.tenantId);
}

[Fact]
public async Task Step2_Operator_CreatesCourse()
{
    // Uses this.tenantId from Step1
    ...
}
```

### Database Reset

`InitializeAsync` does NOT call `ResetDatabaseAsync()` — state accumulates across steps. Each scenario resets the database once in Step 1 before building up state.

```csharp
public Task InitializeAsync() => Task.CompletedTask;
public Task DisposeAsync() => Task.CompletedTask;

[Fact]
public async Task Step1_Setup()
{
    await this.factory.ResetDatabaseAsync();
    // ... create initial state
}
```

## When to Create a New Scenario vs. Extend an Existing One

**New scenario** when:
- The flow involves a different primary actor (golfer vs. operator)
- The flow tests a different top-level feature (waitlist vs. tee sheet)
- The existing scenario would exceed ~15 steps

**Extend existing** when:
- The new test is a natural continuation of the flow
- It uses state already built by earlier steps

## Shared Helpers

Use `TestSetup` for common setup operations (creating tenants, courses, waitlists). Don't duplicate these helpers in test classes.

Use shared DTOs from `ResponseDtos.cs` for common response types. Only define private records for response shapes unique to one scenario.

## Non-Scenario Tests

Some integration tests don't fit the scenario pattern (e.g., repository eligibility queries, middleware behavior). These can use the traditional independent-test pattern with `IAsyncLifetime` + `ResetDatabaseAsync()` per test. Still require `[Collection("Integration")]` and `[IntegrationTest]`.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/rules/backend/integration-test-conventions.md
git commit -m "docs: add integration test conventions rules file"
```

---

### Task 9: Update backend conventions testing section

**Files:**
- Modify: `.claude/rules/backend/backend-conventions.md`

- [ ] **Step 1: Update the Testing section**

Replace the existing `## Testing` section (starting at the `## Testing` heading through the end of the `### Read Models` section that follows, but NOT including `### Read Models`) with the updated version that reflects the new project structure:

```markdown
## Testing

### Testing Pyramid

Unit tests first, integration tests second. Test at the cheapest layer that can prove the behavior.

**Unit tests** (`Shadowbrook.Api.Tests` — no DB, no HTTP, no container):
- Domain aggregates and services — pure behavior, state transitions, event raising
- FluentValidation validators — call `validator.Validate()` directly
- Wolverine message handlers — call `Handle()` with NSubstitute stubs
- Wolverine policies (sagas) — call `Start()` / `Handle()` directly with constructed events
- Infrastructure utilities (e.g., `PhoneNormalizer`, `CourseTime`)

**Integration tests** (`Shadowbrook.Api.IntegrationTests` — TestWebApplicationFactory + SQL Server container):
- Flow-based scenario tests (dependent steps telling a user story)
- DB-dependent behavior (unique constraints, query filters, tenant isolation)
- Middleware behavior (tenant claim, course-exists)
- Repository queries that need real SQL Server (eligibility, filtering)
- Smoke tests (health, OpenAPI)

See `.claude/rules/backend/integration-test-conventions.md` for the scenario test pattern.

**Do not** use integration tests to verify validation rules, null guards, or handler branching logic. Those belong in unit tests.

### Test Organization

```
tests/Shadowbrook.Domain.Tests/
  {Aggregate}Aggregate/              ← domain unit tests

tests/Shadowbrook.Api.Tests/
  Features/
    Waitlist/
      Handlers/                      ← Wolverine handler unit tests
      Policies/                      ← Wolverine policy unit tests
      Validators/                    ← FluentValidation unit tests
    Courses/
      Validators/
    Tenants/
      Validators/
    TeeSheet/
      Validators/
    Bookings/
      Handlers/
  Services/                          ← utility unit tests

tests/Shadowbrook.Api.IntegrationTests/
  *Tests.cs                          ← scenario-based integration tests
  TestWebApplicationFactory.cs       ← shared test server + SQL container
  TestSetup.cs                       ← shared setup helpers
  ResponseDtos.cs                    ← shared response records
  StepOrderer.cs                     ← test execution ordering
```

### NSubstitute for Stubs

Use NSubstitute (`Substitute.For<IProvideAServiceInterface>()`) to stub interfaces in handler unit tests. Use real domain objects (aggregates, entities) — don't substitute those, they have behavior worth exercising. Use `Received()` / `DidNotReceive()` to verify side effects like SMS sends or repository writes.
```

- [ ] **Step 2: Verify the conventions file is valid**

```bash
dotnet build shadowbrook.slnx
```

Expected: Build succeeded (conventions file is markdown, just verifying no accidental changes).

- [ ] **Step 3: Commit**

```bash
git add .claude/rules/backend/backend-conventions.md
git commit -m "docs: update backend conventions for new test project structure"
```

---

### Task 10: Final verification

- [ ] **Step 1: Run all tests across the entire solution**

```bash
dotnet test shadowbrook.slnx
```

Expected: All tests pass.

- [ ] **Step 2: Run only unit tests**

```bash
dotnet test tests/Shadowbrook.Api.Tests
```

Expected: Only unit tests run (no Testcontainers, no SQL Server). Should be fast.

- [ ] **Step 3: Run only integration tests**

```bash
dotnet test tests/Shadowbrook.Api.IntegrationTests
```

Expected: Integration tests run with Testcontainers SQL Server. All pass.

- [ ] **Step 4: Run integration tests by category filter (CI pattern)**

```bash
dotnet test shadowbrook.slnx --filter Category=Integration
```

Expected: Same integration tests run, found via the `[IntegrationTest]` trait.

- [ ] **Step 5: Verify no stale files remain**

```bash
# Should be empty — no integration test files left in old project
find tests/Shadowbrook.Api.Tests -name "*.cs" | xargs grep -l "TestWebApplicationFactory" 2>/dev/null
find tests/Shadowbrook.Api.Tests -name "*.cs" | xargs grep -l "\[IntegrationTest\]" 2>/dev/null
```

Expected: No output (no stale references).

- [ ] **Step 6: Run dotnet format**

```bash
dotnet format shadowbrook.slnx
```

Expected: No formatting issues, or auto-fixed.
