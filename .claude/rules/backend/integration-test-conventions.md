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
