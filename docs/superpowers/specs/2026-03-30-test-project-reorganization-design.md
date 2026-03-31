# Test Project Reorganization

**Date:** 2026-03-30
**Branch:** bug/timeout-saga-correlation

## Problem

The `Shadowbrook.Api.Tests` project mixes unit tests and integration tests. Integration test files are dumped at the project root with no structure. Helper methods (`CreateTestTenantAsync`, etc.) are duplicated across classes. As integration tests grow, the project becomes harder to navigate.

## Design

### New project: `Shadowbrook.Api.IntegrationTests`

A dedicated test project at `tests/Shadowbrook.Api.IntegrationTests/` for all integration tests (anything that needs `TestWebApplicationFactory`, SQL Server, or the real HTTP pipeline).

**Dependencies:** Testcontainers, Respawn, `Microsoft.AspNetCore.Mvc.Testing`, xUnit, project reference to `Shadowbrook.Api`.

**Moves from `Shadowbrook.Api.Tests`:**
- `TestWebApplicationFactory.cs`
- `IntegrationTestAttribute.cs` + `IntegrationTestDiscoverer`
- `IntegrationTestCollection.cs`
- `TestTimeZones.cs`
- All integration test files (everything decorated with `[Collection("Integration")]`)

#### Flow-based scenario tests

Integration tests are structured as **scenarios** — each file tells a story of a user flow from start to finish. Tests within a scenario are **dependent**: each step builds on state from the previous one.

**Naming:** Files are named for the flow they describe, not the endpoint they hit.

| File | Flow |
|------|------|
| `GolferJoinsTheWaitlistTests.cs` | Tenant → course → waitlist open → golfer joins → opening posted → offer sent |
| `OperatorManagesCoursesTests.cs` | Tenant → create course → duplicate name → get by ID → list |
| `TenantIsolationTests.cs` | Two tenants → verify data doesn't leak |

**Test method naming:** Step-numbered with descriptive names.

```csharp
[Fact] public async Task Step1_Operator_CreatesTenantAndCourse() { ... }
[Fact] public async Task Step2_Operator_OpensWaitlist() { ... }
[Fact] public async Task Step3_Golfer_JoinsWaitlist() { ... }
[Fact] public async Task Step4_Operator_PostsTeeTimeOpening() { ... }
[Fact] public async Task Step5_System_SendsOfferToGolfer() { ... }
```

**Shared state:** Scenario classes hold mutable instance fields (IDs, responses) that each step populates and later steps consume.

```csharp
[Collection("Integration")]
[IntegrationTest]
[TestCaseOrderer(
    "Shadowbrook.Api.IntegrationTests.StepOrderer",
    "Shadowbrook.Api.IntegrationTests")]
public class GolferJoinsTheWaitlistTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = factory.CreateClient();

    private Guid tenantId;
    private Guid courseId;
    private Guid openingId;

    public Task InitializeAsync() => Task.CompletedTask; // no per-test reset — state accumulates
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Step1_Operator_CreatesTenantAndCourse() { ... }
}
```

**Note on `IAsyncLifetime`:** `InitializeAsync` does NOT call `ResetDatabaseAsync()` — state accumulates across steps. Database reset happens once at class construction via the collection fixture, not between tests.

**Ordering:** A custom `StepOrderer` (implements `ITestCaseOrderer`) sorts test methods by the `StepN_` prefix, parsing the integer. This makes ordering explicit and resilient to xUnit version changes or alphabetical sorting in test runners.

