# Walk-Up Waitlist — Domain-Driven Design Refactor

## Goal

Introduce a rich domain model for the walk-up waitlist, establishing a DDD pattern for the project going forward. Business logic moves out of endpoint handlers and into domain entities that guard their own invariants, raise domain events, and are testable in isolation.

## Project Restructuring

### Move existing projects under `src/backend/`

```
src/
├── backend/
│   ├── Shadowbrook.Domain/
│   │   └── Shadowbrook.Domain.csproj
│   ├── Shadowbrook.Domain.Tests/
│   │   └── Shadowbrook.Domain.Tests.csproj
│   ├── Shadowbrook.Api/
│   │   └── Shadowbrook.Api.csproj        (currently src/api)
│   ├── Shadowbrook.Api.Tests/
│   │   └── Shadowbrook.Api.Tests.csproj   (currently tests/api)
│   └── shadowbrook.slnx
├── web/
│   └── ...
```

`Shadowbrook.Domain` has **zero dependencies** — no EF Core, no ASP.NET, no third-party packages. `Shadowbrook.Api` references `Shadowbrook.Domain`.

### Update all path references

The following reference `src/api` or `tests/api` and must be updated:
- `shadowbrook.slnx` — project paths
- `Makefile` — build, test, dev commands
- `.github/workflows/` — CI workflow paths
- `.claude/CLAUDE.md` — build/test commands, EF tool paths, project structure docs
- `.claude/rules/backend/` — any rules referencing `src/api`
- `infra/` — Dockerfile, deployment config
- `.editorconfig` / `Directory.Build.props` — if path-scoped

## Domain Project Structure

```
src/backend/Shadowbrook.Domain/
├── Shadowbrook.Domain.csproj
├── Common/
│   ├── Entity.cs
│   ├── IDomainEvent.cs
│   └── DomainException.cs
└── WalkUpWaitlist/
    ├── WalkUpWaitlist.cs            # Aggregate root
    ├── TeeTimeRequest.cs            # Child entity
    ├── WaitlistStatus.cs            # Enum: Open, Closed
    ├── RequestStatus.cs             # Enum: Pending, Fulfilled, Cancelled
    ├── IWalkUpWaitlistRepository.cs # Repository interface
    ├── IShortCodeGenerator.cs       # Domain service interface
    ├── Events/
    │   └── TeeTimeRequestAdded.cs
    └── Exceptions/
        ├── WaitlistNotOpenException.cs
        └── DuplicateTeeTimeRequestException.cs

src/backend/Shadowbrook.Domain.Tests/
├── Shadowbrook.Domain.Tests.csproj  # xUnit, references Shadowbrook.Domain only
└── WalkUpWaitlist/
    └── WalkUpWaitlistTests.cs       # Pure unit tests — no DB, no HTTP
```

## API Project Restructuring

```
src/backend/Shadowbrook.Api/
├── Auth/
├── Endpoints/
├── Infrastructure/
│   ├── Data/
│   │   └── ApplicationDbContext.cs          (moved from Data/)
│   ├── EntityTypeConfigurations/
│   │   └── WalkUpWaitlistConfiguration.cs   (new)
│   ├── Events/
│   │   ├── IDomainEventPublisher.cs         (moved from Events/)
│   │   ├── InProcessDomainEventPublisher.cs
│   │   └── IDomainEventHandler.cs
│   ├── Repositories/
│   │   └── WalkUpWaitlistRepository.cs      (new)
│   └── Services/
│       ├── ConsoleTextMessageService.cs     (moved from Services/)
│       └── ShortCodeGenerator.cs            (new — implements IShortCodeGenerator)
├── Migrations/                              (stays — EF convention)
├── Models/                                  (Course.cs, Tenant.cs, Booking.cs stay)
└── Program.cs
```

- `Data/`, `Events/`, `Services/` move under `Infrastructure/`
- `Models/CourseWaitlist.cs` and `Models/WaitlistRequest.cs` are deleted — replaced by domain entities
- Non-waitlist models (`Course.cs`, `Tenant.cs`, `Booking.cs`) stay in `Models/` until they get their own DDD treatment

## Domain Model

### Entity Base Class

```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => domainEvents.AsReadOnly();
    public void ClearDomainEvents() => domainEvents.Clear();
    protected void AddDomainEvent(IDomainEvent e) => domainEvents.Add(e);
}
```

### IDomainEvent

Moved from `Shadowbrook.Api.Events` to `Shadowbrook.Domain.Common`. The domain defines the contract; the API provides the dispatch infrastructure.

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
```

### DomainException

```csharp
public class DomainException(string message) : Exception(message);
```

Typed subclasses for specific violations:
- `WaitlistNotOpenException` — thrown when trying to close or add requests to a non-open waitlist
- `DuplicateTeeTimeRequestException` — thrown when a pending request already exists for the same tee time

### IShortCodeGenerator (Domain Service)

```csharp
public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
```

Defined in the domain. Implemented in API infrastructure — the implementation queries the database to ensure uniqueness for the given date.

### WalkUpWaitlist (Aggregate Root)

```csharp
public class WalkUpWaitlist : Entity
{
    public Guid CourseId { get; private set; }
    public DateOnly Date { get; private set; }
    public string ShortCode { get; private set; }
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<TeeTimeRequest> teeTimeRequests = [];
    public IReadOnlyCollection<TeeTimeRequest> TeeTimeRequests => teeTimeRequests.AsReadOnly();

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
        if (Status != WaitlistStatus.Open)
            throw new WaitlistNotOpenException();

