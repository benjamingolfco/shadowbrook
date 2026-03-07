# Walk-Up Waitlist DDD Refactor — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Introduce a rich domain model for the walk-up waitlist, restructure the backend under `src/backend/`, and establish DDD patterns for the project.

**Architecture:** Separate `Shadowbrook.Domain` project (zero dependencies) owns the aggregate root, child entities, value objects, domain events, and repository interfaces. `Shadowbrook.Api` contains infrastructure (EF, event dispatch, repository implementations) and thin HTTP endpoints. Domain exceptions bubble up through a global exception handler.

**Tech Stack:** .NET 10, EF Core 10, xUnit

**Design doc:** `docs/plans/2026-03-06-walkup-waitlist-ddd-design.md`

---

### Task 1: Move projects under `src/backend/`

Move `src/api/` to `src/backend/Shadowbrook.Api/` and `tests/api/` to `src/backend/Shadowbrook.Api.Tests/`. Update all references.

**Step 1: Move the directories**

```bash
mkdir -p src/backend
git mv src/api src/backend/Shadowbrook.Api
git mv tests/api src/backend/Shadowbrook.Api.Tests
```

If `tests/` is now empty, remove it: `rmdir tests`

**Step 2: Update `shadowbrook.slnx`**

Replace with:

```xml
<Solution>
  <Folder Name="/src/backend/">
    <Project Path="src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj" />
  </Folder>
  <Folder Name="/src/backend/tests/">
    <Project Path="src/backend/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj" />
  </Folder>
</Solution>
```

**Step 3: Update test project reference**

In `src/backend/Shadowbrook.Api.Tests/Shadowbrook.Api.Tests.csproj`, change:

```xml
<ProjectReference Include="..\..\src\api\Shadowbrook.Api.csproj" />
```

to:

```xml
<ProjectReference Include="..\Shadowbrook.Api\Shadowbrook.Api.csproj" />
```

**Step 4: Update `Makefile`**

```makefile
api: ## Run the .NET API natively (requires SQL Server: make db)
	dotnet run --project src/backend/Shadowbrook.Api

test: ## Run all backend tests
	dotnet test src/backend/Shadowbrook.Api.Tests
```

**Step 5: Update `docker-compose.yml`**

Change `dockerfile: src/api/Dockerfile` to `dockerfile: src/backend/Shadowbrook.Api/Dockerfile`.
Change `./src/api:/app` to `./src/backend/Shadowbrook.Api:/app`.

**Step 6: Update `Dockerfile`**

The Dockerfile is at `src/backend/Shadowbrook.Api/Dockerfile` now. Update the `COPY` lines:

```dockerfile
# Dev stage
COPY src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj ./
...
COPY src/backend/Shadowbrook.Api/ ./

# Build stage
COPY src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj ./
...
COPY src/backend/Shadowbrook.Api/ ./
```

**Step 7: Update CI workflows**

In `.github/workflows/pr.yml` and `.github/workflows/deploy-dev.yml`, update paths-filter:

```yaml
api:
  - 'src/backend/**'
```

In `.github/workflows/deploy-api.yml`, update the Dockerfile path:

```yaml
--file src/backend/Shadowbrook.Api/Dockerfile \
```

**Step 8: Update `.claude/CLAUDE.md`**

Update all references:
- Project structure: `src/api/` → `src/backend/Shadowbrook.Api/`, `tests/api/` → `src/backend/Shadowbrook.Api.Tests/`
- EF commands: `--project src/api` → `--project src/backend/Shadowbrook.Api`
- Test commands: `dotnet test tests/api` → `dotnet test src/backend/Shadowbrook.Api.Tests`

**Step 9: Update `.claude/rules/backend/` files**

In `api-conventions.md`, update paths:
```yaml
paths:
  - "src/backend/Shadowbrook.Api/**/*.cs"
```

In `ef-migrations.md`, update paths:
```yaml
paths:
  - "src/backend/Shadowbrook.Api/Migrations/**"
  - "src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs"
```

Update all `--project src/api` references to `--project src/backend/Shadowbrook.Api`.
Update `src/api/Migrations/` references to `src/backend/Shadowbrook.Api/Migrations/`.

In `backend-developer.md` (`.claude/agents/`), update test path:
```
dotnet test src/backend/Shadowbrook.Api.Tests/ --filter "FullyQualifiedName~{TestClass}"
```

**Step 10: Build and test**

```bash
dotnet build shadowbrook.slnx
dotnet test src/backend/Shadowbrook.Api.Tests
```

Expected: 0 errors, all tests pass.

**Step 11: Commit**

```bash
git add -A
git commit -m "chore: move backend projects under src/backend/"
```

---

### Task 2: Create `Shadowbrook.Domain` project with common base types

