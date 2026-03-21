# Integration Test Improvements Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce integration test count by extracting pure validation to unit-tested FluentValidation validators, eliminate redundant tests, then modernize the Testcontainers infrastructure with Respawn and collection fixtures.

**Architecture:** Phase 1 extracts inline validation from endpoint handlers into FluentValidation validators with unit tests, deletes integration tests that only tested validation. Phase 2 refactors `TestWebApplicationFactory` to use `ICollectionFixture`, Respawn for DB reset, and Wolverine tracked sessions instead of polling.

**Tech Stack:** .NET 10, xUnit, FluentValidation, Testcontainers.MsSql, Respawn, WolverineFx tracked sessions

---

## Phase 1: Test Audit — Extract Validators, Remove Redundant Integration Tests

### Current State

**Integration test files (10 files, ~100+ tests using TestWebApplicationFactory):**

| File | Tests | What They Test |
|------|-------|----------------|
| `SmokeTests.cs` | 1 | Health endpoint |
| `TenantEndpointsTests.cs` | 12 | CRUD + validation (5 validation-only tests) |
| `CourseEndpointsTests.cs` | 11 | CRUD + duplicate checks |
| `TenantCourseIsolationTests.cs` | 9 | Multi-tenant isolation |
| `TenantClaimMiddlewareTests.cs` | 3 | Middleware behavior |
| `TeeTimeSettingsTests.cs` | 7 | Settings CRUD + validation (3 validation-only tests) |
| `CoursePricingTests.cs` | 10 | Pricing CRUD + validation (4 validation-only tests) |
| `TeeSheetEndpointsTests.cs` | 9 | Tee sheet generation + validation (3 validation-only tests) |
| `WalkUpWaitlistEndpointsTests.cs` | 20 | Waitlist open/close/entries/requests |
| `WalkUpJoinEndpointsTests.cs` | 15 | Golfer join flow |
| `WaitlistOfferEndpointsTests.cs` | 15 | Offer view/accept/saga chain |

**Unit tests already exist for:**
- 4 FluentValidation validators (AddGolferToWaitlist, CreateWalkUpWaitlistRequest, VerifyCode, JoinWaitlist)
- 7 Wolverine message handlers
- PhoneNormalizer utility
- Domain aggregates (separate project)

### Integration Tests to Convert to Unit Tests

These integration tests spin up the full HTTP stack + SQL Server just to test pure input validation that could be a FluentValidation validator unit test:

**TenantEndpointsTests — 5 tests → extract `CreateTenantRequestValidator`:**
- `PostTenant_WithMissingOrganizationName_ReturnsBadRequest`
- `PostTenant_WithMissingContactName_ReturnsBadRequest`
- `PostTenant_WithMissingContactEmail_ReturnsBadRequest`
- `PostTenant_WithMissingContactPhone_ReturnsBadRequest`
- `PostTenant_WithInvalidEmailFormat_ReturnsBadRequest`

**TeeTimeSettingsTests — 3 tests → extract `TeeTimeSettingsRequestValidator`:**
- `UpdateTeeTimeSettings_InvalidInterval_ReturnsBadRequest`
- `UpdateTeeTimeSettings_FirstAfterLast_ReturnsBadRequest`
- `UpdateTeeTimeSettings_FirstEqualsLast_ReturnsBadRequest`

**CoursePricingTests — 2 tests → extract `PricingRequestValidator`:**
- `UpdatePricing_NegativePrice_ReturnsBadRequest`
- `UpdatePricing_ExcessivelyLargePrice_ReturnsBadRequest`

**CourseEndpointsTests — 1 test → covered by `CreateCourseRequestValidator`:**
- `PostCourse_WithoutName_ReturnsBadRequest`

**NOT extractable — TeeSheetEndpointsTests query parameter validation (3 tests stay):**
- `GetTeeSheet_MissingCourseId_ReturnsBadRequest`, `GetTeeSheet_InvalidDateFormat_ReturnsBadRequest`, `GetTeeSheet_MissingDate_ReturnsBadRequest` — these validate query parameters, not a request body DTO. Wolverine's FluentValidation middleware only validates request body types, so these cannot be extracted without refactoring the endpoint signature. Keep as integration tests.

**Total: ~11 integration tests can become ~11 validator unit tests (same coverage, 100x faster)**

### Integration Tests That Are Redundant