```csharp
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

#### Shared helpers

Common setup operations (create tenant, create course with settings, add golfer to waitlist) are consolidated into a static `TestSetup` helper class instead of being duplicated in every scenario.

```csharp
public static class TestSetup
{
    public static async Task<Guid> CreateTenantAsync(HttpClient client) { ... }
    public static async Task<(Guid TenantId, Guid CourseId)> CreateCourseWithSettingsAsync(HttpClient client) { ... }
    public static async Task OpenWaitlistAsync(HttpClient client, Guid courseId) { ... }
    public static async Task<Guid> AddGolferToWaitlistAsync(HttpClient client, Guid courseId, ...) { ... }
}
```

Response DTOs used by multiple scenarios live in a shared `ResponseDtos.cs` file rather than being redefined as private records in each test class.

#### Database reset strategy

Each scenario class resets the database once before its steps run. Since tests within a class are dependent and share state, there is no per-test reset.

The `TestWebApplicationFactory` keeps `ResetDatabaseAsync()` for this purpose. Scenario classes call it in a class-level setup (first step or `IAsyncLifetime.InitializeAsync` at the class level — only for the first scenario in a collection run, handled by the fixture).

### Reorganized `Shadowbrook.Api.Tests` — pure unit tests

After moving integration tests out, reorganize the remaining unit tests to mirror the API feature folder structure:

```
tests/Shadowbrook.Api.Tests/
  Features/
    Waitlist/
      Handlers/
        BookingCancelledSmsHandlerTests.cs
        ExpireTeeTimeOpeningHandlerTests.cs
        FindAndOfferEligibleGolfersHandlerTests.cs
        MarkOfferStaleHandlerTests.cs
        TeeTimeOpeningCancelledCancelBookingsHandlerTests.cs
        TeeTimeOpeningCancelledRejectOffersHandlerTests.cs
        TeeTimeOpeningFilledRejectOffersHandlerTests.cs
        TeeTimeOpeningSlotsClaimedCreateConfirmedBookingHandlerTests.cs
        TeeTimeOpeningSlotsClaimedSmsHandlerTests.cs
        WaitlistOfferAcceptedRemoveFromWaitlistHandlerTests.cs
      Policies/
        TeeTimeOpeningExpirationPolicyTests.cs
        TeeTimeOpeningOfferPolicyTests.cs
        WaitlistOfferResponsePolicyTests.cs
      Validators/
        AddGolferToWaitlistRequestValidatorTests.cs
        CreateTeeTimeOpeningRequestValidatorTests.cs
        JoinWaitlistRequestValidatorTests.cs
    Courses/
      Validators/
        CreateCourseRequestValidatorTests.cs
        PricingRequestValidatorTests.cs
    Tenants/
      Validators/
        CreateTenantRequestValidatorTests.cs
        VerifyCodeRequestValidatorTests.cs
    TeeSheet/
      Validators/
        TeeTimeSettingsRequestValidatorTests.cs
  Services/
    FeatureKeysTests.cs
    FeatureServiceTests.cs
    PhoneNormalizerTests.cs
```

**Handler test placement follows the same consumer rule as production code** — handler tests live under the feature that consumes the event, matching where the handler itself lives in the API project.

### Rules file: `.claude/rules/backend/integration-test-conventions.md`

A new rules file documenting:
- Scenario test pattern (flow-based, dependent, step-numbered)
- When to create a new scenario vs extend an existing one
- Naming conventions (`Step{N}_{Actor}_{Action}`)
- Shared state pattern (mutable instance fields)
- Database reset strategy (once per scenario class, not per test)
- `TestSetup` helper usage
- `[Collection("Integration")]` + `[IntegrationTest]` + `[TestCaseOrderer]` required on every scenario class
- Project boundary: unit tests in `Shadowbrook.Api.Tests`, integration tests in `Shadowbrook.Api.IntegrationTests`

### What doesn't change

- `Shadowbrook.Domain.Tests` — already well-organized by aggregate, stays as-is
- `TestWebApplicationFactory` internals — same Testcontainers + Respawn setup, just moves projects
- xUnit as the test framework
- NSubstitute for unit test stubs

## Migration plan

1. Create the new `Shadowbrook.Api.IntegrationTests` project with dependencies
2. Move infrastructure files (`TestWebApplicationFactory`, `IntegrationTestAttribute`, `IntegrationTestCollection`, `TestTimeZones`)
3. Add `StepOrderer` and `TestSetup` helper
4. Add shared `ResponseDtos.cs`
5. Migrate existing integration tests into flow-based scenario files, consolidating related tests
6. Reorganize remaining unit tests in `Shadowbrook.Api.Tests` into feature folders
7. Update namespaces to match new folder structure
8. Write `.claude/rules/backend/integration-test-conventions.md`
9. Update `.claude/rules/backend/backend-conventions.md` testing section to reference new structure
10. Verify all tests pass