**Step 1: Create the project**

```bash
dotnet new classlib -n Shadowbrook.Domain -o src/backend/Shadowbrook.Domain --framework net10.0
rm src/backend/Shadowbrook.Domain/Class1.cs
dotnet sln shadowbrook.slnx add src/backend/Shadowbrook.Domain/Shadowbrook.Domain.csproj
```

Verify `Shadowbrook.Domain.csproj` has `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

**Step 2: Add project reference from API to Domain**

In `src/backend/Shadowbrook.Api/Shadowbrook.Api.csproj`, add:

```xml
<ItemGroup>
  <ProjectReference Include="..\Shadowbrook.Domain\Shadowbrook.Domain.csproj" />
</ItemGroup>
```

**Step 3: Create `Common/IDomainEvent.cs`**

Create `src/backend/Shadowbrook.Domain/Common/IDomainEvent.cs`:

```csharp
namespace Shadowbrook.Domain.Common;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
```

**Step 4: Create `Common/Entity.cs`**

Create `src/backend/Shadowbrook.Domain/Common/Entity.cs`:

```csharp
namespace Shadowbrook.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();
    public void ClearDomainEvents() => domainEvents.Clear();
    protected void AddDomainEvent(IDomainEvent domainEvent) => domainEvents.Add(domainEvent);
}
```

**Step 5: Create `Common/DomainException.cs`**

Create `src/backend/Shadowbrook.Domain/Common/DomainException.cs`:

```csharp
namespace Shadowbrook.Domain.Common;

public class DomainException(string message) : Exception(message);
```

**Step 6: Build**

```bash
dotnet build shadowbrook.slnx
```

Expected: 0 errors.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat(domain): add Shadowbrook.Domain project with Entity, IDomainEvent, DomainException"
```

---

### Task 3: Create `Shadowbrook.Domain.Tests` project

**Step 1: Create the project**

```bash
dotnet new xunit -n Shadowbrook.Domain.Tests -o src/backend/Shadowbrook.Domain.Tests --framework net10.0
rm src/backend/Shadowbrook.Domain.Tests/UnitTest1.cs
dotnet sln shadowbrook.slnx add src/backend/Shadowbrook.Domain.Tests/Shadowbrook.Domain.Tests.csproj
```

**Step 2: Add project reference to Domain**

In `src/backend/Shadowbrook.Domain.Tests/Shadowbrook.Domain.Tests.csproj`, add:

```xml
<ItemGroup>
  <ProjectReference Include="..\Shadowbrook.Domain\Shadowbrook.Domain.csproj" />
</ItemGroup>
```

Remove any default project references that the template may have added.

**Step 3: Update `Makefile` test target**

```makefile
test: ## Run all backend tests
	dotnet test src/backend/Shadowbrook.Domain.Tests
	dotnet test src/backend/Shadowbrook.Api.Tests
```

**Step 4: Build**

```bash
dotnet build shadowbrook.slnx
```

Expected: 0 errors.

**Step 5: Commit**

```bash
git add -A
git commit -m "chore: add Shadowbrook.Domain.Tests project"
```

---

### Task 4: Create domain enums, exceptions, and event

**Step 1: Create `WalkUpWaitlist/WaitlistStatus.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/WaitlistStatus.cs`:

```csharp
namespace Shadowbrook.Domain.WalkUpWaitlist;

public enum WaitlistStatus
{
    Open,
    Closed
}
```

**Step 2: Create `WalkUpWaitlist/RequestStatus.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/RequestStatus.cs`:

```csharp
namespace Shadowbrook.Domain.WalkUpWaitlist;

public enum RequestStatus
{
    Pending,
    Fulfilled,
    Cancelled
}
```

**Step 3: Create `WalkUpWaitlist/Exceptions/WaitlistNotOpenException.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/Exceptions/WaitlistNotOpenException.cs`:

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class WaitlistNotOpenException()
    : DomainException("Walk-up waitlist is not open.");
```

**Step 4: Create `WalkUpWaitlist/Exceptions/DuplicateTeeTimeRequestException.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/Exceptions/DuplicateTeeTimeRequestException.cs`:

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

public class DuplicateTeeTimeRequestException(TimeOnly teeTime)
    : DomainException($"An active waitlist request already exists for {teeTime:HH:mm}.");
```

**Step 5: Create `WalkUpWaitlist/Events/TeeTimeRequestAdded.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/Events/TeeTimeRequestAdded.cs`:

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist.Events;