These test the same thing as other integration tests (overlap between test files):

**TenantCourseIsolationTests duplicates CourseEndpointsTests:**
- `CreateCourse_DuplicateName_SameTenant_ReturnsConflict` — same as `CourseEndpointsTests.PostCourse_DuplicateName_ReturnsConflict`
- `CreateCourse_DuplicateName_DifferentTenants_Succeeds` — same as `CourseEndpointsTests.PostCourse_SameNameDifferentTenant_ReturnsCreated`
- `CreateCourse_WithTenantIdInBody_Succeeds` — tests an alternate input path, keep
- `CreateCourse_WithoutTenantHeaderOrBody_ReturnsBadRequest` — tests middleware + binding behavior (TenantId from header), NOT pure input validation. Keep as integration test.

**WaitlistOfferEndpointsTests has near-duplicate saga tests:**
- `AcceptOffer_CreatesBooking` and `AcceptOffer_FillSucceeds_CreatesBookingViaSaga` — test the same thing (both check `db.Bookings.AnyAsync`)
- `AcceptOffer_RemovesGolferFromWaitlist` and `AcceptOffer_FillSucceeds_RemovesFromWaitlistViaSaga` — test the same thing (both check `entry.RemovedAt`)

**Total: ~4 redundant integration tests to delete**

### Integration Tests That Must Stay (need DB/HTTP/middleware)

These test behavior that genuinely requires the full stack:

- **SmokeTests** (1) — validates the app starts and health endpoint works
- **TenantClaimMiddlewareTests** (3) — tests middleware behavior with HTTP headers
- **TenantCourseIsolationTests** (remaining ~6) — tests EF query filter tenant isolation (DB-dependent)
- **TenantEndpointsTests** (remaining ~7) — CRUD, duplicate detection (DB uniqueness), list/detail
- **CourseEndpointsTests** (remaining ~9) — CRUD, duplicate detection (DB uniqueness + collation), tenant info
- **CoursePricingTests** (remaining ~6) — persistence round-trip, not-found for missing courses
- **TeeTimeSettingsTests** (remaining ~4) — persistence round-trip, not-found
- **TeeSheetEndpointsTests** (remaining ~7) — complex query joining bookings + settings, slot generation
- **WalkUpWaitlistEndpointsTests** (all ~20) — state machine transitions, DB-dependent
- **WalkUpJoinEndpointsTests** (all ~15) — multi-step flows with golfer reuse, DB-dependent
- **WaitlistOfferEndpointsTests** (remaining ~11) — saga chain, SMS flow, DB-dependent

---

## Phase 1 Tasks