        var now = DateTimeOffset.UtcNow;
        Status = WaitlistStatus.Closed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public TeeTimeRequest AddTeeTimeRequest(TimeOnly teeTime, int golfersNeeded)
    {
        if (Status != WaitlistStatus.Open)
            throw new WaitlistNotOpenException();

        var duplicate = teeTimeRequests.Any(r =>
            r.TeeTime == teeTime && r.Status == RequestStatus.Pending);
        if (duplicate)
            throw new DuplicateTeeTimeRequestException(teeTime);

        var request = new TeeTimeRequest(Id, teeTime, golfersNeeded);
        teeTimeRequests.Add(request);

        AddDomainEvent(new TeeTimeRequestAdded
        {
            WaitlistId = Id,
            TeeTimeRequestId = request.Id,
            CourseId = CourseId,
            Date = Date,
            TeeTime = teeTime,
            GolfersNeeded = golfersNeeded
        });

        UpdatedAt = DateTimeOffset.UtcNow;
        return request;
    }
}
```

### TeeTimeRequest (Child Entity)

```csharp
public class TeeTimeRequest : Entity
{
    public Guid WalkUpWaitlistId { get; private set; }
    public TimeOnly TeeTime { get; private set; }
    public int GolfersNeeded { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TeeTimeRequest() { } // EF

    internal TeeTimeRequest(Guid walkUpWaitlistId, TimeOnly teeTime, int golfersNeeded)
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

### Enums

```csharp
public enum WaitlistStatus { Open, Closed }
public enum RequestStatus { Pending, Fulfilled, Cancelled }
```

## Repository

### Interface (Domain)

```csharp
public interface IWalkUpWaitlistRepository
{
    Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date);
    Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date);
    void Add(WalkUpWaitlist waitlist);
    Task SaveAsync();
}
```

### Implementation (API Infrastructure)

```csharp
public class WalkUpWaitlistRepository(ApplicationDbContext db) : IWalkUpWaitlistRepository
{
    public async Task<WalkUpWaitlist?> GetByCourseDateAsync(Guid courseId, DateOnly date)
        => await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date);

    public async Task<WalkUpWaitlist?> GetOpenByCourseDateAsync(Guid courseId, DateOnly date)
        => await db.WalkUpWaitlists
            .Include(w => w.TeeTimeRequests)
            .FirstOrDefaultAsync(w => w.CourseId == courseId && w.Date == date
                && w.Status == WaitlistStatus.Open);

    public void Add(WalkUpWaitlist waitlist) => db.WalkUpWaitlists.Add(waitlist);

    public async Task SaveAsync() => await db.SaveChangesAsync();
}
```

Domain event dispatch happens in `ApplicationDbContext.SaveChangesAsync()` override — it collects events from all tracked `Entity` instances, saves, then publishes.

## EF Configuration

### WalkUpWaitlistConfiguration

```csharp
builder.ToTable("CourseWaitlists");  // keep existing table name
builder.HasKey(w => w.Id);
builder.Property(w => w.Status).HasConversion<string>();
builder.Property(w => w.ShortCode).IsRequired();
builder.HasMany(w => w.TeeTimeRequests)
    .WithOne()
    .HasForeignKey(r => r.WalkUpWaitlistId);
builder.Navigation(w => w.TeeTimeRequests)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

Store `WaitlistStatus` and `RequestStatus` as strings so existing data is compatible. No data migration needed — `"Open"`, `"Closed"`, `"Pending"` match enum member names exactly.

**Table name mapping:** `CourseWaitlists` and `WaitlistRequests` table names are preserved. Only the C# class names change.

## Global Exception Handler

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

## Endpoint After Refactor

Endpoints become thin HTTP adapters — parse request, load aggregate, call domain method, save, return response.

```csharp
private static async Task<IResult> OpenWaitlist(
    Guid courseId,
    ApplicationDbContext db,
    IWalkUpWaitlistRepository repo,
    IShortCodeGenerator shortCodeGenerator)
{
    var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
    if (course is null)
        return Results.NotFound(new { error = "Course not found." });

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var existing = await repo.GetByCourseDateAsync(courseId, today);

    if (existing is not null)
        return Results.Conflict(new { error = existing.Status == WaitlistStatus.Open
            ? "Walk-up waitlist is already open for today."
            : "Walk-up waitlist was already used today." });

    var waitlist = await WalkUpWaitlist.OpenAsync(courseId, today, shortCodeGenerator);

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
        return Results.NotFound(new { error = "No open walk-up waitlist found for today." });

    waitlist.Close();  // domain method — throws if not open
    await repo.SaveAsync();

    return Results.Ok(ToResponse(waitlist));
}

private static async Task<IResult> CreateWaitlistRequest(
    Guid courseId,
    CreateWalkUpWaitlistRequestRequest request,
    IWalkUpWaitlistRepository repo)
{
    // ... parse and validate request fields ...

    var waitlist = await repo.GetOpenByCourseDateAsync(courseId, parsedDate);
    if (waitlist is null)
        return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });

    var teeTimeRequest = waitlist.AddTeeTimeRequest(parsedTeeTime, request.GolfersNeeded);
    await repo.SaveAsync();  // also dispatches TeeTimeRequestAdded event

    return Results.Created(..., ToRequestResponse(teeTimeRequest));
}
```

## Migration Strategy

No database migration. Table names, column names, and stored values all stay the same:
- `CourseWaitlists` table maps to `WalkUpWaitlist` entity
- `WaitlistRequests` table maps to `TeeTimeRequest` entity
- Status strings (`"Open"`, `"Closed"`, `"Pending"`) match enum names exactly

Run `dotnet ef migrations has-pending-model-changes` to verify after implementation.