public record TeeTimeRequestAdded : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required int GolfersNeeded { get; init; }
}
```

**Step 6: Create `WalkUpWaitlist/IShortCodeGenerator.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/IShortCodeGenerator.cs`:

```csharp
namespace Shadowbrook.Domain.WalkUpWaitlist;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
```

**Step 7: Build**

```bash
dotnet build shadowbrook.slnx
```

Expected: 0 errors.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat(domain): add waitlist enums, exceptions, event, and IShortCodeGenerator"
```

---

### Task 5: Create `TeeTimeRequest` child entity

**Step 1: Write the test**

Create `src/backend/Shadowbrook.Domain.Tests/WalkUpWaitlist/TeeTimeRequestTests.cs`:

```csharp
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlist;

public class TeeTimeRequestTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var waitlistId = Guid.NewGuid();
        var teeTime = new TimeOnly(10, 0);

        var request = new TeeTimeRequest(waitlistId, teeTime, 2);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(waitlistId, request.WalkUpWaitlistId);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
        Assert.Equal(RequestStatus.Pending, request.Status);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test src/backend/Shadowbrook.Domain.Tests --filter "TeeTimeRequestTests"
```

Expected: FAIL — `TeeTimeRequest` class does not exist.

**Step 3: Create `WalkUpWaitlist/TeeTimeRequest.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/TeeTimeRequest.cs`:

```csharp
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.WalkUpWaitlist;

public class TeeTimeRequest : Entity
{
    public Guid WalkUpWaitlistId { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int GolfersNeeded { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TeeTimeRequest() { } // EF

    public TeeTimeRequest(Guid walkUpWaitlistId, TimeOnly teeTime, int golfersNeeded)
    {
        Id = Guid.NewGuid();
        WalkUpWaitlistId = walkUpWaitlistId;
        TeeTime = teeTime;
        GolfersNeeded = golfersNeeded;
        Status = RequestStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

**Step 4: Run test to verify it passes**

```bash
dotnet test src/backend/Shadowbrook.Domain.Tests --filter "TeeTimeRequestTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(domain): add TeeTimeRequest child entity with tests"
```

---

### Task 6: Create `WalkUpWaitlist` aggregate root

**Step 1: Write the tests**

Create `src/backend/Shadowbrook.Domain.Tests/WalkUpWaitlist/WalkUpWaitlistTests.cs`:

```csharp
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.Tests.WalkUpWaitlist;

public class WalkUpWaitlistTests
{
    private readonly StubShortCodeGenerator shortCodeGenerator = new("1234");

    [Fact]
    public async Task OpenAsync_CreatesOpenWaitlist()
    {
        var courseId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 6);