### Task 1: Extract `CreateTenantRequestValidator` + unit tests

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Tenants/TenantEndpoints.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/CreateTenantRequestValidatorTests.cs`

- [ ] **Step 1: Write the failing unit tests**

```csharp
using FluentValidation.TestHelper;
using static Shadowbrook.Api.Features.Tenants.TenantEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateTenantRequestValidatorTests
{
    private readonly CreateTenantRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        validator.TestValidate(new CreateTenantRequest("Org", "Name", "e@e.com", "555-123-4567"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_OrganizationName_Fails() =>
        validator.TestValidate(new CreateTenantRequest("", "Name", "e@e.com", "555-123-4567"))
            .ShouldHaveValidationErrorFor(x => x.OrganizationName);

    [Fact]
    public void Missing_ContactName_Fails() =>
        validator.TestValidate(new CreateTenantRequest("Org", "", "e@e.com", "555-123-4567"))
            .ShouldHaveValidationErrorFor(x => x.ContactName);

    [Fact]
    public void Missing_ContactEmail_Fails() =>
        validator.TestValidate(new CreateTenantRequest("Org", "Name", "", "555-123-4567"))
            .ShouldHaveValidationErrorFor(x => x.ContactEmail);

    [Fact]
    public void Invalid_ContactEmail_Fails() =>
        validator.TestValidate(new CreateTenantRequest("Org", "Name", "not-email", "555-123-4567"))
            .ShouldHaveValidationErrorFor(x => x.ContactEmail);

    [Fact]
    public void Missing_ContactPhone_Fails() =>
        validator.TestValidate(new CreateTenantRequest("Org", "Name", "e@e.com", ""))
            .ShouldHaveValidationErrorFor(x => x.ContactPhone);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "CreateTenantRequestValidatorTests" -v q`
Expected: FAIL — `CreateTenantRequestValidator` does not exist yet

- [ ] **Step 3: Extract validator from inline checks in TenantEndpoints**

Add inside `TenantEndpoints.cs` (alongside the request record):

```csharp
public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty();
        RuleFor(x => x.ContactName).NotEmpty();
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty();
    }
}
```

Remove the corresponding manual `if` checks from the `CreateTenant` handler method (keep business logic checks like duplicate name).

- [ ] **Step 4: Run unit tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "CreateTenantRequestValidatorTests" -v q`
Expected: PASS

- [ ] **Step 5: Delete the 5 redundant validation integration tests FIRST**

These tests assert on the old `{ "error": "..." }` response format from inline `Results.BadRequest()`. With FluentValidation middleware, the response format changes to RFC 7807 Problem Details, so these tests would fail if run before deletion. Since the unit tests now cover the same validation logic, delete them now.

Remove from `TenantEndpointsTests.cs`:
- `PostTenant_WithMissingOrganizationName_ReturnsBadRequest`
- `PostTenant_WithMissingContactName_ReturnsBadRequest`
- `PostTenant_WithMissingContactEmail_ReturnsBadRequest`
- `PostTenant_WithMissingContactPhone_ReturnsBadRequest`
- `PostTenant_WithInvalidEmailFormat_ReturnsBadRequest`

Remove from `TenantEndpointsTests.cs`:
- `PostTenant_WithMissingOrganizationName_ReturnsBadRequest`
- `PostTenant_WithMissingContactName_ReturnsBadRequest`
- `PostTenant_WithMissingContactEmail_ReturnsBadRequest`
- `PostTenant_WithMissingContactPhone_ReturnsBadRequest`
- `PostTenant_WithInvalidEmailFormat_ReturnsBadRequest`

- [ ] **Step 6: Build and run all tests**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Api.Tests -v q`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: extract CreateTenantRequestValidator, replace 5 integration tests with unit tests"
```

### Task 2: Extract `TeeTimeSettingsRequestValidator` + unit tests

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Courses/CourseEndpoints.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/TeeTimeSettingsRequestValidatorTests.cs`

- [ ] **Step 1: Write failing unit tests**

```csharp
using FluentValidation.TestHelper;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class TeeTimeSettingsRequestValidatorTests
{
    private readonly TeeTimeSettingsRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00")))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_Interval_Fails(int interval) =>
        validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00")))
            .ShouldHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Fact]
    public void FirstTeeTime_After_LastTeeTime_Fails() =>
        validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("18:00"), TimeOnly.Parse("07:00")))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Fact]
    public void FirstTeeTime_Equals_LastTeeTime_Fails() =>
        validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("07:00")))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "TeeTimeSettingsRequestValidatorTests" -v q`

- [ ] **Step 3: Extract validator from inline checks in CourseEndpoints**

```csharp
public class TeeTimeSettingsRequestValidator : AbstractValidator<TeeTimeSettingsRequest>
{
    private static readonly int[] AllowedIntervals = [8, 10, 12];

    public TeeTimeSettingsRequestValidator()
    {
        RuleFor(x => x.TeeTimeIntervalMinutes)
            .Must(i => AllowedIntervals.Contains(i))
            .WithMessage("Interval must be 8, 10, or 12 minutes.");
        RuleFor(x => x.FirstTeeTime)
            .LessThan(x => x.LastTeeTime)
            .WithMessage("First tee time must be before last tee time.");
    }
}
```

Remove the corresponding inline checks from the handler.

- [ ] **Step 4: Run unit tests to verify they pass**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "TeeTimeSettingsRequestValidatorTests" -v q`

- [ ] **Step 5: Delete 3 redundant validation integration tests from TeeTimeSettingsTests.cs**

Delete before running integration suite — response format changes from `{ "error": "..." }` to Problem Details.

Remove:
- `UpdateTeeTimeSettings_InvalidInterval_ReturnsBadRequest`
- `UpdateTeeTimeSettings_FirstAfterLast_ReturnsBadRequest`
- `UpdateTeeTimeSettings_FirstEqualsLast_ReturnsBadRequest`

- [ ] **Step 6: Build and run all tests**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Api.Tests -v q`

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: extract TeeTimeSettingsRequestValidator, replace 3 integration tests with unit tests"
```