        var waitlist = await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            courseId, date, this.shortCodeGenerator);

        Assert.NotEqual(Guid.Empty, waitlist.Id);
        Assert.Equal(courseId, waitlist.CourseId);
        Assert.Equal(date, waitlist.Date);
        Assert.Equal("1234", waitlist.ShortCode);
        Assert.Equal(WaitlistStatus.Open, waitlist.Status);
        Assert.Null(waitlist.ClosedAt);
        Assert.Empty(waitlist.TeeTimeRequests);
    }

    [Fact]
    public async Task Close_TransitionsToClosedStatus()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.Close();

        Assert.Equal(WaitlistStatus.Closed, waitlist.Status);
        Assert.NotNull(waitlist.ClosedAt);
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        Assert.Throws<WaitlistNotOpenException>(() => waitlist.Close());
    }

    [Fact]
    public async Task AddTeeTimeRequest_AddsRequestAndRaisesEvent()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var teeTime = new TimeOnly(10, 0);

        var request = waitlist.AddTeeTimeRequest(teeTime, 2);

        Assert.Single(waitlist.TeeTimeRequests);
        Assert.Equal(teeTime, request.TeeTime);
        Assert.Equal(2, request.GolfersNeeded);
        Assert.Equal(RequestStatus.Pending, request.Status);

        var domainEvent = Assert.Single(waitlist.DomainEvents);
        var addedEvent = Assert.IsType<TeeTimeRequestAdded>(domainEvent);
        Assert.Equal(waitlist.Id, addedEvent.WaitlistId);
        Assert.Equal(request.Id, addedEvent.TeeTimeRequestId);
        Assert.Equal(teeTime, addedEvent.TeeTime);
        Assert.Equal(2, addedEvent.GolfersNeeded);
    }

    [Fact]
    public async Task AddTeeTimeRequest_WhenClosed_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        waitlist.Close();

        Assert.Throws<WaitlistNotOpenException>(
            () => waitlist.AddTeeTimeRequest(new TimeOnly(10, 0), 2));
    }

    [Fact]
    public async Task AddTeeTimeRequest_DuplicateTeeTime_Throws()
    {
        var waitlist = await CreateOpenWaitlistAsync();
        var teeTime = new TimeOnly(10, 0);
        waitlist.AddTeeTimeRequest(teeTime, 2);

        Assert.Throws<DuplicateTeeTimeRequestException>(
            () => waitlist.AddTeeTimeRequest(teeTime, 3));
    }

    [Fact]
    public async Task AddTeeTimeRequest_DifferentTeeTimes_Succeeds()
    {
        var waitlist = await CreateOpenWaitlistAsync();

        waitlist.AddTeeTimeRequest(new TimeOnly(10, 0), 2);
        waitlist.AddTeeTimeRequest(new TimeOnly(11, 0), 3);

        Assert.Equal(2, waitlist.TeeTimeRequests.Count);
    }

    private async Task<Domain.WalkUpWaitlist.WalkUpWaitlist> CreateOpenWaitlistAsync()
    {
        return await Domain.WalkUpWaitlist.WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 6), this.shortCodeGenerator);
    }

    private class StubShortCodeGenerator(string code) : IShortCodeGenerator
    {
        public Task<string> GenerateAsync(DateOnly date) => Task.FromResult(code);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/backend/Shadowbrook.Domain.Tests --filter "WalkUpWaitlistTests"
```

Expected: FAIL — `WalkUpWaitlist` class does not exist.

**Step 3: Create `WalkUpWaitlist/WalkUpWaitlist.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/WalkUpWaitlist.cs`:

```csharp
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Events;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

namespace Shadowbrook.Domain.WalkUpWaitlist;

public class WalkUpWaitlist : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public string ShortCode { get; private set; } = string.Empty;
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<TeeTimeRequest> teeTimeRequests = [];
    public IReadOnlyCollection<TeeTimeRequest> TeeTimeRequests => this.teeTimeRequests.AsReadOnly();

    private WalkUpWaitlist() { } // EF

    public static async Task<WalkUpWaitlist> OpenAsync(
        Guid courseId, DateOnly date, IShortCodeGenerator shortCodeGenerator)
    {
        var shortCode = await shortCodeGenerator.GenerateAsync(date);
        var now = DateTimeOffset.UtcNow;
        return new WalkUpWaitlist
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Date = date,
            ShortCode = shortCode,
            Status = WaitlistStatus.Open,
            OpenedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Close()
    {
        if (this.Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var now = DateTimeOffset.UtcNow;
        this.Status = WaitlistStatus.Closed;
        this.ClosedAt = now;
        this.UpdatedAt = now;
    }

    public TeeTimeRequest AddTeeTimeRequest(TimeOnly teeTime, int golfersNeeded)
    {
        if (this.Status != WaitlistStatus.Open)
        {
            throw new WaitlistNotOpenException();
        }

        var duplicate = this.teeTimeRequests.Any(r =>
            r.TeeTime == teeTime && r.Status == RequestStatus.Pending);
        if (duplicate)
        {
            throw new DuplicateTeeTimeRequestException(teeTime);
        }

        var request = new TeeTimeRequest(this.Id, teeTime, golfersNeeded);
        this.teeTimeRequests.Add(request);

        AddDomainEvent(new TeeTimeRequestAdded
        {
            WaitlistId = this.Id,
            TeeTimeRequestId = request.Id,
            CourseId = this.CourseId,
            Date = this.Date,
            TeeTime = teeTime,
            GolfersNeeded = golfersNeeded
        });

        this.UpdatedAt = DateTimeOffset.UtcNow;
        return request;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/backend/Shadowbrook.Domain.Tests --filter "WalkUpWaitlistTests"
```

Expected: all 7 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat(domain): add WalkUpWaitlist aggregate root with tests"
```

---

### Task 7: Create repository interface and add `IWalkUpWaitlistRepository`

**Step 1: Create `WalkUpWaitlist/IWalkUpWaitlistRepository.cs`**

Create `src/backend/Shadowbrook.Domain/WalkUpWaitlist/IWalkUpWaitlistRepository.cs`:

```csharp
namespace Shadowbrook.Domain.WalkUpWaitlist;

public interface IWalkUpWaitlistRepository
{
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(WalkUpWaitlist waitlist);
    Task SaveAsync();
}
```

**Step 2: Build**

```bash
dotnet build shadowbrook.slnx
```

Expected: 0 errors.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat(domain): add IWalkUpWaitlistRepository interface"
```

---

### Task 8: Restructure API project — move folders under Infrastructure

**Step 1: Create Infrastructure directories**

```bash
mkdir -p src/backend/Shadowbrook.Api/Infrastructure/Data
mkdir -p src/backend/Shadowbrook.Api/Infrastructure/Events
mkdir -p src/backend/Shadowbrook.Api/Infrastructure/Services
mkdir -p src/backend/Shadowbrook.Api/Infrastructure/Repositories
mkdir -p src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations
```

**Step 2: Move files**

```bash
git mv src/backend/Shadowbrook.Api/Data/ApplicationDbContext.cs src/backend/Shadowbrook.Api/Infrastructure/Data/
git mv src/backend/Shadowbrook.Api/Events/IDomainEventPublisher.cs src/backend/Shadowbrook.Api/Infrastructure/Events/
git mv src/backend/Shadowbrook.Api/Events/IDomainEventHandler.cs src/backend/Shadowbrook.Api/Infrastructure/Events/
git mv src/backend/Shadowbrook.Api/Events/InProcessDomainEventPublisher.cs src/backend/Shadowbrook.Api/Infrastructure/Events/
git mv src/backend/Shadowbrook.Api/Events/WaitlistRequestCreated.cs src/backend/Shadowbrook.Api/Infrastructure/Events/
git mv src/backend/Shadowbrook.Api/Services/ConsoleTextMessageService.cs src/backend/Shadowbrook.Api/Infrastructure/Services/
git mv src/backend/Shadowbrook.Api/Services/ITextMessageService.cs src/backend/Shadowbrook.Api/Infrastructure/Services/
```

Remove empty directories:

```bash
rmdir src/backend/Shadowbrook.Api/Data
rmdir src/backend/Shadowbrook.Api/Events
rmdir src/backend/Shadowbrook.Api/Services
```

**Step 3: Update namespaces**

Update namespaces in all moved files. The pattern is `Shadowbrook.Api.X` → `Shadowbrook.Api.Infrastructure.X`:

- `ApplicationDbContext.cs`: `Shadowbrook.Api.Data` → `Shadowbrook.Api.Infrastructure.Data`
- `IDomainEventPublisher.cs`: `Shadowbrook.Api.Events` → `Shadowbrook.Api.Infrastructure.Events`
- `IDomainEventHandler.cs`: `Shadowbrook.Api.Events` → `Shadowbrook.Api.Infrastructure.Events`
- `InProcessDomainEventPublisher.cs`: `Shadowbrook.Api.Events` → `Shadowbrook.Api.Infrastructure.Events`
- `WaitlistRequestCreated.cs`: `Shadowbrook.Api.Events` → `Shadowbrook.Api.Infrastructure.Events`
- `ConsoleTextMessageService.cs`: `Shadowbrook.Api.Services` → `Shadowbrook.Api.Infrastructure.Services`
- `ITextMessageService.cs`: `Shadowbrook.Api.Services` → `Shadowbrook.Api.Infrastructure.Services`

**Step 4: Update all `using` statements across the codebase**

Search and replace in `src/backend/Shadowbrook.Api/` and `src/backend/Shadowbrook.Api.Tests/`:

- `using Shadowbrook.Api.Data;` → `using Shadowbrook.Api.Infrastructure.Data;`
- `using Shadowbrook.Api.Events;` → `using Shadowbrook.Api.Infrastructure.Events;`
- `using Shadowbrook.Api.Services;` → `using Shadowbrook.Api.Infrastructure.Services;`

Files that will need updates (use `grep -r` to find all):
- `Program.cs`
- All endpoint files in `Endpoints/`
- `TestWebApplicationFactory.cs` in tests
- Any test files referencing these namespaces

**Step 5: Remove `IDomainEvent.cs` from API (now in Domain)**

Delete `src/backend/Shadowbrook.Api/Infrastructure/Events/IDomainEvent.cs` — wait, this file wasn't moved. It's still at `src/backend/Shadowbrook.Api/Events/IDomainEvent.cs`. Actually, it was moved in step 2. We need to delete it since the domain project now owns `IDomainEvent`.

Actually — don't move `IDomainEvent.cs` in step 2. Instead:
- Move all files from `Events/` **except** `IDomainEvent.cs`
- Then delete `IDomainEvent.cs` from the API project
- Update `InProcessDomainEventPublisher.cs` and `WaitlistRequestCreated.cs` to use `using Shadowbrook.Domain.Common;` for `IDomainEvent`
- Update `IDomainEventPublisher.cs` and `IDomainEventHandler.cs` to use `using Shadowbrook.Domain.Common;` for `IDomainEvent`

**Step 6: Build and test**

```bash
dotnet build shadowbrook.slnx
dotnet test src/backend/Shadowbrook.Api.Tests
```

Expected: 0 errors, all tests pass.

**Step 7: Commit**

```bash
git add -A
git commit -m "refactor: move Data, Events, Services under Infrastructure namespace"
```

---

### Task 9: Add EF configuration, repository implementation, and ShortCodeGenerator

**Step 1: Create `WalkUpWaitlistConfiguration.cs`**

Create `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WalkUpWaitlistConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WalkUpWaitlistConfiguration : IEntityTypeConfiguration<WalkUpWaitlistEntity>
{
    public void Configure(EntityTypeBuilder<WalkUpWaitlistEntity> builder)
    {
        builder.ToTable("CourseWaitlists");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(w => w.ShortCode).IsRequired().HasMaxLength(4);

        builder.HasIndex(w => new { w.CourseId, w.Date }).IsUnique();
        builder.HasIndex(w => new { w.ShortCode, w.Date });

        builder.HasMany(w => w.TeeTimeRequests)
            .WithOne()
            .HasForeignKey(r => r.WalkUpWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.TeeTimeRequests)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class TeeTimeRequestConfiguration : IEntityTypeConfiguration<TeeTimeRequest>
{
    public void Configure(EntityTypeBuilder<TeeTimeRequest> builder)
    {
        builder.ToTable("WaitlistRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>();

        builder.HasIndex(r => new { r.WalkUpWaitlistId, r.TeeTime });
        builder.HasIndex(r => new { r.WalkUpWaitlistId, r.Status });
    }
}
```

**Step 2: Update `ApplicationDbContext`**

Replace the `CourseWaitlists` and `WaitlistRequests` `DbSet` properties and their `OnModelCreating` configuration.

Change:
```csharp
public DbSet<CourseWaitlist> CourseWaitlists => Set<CourseWaitlist>();
public DbSet<WaitlistRequest> WaitlistRequests => Set<WaitlistRequest>();
```

To:
```csharp
public DbSet<Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist> WalkUpWaitlists => Set<Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist>();
public DbSet<TeeTimeRequest> TeeTimeRequests => Set<TeeTimeRequest>();
```

Add `using Shadowbrook.Domain.WalkUpWaitlist;` (for `TeeTimeRequest`).

Remove the `CourseWaitlist` and `WaitlistRequest` configuration blocks from `OnModelCreating` and add:

```csharp
modelBuilder.ApplyConfiguration(new WalkUpWaitlistConfiguration());
modelBuilder.ApplyConfiguration(new TeeTimeRequestConfiguration());
```

Add `using Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;`.

Also add domain event dispatch override to `ApplicationDbContext`:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var result = await base.SaveChangesAsync(cancellationToken);

    var domainEvents = ChangeTracker.Entries<Entity>()
        .SelectMany(e => e.Entity.DomainEvents)
        .ToList();

    foreach (var entity in ChangeTracker.Entries<Entity>())
    {
        entity.Entity.ClearDomainEvents();
    }

    var publisher = this.currentUser is not null
        ? this.Services?.GetService<IDomainEventPublisher>()
        : null;

    if (publisher is not null)
    {
        foreach (var domainEvent in domainEvents)
        {
            await publisher.PublishAsync(domainEvent);
        }
    }

    return result;
}
```

Note: this requires storing `IServiceProvider` or resolving the publisher. A simpler approach: inject `IDomainEventPublisher` into the `DbContext` constructor. Update the primary constructor:

```csharp
public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null,
    IDomainEventPublisher? eventPublisher = null) : DbContext(options)
```

Then the `SaveChangesAsync` override uses `this.eventPublisher` directly.

Add `using Shadowbrook.Domain.Common;` for `Entity`.

**Step 3: Create `WalkUpWaitlistRepository.cs`**

Create `src/backend/Shadowbrook.Api/Infrastructure/Repositories/WalkUpWaitlistRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.Repositories;

public class WalkUpWaitlistRepository(ApplicationDbContext db) : IWalkUpWaitlistRepository
{
    public async Task<WalkUpWaitlistEntity?> GetByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);
    }

    public async Task<WalkUpWaitlistEntity?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
    {
        return await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date
                && w.Status == WaitlistStatus.Open);
    }

    public void Add(WalkUpWaitlistEntity waitlist)
    {
        db.WalkUpWaitlists.Add(waitlist);
    }

    public async Task SaveAsync()
    {
        await db.SaveChangesAsync();
    }
}
```

**Step 4: Create `ShortCodeGenerator.cs`**

Create `src/backend/Shadowbrook.Api/Infrastructure/Services/ShortCodeGenerator.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.Services;

public class ShortCodeGenerator(ApplicationDbContext db) : IShortCodeGenerator
{
    public async Task<string> GenerateAsync(DateOnly date)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Random.Shared.Next(0, 10000).ToString("D4");
            var taken = await db.WalkUpWaitlists
                .AnyAsync(w => w.ShortCode == candidate && w.Date == date);
            if (!taken)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique short code.");
    }
}
```

**Step 5: Register services in `Program.cs`**

Add to `Program.cs`:

```csharp
builder.Services.AddScoped<IWalkUpWaitlistRepository, WalkUpWaitlistRepository>();
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
```

Add the necessary `using` statements:
```csharp
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Api.Infrastructure.Services;
```

**Step 6: Delete old model files**

Delete `src/backend/Shadowbrook.Api/Models/CourseWaitlist.cs` and `src/backend/Shadowbrook.Api/Models/WaitlistRequest.cs`. The domain entities replace them.

Also check `src/backend/Shadowbrook.Api/Models/Course.cs` — it likely has a `Waitlists` navigation property referencing the old `CourseWaitlist`. Update it to reference the domain entity or remove the navigation if it's not used by other endpoints.

**Step 7: Build**

```bash
dotnet build shadowbrook.slnx
```

Fix any compilation errors from the type changes.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: add EF configuration, repository, ShortCodeGenerator, domain event dispatch"
```

---

### Task 10: Refactor endpoints to use domain model and repository

**Step 1: Add global exception handler**

In `Program.cs`, add before `app.MapWalkUpWaitlistEndpoints()`:

```csharp
app.UseExceptionHandler(error => error.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (ex is DomainException domainEx)
    {
        context.Response.StatusCode = domainEx switch
        {
            DuplicateTeeTimeRequestException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        await context.Response.WriteAsJsonAsync(new { error = domainEx.Message });
    }
}));
```

Add using statements:
```csharp
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
```

**Step 2: Refactor `WalkUpWaitlistEndpoints.cs`**

Rewrite the endpoint file to use the repository and domain model. Key changes:

- `OpenWaitlist`: Use `IWalkUpWaitlistRepository` for existence check, `WalkUpWaitlist.OpenAsync()` with `IShortCodeGenerator`, `repo.Add()`, `repo.SaveAsync()`
- `CloseWaitlist`: Use `repo.GetOpenByCourseDateAsync()`, call `waitlist.Close()`, `repo.SaveAsync()`
- `CreateWaitlistRequest`: Use `repo.GetOpenByCourseDateAsync()`, call `waitlist.AddTeeTimeRequest()`, `repo.SaveAsync()` — remove explicit event publishing (handled by DbContext)
- `GetToday`: Use `repo.GetByCourseDateAsync()`

Remove `using Shadowbrook.Api.Infrastructure.Events;` (no more explicit event publishing).
Add `using Shadowbrook.Domain.WalkUpWaitlist;`.

Update response DTOs to use `WaitlistStatus` enum in the `ToResponse` helper — convert with `.ToString()`.

Full implementation:

```csharp
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    public static void MapWalkUpWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}/walkup-waitlist");

        group.MapPost("/open", OpenWaitlist);
        group.MapPost("/close", CloseWaitlist);
        group.MapGet("/today", GetToday);
        group.MapPost("/requests", CreateWaitlistRequest);
    }

    private static async Task<IResult> OpenWaitlist(
        Guid courseId,
        ApplicationDbContext db,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await repo.GetByCourseDateAsync(courseId, today);

        if (existing is not null)
        {
            return Results.Conflict(new
            {
                error = existing.Status == WaitlistStatus.Open
                    ? "Walk-up waitlist is already open for today."
                    : "Walk-up waitlist was already used today."
            });
        }

        var waitlist = await WalkUpWaitlistEntity.OpenAsync(courseId, today, shortCodeGenerator);

        repo.Add(waitlist);
        await repo.SaveAsync();

        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", ToResponse(waitlist));
    }

    private static async Task<IResult> CloseWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        waitlist.Close();
        await repo.SaveAsync();

        return Results.Ok(ToResponse(waitlist));
    }

    private static async Task<IResult> GetToday(
        Guid courseId,
        ApplicationDbContext db,
        IWalkUpWaitlistRepository repo)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;
        var todayResponse = new WalkUpWaitlistTodayResponse(
            waitlistResponse,
            new List<WalkUpWaitlistEntryResponse>());

        return Results.Ok(todayResponse);
    }

    private static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWalkUpWaitlistRequestRequest request,
        IWalkUpWaitlistRepository repo)
    {
        if (string.IsNullOrWhiteSpace(request.Date) ||
            !DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var parsedDate))
        {
            return Results.BadRequest(new { error = "A valid date in yyyy-MM-dd format is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TeeTime) ||
            !TimeOnly.TryParseExact(request.TeeTime, new[] { "HH:mm", "HH:mm:ss" }, out var parsedTeeTime))
        {
            return Results.BadRequest(new { error = "A valid tee time in HH:mm format is required." });
        }

        if (request.GolfersNeeded is < 1 or > 4)
        {
            return Results.BadRequest(new { error = "Golfers needed must be between 1 and 4." });
        }

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, parsedDate);
        if (waitlist is null)
        {
            return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });
        }

        var teeTimeRequest = waitlist.AddTeeTimeRequest(parsedTeeTime, request.GolfersNeeded);
        await repo.SaveAsync();

        var response = new WalkUpWaitlistRequestResponse(
            teeTimeRequest.Id,
            teeTimeRequest.TeeTime.ToString("HH:mm"),
            teeTimeRequest.GolfersNeeded,
            teeTimeRequest.Status.ToString());

        return Results.Created(
            $"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlistEntity w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"),
            w.Status.ToString(), w.OpenedAt, w.ClosedAt);
}