### Task 3: Extract `PricingRequestValidator` + unit tests

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Courses/CourseEndpoints.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/PricingRequestValidatorTests.cs`

- [ ] **Step 1: Write failing unit tests**

```csharp
using FluentValidation.TestHelper;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class PricingRequestValidatorTests
{
    private readonly PricingRequestValidator validator = new();

    [Fact]
    public void Valid_Price_Passes() =>
        validator.TestValidate(new PricingRequest(45.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Zero_Price_Passes() =>
        validator.TestValidate(new PricingRequest(0.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Max_Price_Passes() =>
        validator.TestValidate(new PricingRequest(10000.00m))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Negative_Price_Fails() =>
        validator.TestValidate(new PricingRequest(-10.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);

    [Fact]
    public void Excessive_Price_Fails() =>
        validator.TestValidate(new PricingRequest(10001.00m))
            .ShouldHaveValidationErrorFor(x => x.FlatRatePrice);
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Extract validator**

```csharp
public class PricingRequestValidator : AbstractValidator<PricingRequest>
{
    public PricingRequestValidator()
    {
        RuleFor(x => x.FlatRatePrice)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10000);
    }
}
```

Remove inline checks from the handler.

- [ ] **Step 4: Run unit tests to verify they pass**

- [ ] **Step 5: Delete 2 redundant validation integration tests from CoursePricingTests.cs**

Delete before running integration suite — response format changes from `{ "error": "..." }` to Problem Details.

Remove:
- `UpdatePricing_NegativePrice_ReturnsBadRequest`
- `UpdatePricing_ExcessivelyLargePrice_ReturnsBadRequest`

- [ ] **Step 6: Build and run all tests**

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: extract PricingRequestValidator, replace 2 integration tests with unit tests"
```

### Task 4: Extract `CreateCourseRequestValidator` + unit tests

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Courses/CourseEndpoints.cs`
- Create: `tests/Shadowbrook.Api.Tests/Validators/CreateCourseRequestValidatorTests.cs`

- [ ] **Step 1: Write failing unit tests**

```csharp
using FluentValidation.TestHelper;
using static Shadowbrook.Api.Features.Courses.CourseEndpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateCourseRequestValidatorTests
{
    private readonly CreateCourseRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        validator.TestValidate(new CreateCourseRequest("Course Name", null, null, null, null, null, null))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_Name_Fails() =>
        validator.TestValidate(new CreateCourseRequest("", null, null, null, null, null, null))
            .ShouldHaveValidationErrorFor(x => x.Name);
}
```

Note: The `CreateCourseRequest` record parameters should match what exists in `CourseEndpoints.cs`. Adjust constructor args to match.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Extract validator**

```csharp
public class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
```

Remove the `string.IsNullOrWhiteSpace(request.Name)` check from handler. Keep the tenant-exists and duplicate-name checks (those need DB).

- [ ] **Step 4: Run unit tests to verify they pass**

- [ ] **Step 5: Delete redundant integration test from CourseEndpointsTests.cs**

Remove: `PostCourse_WithoutName_ReturnsBadRequest`

Do NOT delete `TenantCourseIsolationTests.CreateCourse_WithoutTenantHeaderOrBody_ReturnsBadRequest` — it tests middleware + header binding behavior, not pure input validation.

- [ ] **Step 6: Build and run all tests**

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: extract CreateCourseRequestValidator, remove redundant validation integration tests"
```

### Task 5: Delete duplicate integration tests

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/TenantCourseIsolationTests.cs`
- Modify: `tests/Shadowbrook.Api.Tests/WaitlistOfferEndpointsTests.cs`

- [ ] **Step 1: Delete duplicates from TenantCourseIsolationTests.cs**

Remove (duplicated in `CourseEndpointsTests`):
- `CreateCourse_DuplicateName_SameTenant_ReturnsConflict`
- `CreateCourse_DuplicateName_DifferentTenants_Succeeds`

- [ ] **Step 2: Delete duplicates from WaitlistOfferEndpointsTests.cs**

Remove (duplicated saga chain tests):
- `AcceptOffer_FillSucceeds_CreatesBookingViaSaga` (duplicate of `AcceptOffer_CreatesBooking`)
- `AcceptOffer_FillSucceeds_RemovesFromWaitlistViaSaga` (duplicate of `AcceptOffer_RemovesGolferFromWaitlist`)

- [ ] **Step 3: Build and run all tests**

Run: `dotnet build shadowbrook.slnx && dotnet test tests/Shadowbrook.Api.Tests -v q`

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test: remove 4 duplicate integration tests"
```

---

## Phase 2: Modernize Test Infrastructure

### Task 6: Add Respawn NuGet package

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`

- [ ] **Step 1: Add Respawn package**

Run: `dotnet add tests/Shadowbrook.Api.Tests package Respawn`

- [ ] **Step 2: Build to verify**

Run: `dotnet build shadowbrook.slnx`

- [ ] **Step 3: Commit**

```bash
git add tests/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj
git commit -m "chore: add Respawn package for test database reset"
```

### Task 7: Refactor TestWebApplicationFactory to ICollectionFixture + Respawn

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/TestWebApplicationFactory.cs`
- Create: `tests/Shadowbrook.Api.Tests/IntegrationTestCollection.cs`

This is the core infrastructure change. The current pattern creates a new database per test class (10 classes = 10 `CREATE DATABASE` + 10 `Migrate()` calls). The new pattern:

1. One container, one database, one migration — shared across all test classes via `ICollectionFixture`
2. Respawn resets data between each test (~50ms vs ~5s per migration)

- [ ] **Step 1: Create the collection fixture definition**

Create `tests/Shadowbrook.Api.Tests/IntegrationTestCollection.cs`:

```csharp
namespace Shadowbrook.Api.Tests;

[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>;
```

- [ ] **Step 2: Refactor TestWebApplicationFactory to use single database + Respawn**

Key changes:
- Remove the per-class database creation (`_databaseName`, `CREATE DATABASE`)
- Start container and create single database in `InitializeAsync`
- Add `ResetDatabaseAsync()` method using Respawn
- Expose the Respawner for test classes to call

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Respawn;
using Shadowbrook.Api.Infrastructure.Data;
using Testcontainers.MsSql;
using Wolverine;

namespace Shadowbrook.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner? respawner;
    private string connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await this.sqlContainer.StartAsync();
        this.connectionString = this.sqlContainer.GetConnectionString();
    }

    public async Task ResetDatabaseAsync()
    {
        if (this.respawner is null)
        {
            await using var conn = new SqlConnection(this.connectionString);
            await conn.OpenAsync();
            // Wolverine tables live in the "wolverine" schema (configured in Program.cs),
            // so SchemasToInclude = ["dbo"] already excludes them. Only EF migration
            // history needs explicit protection.
            this.respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["dbo"],
                TablesToIgnore = [
                    new Respawn.Graph.Table("__EFMigrationsHistory"),
                ]
            });
        }

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync();
        await this.respawner.ResetAsync(connection);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await this.sqlContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", this.connectionString);

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(this.connectionString));

            services.DisableAllExternalWolverineTransports();
            services.RunWolverineInSoloMode();

            services.ConfigureWolverine(opts =>
                opts.DefaultLocalQueue.BufferedInMemory());
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return host;
    }
}
```

- [ ] **Step 3: Update ALL integration test classes**

Replace `IClassFixture<TestWebApplicationFactory>` with `[Collection("Integration")]` on every integration test class:

```csharp
// Before:
public class SmokeTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>

// After:
[Collection("Integration")]
public class SmokeTests(TestWebApplicationFactory factory)
```

Add `IAsyncLifetime` implementation to test classes that need clean state:

```csharp
[Collection("Integration")]
public class TenantEndpointsTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ... existing tests ...
}
```

Files to update:
- `SmokeTests.cs`
- `TenantEndpointsTests.cs`
- `CourseEndpointsTests.cs`
- `TenantCourseIsolationTests.cs`
- `TenantClaimMiddlewareTests.cs`
- `TeeTimeSettingsTests.cs`
- `CoursePricingTests.cs`
- `TeeSheetEndpointsTests.cs`
- `WalkUpWaitlistEndpointsTests.cs`
- `WalkUpJoinEndpointsTests.cs`
- `WaitlistOfferEndpointsTests.cs`

- [ ] **Step 4: Build**

Run: `dotnet build shadowbrook.slnx`

- [ ] **Step 5: Run all tests and fix any issues**

Run: `dotnet test tests/Shadowbrook.Api.Tests -v q`

Note: Since tests now share one DB with Respawn reset, some tests that relied on an empty DB or counted absolute results (like `GetAllTenants_WithNoTenants_ReturnsEmptyArray`) may need adjustment. Tests should use unique data or filter results rather than assuming an empty database. Investigate any failures.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: migrate to ICollectionFixture + Respawn for shared container and fast DB reset"
```

### Task 8: Replace SMS polling with Wolverine tracked sessions (WaitlistOfferEndpointsTests)

**Files:**
- Modify: `tests/Shadowbrook.Api.Tests/WaitlistOfferEndpointsTests.cs`

The current `GetOfferTokenFromSmsAsync` uses a `Task.Delay` polling loop (50ms intervals, 10s timeout). Wolverine's tracked sessions provide a deterministic alternative.

- [ ] **Step 1: Research the tracked session API**

Check: `dotnet add tests/Shadowbrook.Api.Tests package WolverineFx.Testing` (if not already referenced)

The key API is `IHost.InvokeMessageAndWaitAsync()` or `IHost.TrackActivity()`. Since we're triggering via HTTP (not direct message invoke), we need `TrackActivity()` to wrap the HTTP call.

Note: This step requires investigation. If Wolverine's tracked sessions don't easily wrap HTTP requests made via `HttpClient`, the polling approach may be the pragmatic choice. Investigate before committing to a rewrite.

- [ ] **Step 2: If tracked sessions work with HTTP requests, refactor GetOfferTokenFromSmsAsync**

Replace the polling loop with:

```csharp
private async Task<Guid> GetOfferTokenFromSmsAsync(string phone)
{
    // Wait for all Wolverine message processing to complete
    var host = factory.Services.GetRequiredService<IHost>();
    await host.WaitForNonStaleData(); // or similar API

    var offerMessage = this.smsService.GetByPhone(phone)
        .LastOrDefault(m => m.Body.Contains("Claim your spot:"));
    Assert.NotNull(offerMessage);

    // ... extract token ...
}
```

- [ ] **Step 3: If tracked sessions don't work, document why and keep polling**

The polling approach is functional. If tracked sessions can't wrap HTTP-initiated flows, add a comment explaining the trade-off and move on.

- [ ] **Step 4: Run WaitlistOfferEndpointsTests**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "WaitlistOfferEndpointsTests" -v q`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: replace SMS polling with Wolverine tracked sessions in offer tests"
```

---

## Summary

| Phase | Action | Integration Tests Removed | Unit Tests Added |
|-------|--------|--------------------------|------------------|
| 1.1 | Extract CreateTenantRequestValidator | -5 | +6 |
| 1.2 | Extract TeeTimeSettingsRequestValidator | -3 | +4 |
| 1.3 | Extract PricingRequestValidator | -2 | +5 |
| 1.4 | Extract CreateCourseRequestValidator | -1 | +2 |
| 1.5 | Delete duplicates | -4 | 0 |
| 2.1 | Add Respawn | 0 | 0 |
| 2.2 | ICollectionFixture + Respawn | 0 | 0 |
| 2.3 | Tracked sessions (if feasible) | 0 | 0 |
| **Total** | | **-15** | **+17** |

**Net result:** ~15 fewer integration tests (each ~5-30s), ~17 new unit tests (each ~5ms), shared container startup (one-time ~15s vs per-class), Respawn resets (~50ms vs migration ~5s).

## Review Notes

Issues identified and fixed during plan review:
1. **Record names corrected** — actual records are `TeeTimeSettingsRequest` and `PricingRequest`, not `UpdateTeeTimeSettingsRequest`/`UpdatePricingRequest`
2. **Allowed intervals corrected** — actual whitelist is `[8, 10, 12]`, not `[6, 7, 8, 9, 10, 12]`
3. **TeeSheet query param tests kept** — FluentValidation middleware only validates request body DTOs, not query parameters. 3 TeeSheet validation tests must stay as integration tests.
4. **Wolverine schema clarified** — Wolverine tables are in `wolverine` schema (not `dbo`), so `SchemasToInclude = ["dbo"]` already excludes them. Simplified `TablesToIgnore`.
5. **TenantId header test kept** — `CreateCourse_WithoutTenantHeaderOrBody_ReturnsBadRequest` tests middleware binding, not input validation.
6. **Response format ordering** — Integration tests that assert on `{ "error": "..." }` must be deleted before running the suite, since FluentValidation changes the response format to Problem Details.