public record CreateWalkUpWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);

public record WalkUpWaitlistRequestResponse(
    Guid Id,
    string TeeTime,
    int GolfersNeeded,
    string Status);

public record WalkUpWaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WalkUpWaitlistTodayResponse(
    WalkUpWaitlistResponse? Waitlist,
    List<WalkUpWaitlistEntryResponse> Entries);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    DateTimeOffset JoinedAt);
```

**Step 3: Build**

```bash
dotnet build shadowbrook.slnx
```

Fix any remaining compilation errors.

**Step 4: Run all tests**

```bash
dotnet test src/backend/Shadowbrook.Api.Tests
dotnet test src/backend/Shadowbrook.Domain.Tests
```

Expected: All tests pass. The integration tests should work because the EF config maps to the same tables/columns and enums are stored as matching strings.

**Step 5: Verify no pending model changes**

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api
```

Expected: No pending changes. If there are, investigate — the EF model should be equivalent.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor: use domain model in walk-up waitlist endpoints"
```

---

### Task 11: Clean up old event and update references

**Step 1: Evaluate `WaitlistRequestCreated` event**

The old `WaitlistRequestCreated` event in `Infrastructure/Events/` is now replaced by `TeeTimeRequestAdded` in the domain. Check if anything subscribes to `WaitlistRequestCreated`:

```bash
grep -r "WaitlistRequestCreated" src/backend/
```

If nothing references it beyond the old endpoint code (which was replaced in Task 10), delete it:

```bash
rm src/backend/Shadowbrook.Api/Infrastructure/Events/WaitlistRequestCreated.cs
```

**Step 2: Build and test**

```bash
dotnet build shadowbrook.slnx
dotnet test src/backend/Shadowbrook.Api.Tests
dotnet test src/backend/Shadowbrook.Domain.Tests
```

Expected: All pass.

**Step 3: Commit**

```bash
git add -A
git commit -m "chore: remove obsolete WaitlistRequestCreated event"
```

---

### Task 12: Update `Course` model navigation property

**Step 1: Check `Course.cs` for waitlist navigation**

Read `src/backend/Shadowbrook.Api/Models/Course.cs` and check for `Waitlists` or `CourseWaitlist` references. If there's a navigation property like `public ICollection<CourseWaitlist> Waitlists`, update it to reference the domain entity or remove it if unused.

If updating:
```csharp
public ICollection<Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist> Waitlists { get; set; } = [];
```

Update the `OnModelCreating` relationship in `ApplicationDbContext` if needed — the `WalkUpWaitlistConfiguration` should handle the relationship from the waitlist side.

**Step 2: Build and test**

```bash
dotnet build shadowbrook.slnx
dotnet test src/backend/Shadowbrook.Api.Tests
```

**Step 3: Commit**

```bash
git add -A
git commit -m "refactor: update Course navigation to use domain WalkUpWaitlist entity"
```

---

### Task 13: Final verification and documentation update

**Step 1: Run full test suite**

```bash
dotnet test src/backend/Shadowbrook.Domain.Tests
dotnet test src/backend/Shadowbrook.Api.Tests
pnpm --dir src/web lint
pnpm --dir src/web test
```

Expected: All pass.

**Step 2: Verify EF migration state**

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations has-pending-model-changes --project src/backend/Shadowbrook.Api
```

Expected: No pending changes.

**Step 3: Update CLAUDE.md project structure**

Add the domain project to the project structure section and note the DDD pattern:

```markdown
## Project Structure
- src/backend/Shadowbrook.Domain/ — Domain model (aggregates, entities, events, exceptions, repository interfaces)
- src/backend/Shadowbrook.Api/ — .NET Web API (endpoints, infrastructure: EF, repositories, services)
- src/backend/Shadowbrook.Domain.Tests/ — Domain unit tests (pure, no infrastructure)
- src/backend/Shadowbrook.Api.Tests/ — API integration tests (TestWebApplicationFactory with SQLite)
- src/web/ — React SPA
- docs/ — Documentation
- docs/plans/ — Design docs and implementation plans
- infra/ — Azure deployment config (planned)
```

**Step 4: Commit**

```bash
git add -A
git commit -m "docs: update CLAUDE.md and rules for new project structure"
```
