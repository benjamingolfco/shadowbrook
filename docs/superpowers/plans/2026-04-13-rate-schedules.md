# Rate Schedules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add pricing rules to courses — a default price and optional rate schedules with day-of-week + time-band granularity, resolved at draft time, propagated on changes, and locked in at booking time.

**Architecture:** New `CoursePricingSettings` aggregate root (one per course) owns `RateSchedule` entities. Price resolution uses specificity-based matching (fewer days wins, narrower time band wins). Prices are stamped on `TeeSheetInterval` at draft time, locked on `TeeTimeClaim` at booking time, and propagated to `Booking`. A `PricingSettingsChanged` domain event triggers repricing of all future tee sheets.

**Tech Stack:** .NET 10, EF Core 10, Wolverine HTTP + messaging, FluentValidation, xUnit + NSubstitute

---

## File Structure

### New Files — Domain

| File | Responsibility |
|------|---------------|
| `src/backend/Teeforce.Domain/CoursePricingAggregate/CoursePricingSettings.cs` | Aggregate root — default price, min/max bounds, rate schedule collection, conflict detection, price resolution |
| `src/backend/Teeforce.Domain/CoursePricingAggregate/RateSchedule.cs` | Owned entity — name, days of week, time band, price |
| `src/backend/Teeforce.Domain/CoursePricingAggregate/ICoursePricingSettingsRepository.cs` | Repository interface |
| `src/backend/Teeforce.Domain/CoursePricingAggregate/Events/PricingSettingsChanged.cs` | Domain event raised when pricing config changes |
| `src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/ConflictingScheduleException.cs` | Thrown when two schedules with same specificity overlap |
| `src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/PriceOutOfBoundsException.cs` | Thrown when a price violates min/max bounds |

### New Files — API

| File | Responsibility |
|------|---------------|
| `src/backend/Teeforce.Api/Features/Pricing/Endpoints/PricingEndpoints.cs` | GET/PUT/POST/DELETE endpoints for pricing CRUD |
| `src/backend/Teeforce.Api/Features/Pricing/Handlers/PricingSettingsChanged/RepriceFutureSheetsHandler.cs` | Reprices all future tee sheets when pricing changes |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CoursePricingSettingsConfiguration.cs` | EF config for new aggregate + owned RateSchedule |
| `src/backend/Teeforce.Api/Infrastructure/Repositories/CoursePricingSettingsRepository.cs` | Repository implementation |

### New Files — Tests

| File | Responsibility |
|------|---------------|
| `tests/Teeforce.Domain.Tests/CoursePricingAggregate/CoursePricingSettingsTests.cs` | Domain unit tests for aggregate |
| `tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetPricingTests.cs` | Domain unit tests for ApplyPricing |
| `tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimePricingTests.cs` | Domain unit tests for price stamping on claims |
| `tests/Teeforce.Domain.Tests/BookingAggregate/BookingPricingTests.cs` | Domain unit tests for Booking pricing |
| `tests/Teeforce.Api.Tests/Features/Pricing/Validators/PricingValidatorTests.cs` | Validator unit tests |
| `tests/Teeforce.Api.Tests/Features/Pricing/Handlers/RepriceFutureSheetsHandlerTests.cs` | Handler unit tests |

### Modified Files

| File | Change |
|------|--------|
| `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs` | Add `Price` (decimal?) and `RateScheduleId` (Guid?) properties |
| `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs` | Add `ApplyPricing()` method |
| `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs` | Add `Price` (decimal?) property |
| `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs` | Stamp `Price` from interval onto claim |
| `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimed.cs` | Add `Price` (decimal?) field |
| `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs` | Add `PricePerPlayer` and `TotalPrice` properties |
| `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs` | Pass price from event |
| `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs` | Stamp prices at draft time |
| `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs` | Add `CoursePricingSettings` DbSet + configuration |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs` | Add Price/RateScheduleId columns to intervals |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs` | Add Price column to claims |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs` | Add PricePerPlayer/TotalPrice columns |
| `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs` | Map new domain exceptions |
| `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs` | Remove old pricing endpoints/DTOs |
| `src/backend/Teeforce.Domain/CourseAggregate/Course.cs` | Remove `FlatRatePrice` and `UpdatePricing()` |
| `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs` | Remove FlatRatePrice config |

---

## Task 1: CoursePricingSettings Aggregate + RateSchedule Domain Model

**Files:**
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/RateSchedule.cs`
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/CoursePricingSettings.cs`
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/ConflictingScheduleException.cs`
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/PriceOutOfBoundsException.cs`
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/Events/PricingSettingsChanged.cs`
- Create: `src/backend/Teeforce.Domain/CoursePricingAggregate/ICoursePricingSettingsRepository.cs`

- [ ] **Step 1: Create RateSchedule owned entity**

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/RateSchedule.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate;

public class RateSchedule : Entity
{
    public Guid CoursePricingSettingsId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DayOfWeek[] DaysOfWeek { get; private set; } = [];
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public decimal Price { get; private set; }

    private RateSchedule() { } // EF

    internal RateSchedule(Guid parentId, string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        Id = Guid.CreateVersion7();
        CoursePricingSettingsId = parentId;
        Name = name;
        DaysOfWeek = daysOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Price = price;
    }

    internal void Update(string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        Name = name;
        DaysOfWeek = daysOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        Price = price;
    }

    internal TimeSpan TimeBandWidth => EndTime.ToTimeSpan() - StartTime.ToTimeSpan();

    internal bool OverlapsTimeWith(RateSchedule other) =>
        StartTime < other.EndTime && other.StartTime < EndTime;

    internal bool SharesDayWith(RateSchedule other) =>
        DaysOfWeek.Any(d => other.DaysOfWeek.Contains(d));

    internal bool HasSameSpecificityAs(RateSchedule other) =>
        DaysOfWeek.Length == other.DaysOfWeek.Length && TimeBandWidth == other.TimeBandWidth;
}
```

- [ ] **Step 2: Create domain exceptions**

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/ConflictingScheduleException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Exceptions;

public class ConflictingScheduleException(string scheduleName, string conflictingScheduleName)
    : DomainException($"Schedule '{scheduleName}' conflicts with '{conflictingScheduleName}' — same specificity on overlapping day+time.")
{
    public string ScheduleName { get; } = scheduleName;
    public string ConflictingScheduleName { get; } = conflictingScheduleName;
}
```

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/Exceptions/PriceOutOfBoundsException.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Exceptions;

public class PriceOutOfBoundsException(decimal price, decimal? minPrice, decimal? maxPrice)
    : DomainException($"Price {price} is outside the allowed range [{minPrice?.ToString() ?? "∞"}, {maxPrice?.ToString() ?? "∞"}].")
{
    public decimal Price { get; } = price;
    public decimal? MinPrice { get; } = minPrice;
    public decimal? MaxPrice { get; } = maxPrice;
}
```

- [ ] **Step 3: Create PricingSettingsChanged domain event**

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/Events/PricingSettingsChanged.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Events;

public record PricingSettingsChanged : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid CourseId { get; init; }
}
```

- [ ] **Step 4: Create CoursePricingSettings aggregate root**

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/CoursePricingSettings.cs
using Teeforce.Domain.Common;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.CoursePricingAggregate.Exceptions;

namespace Teeforce.Domain.CoursePricingAggregate;

public class CoursePricingSettings : Entity
{
    public Guid CourseId { get; private set; }
    public decimal? DefaultPrice { get; private set; }
    public decimal? MinPrice { get; private set; }
    public decimal? MaxPrice { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<RateSchedule> rateSchedules = [];
    public IReadOnlyList<RateSchedule> RateSchedules => this.rateSchedules.AsReadOnly();

    private CoursePricingSettings() { } // EF

    public static CoursePricingSettings Create(Guid courseId, decimal? defaultPrice = null)
    {
        return new CoursePricingSettings
        {
            Id = Guid.CreateVersion7(),
            CourseId = courseId,
            DefaultPrice = defaultPrice,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateDefaultPrice(decimal? defaultPrice)
    {
        if (defaultPrice is not null)
        {
            ValidatePriceBounds(defaultPrice.Value);
        }
        DefaultPrice = defaultPrice;
        RaisePricingChanged();
    }

    public void UpdateBounds(decimal? minPrice, decimal? maxPrice)
    {
        MinPrice = minPrice;
        MaxPrice = maxPrice;

        // Validate existing default price against new bounds
        if (DefaultPrice is not null)
        {
            ValidatePriceBounds(DefaultPrice.Value);
        }

        // Validate all existing schedule prices against new bounds
        foreach (var schedule in this.rateSchedules)
        {
            ValidatePriceBounds(schedule.Price);
        }

        RaisePricingChanged();
    }

    public RateSchedule AddSchedule(string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        if (daysOfWeek.Length == 0)
        {
            throw new DomainException("At least one day of week is required.");
        }
        if (startTime >= endTime)
        {
            throw new DomainException("Start time must be before end time.");
        }
        if (price <= 0)
        {
            throw new DomainException("Price must be greater than zero.");
        }
        ValidatePriceBounds(price);

        var schedule = new RateSchedule(Id, name, daysOfWeek, startTime, endTime, price);
        CheckForConflicts(schedule);
        this.rateSchedules.Add(schedule);
        RaisePricingChanged();
        return schedule;
    }

    public void UpdateSchedule(Guid scheduleId, string name, DayOfWeek[] daysOfWeek, TimeOnly startTime, TimeOnly endTime, decimal price)
    {
        var schedule = this.rateSchedules.FirstOrDefault(s => s.Id == scheduleId)
            ?? throw new EntityNotFoundException(nameof(RateSchedule), scheduleId);

        if (daysOfWeek.Length == 0)
        {
            throw new DomainException("At least one day of week is required.");
        }
        if (startTime >= endTime)
        {
            throw new DomainException("Start time must be before end time.");
        }
        if (price <= 0)
        {
            throw new DomainException("Price must be greater than zero.");
        }
        ValidatePriceBounds(price);

        // Create a temp schedule to check conflicts (excluding the one being updated)
        var temp = new RateSchedule(Id, name, daysOfWeek, startTime, endTime, price);
        CheckForConflicts(temp, excludeId: scheduleId);

        schedule.Update(name, daysOfWeek, startTime, endTime, price);
        RaisePricingChanged();
    }

    public void RemoveSchedule(Guid scheduleId)
    {
        var schedule = this.rateSchedules.FirstOrDefault(s => s.Id == scheduleId)
            ?? throw new EntityNotFoundException(nameof(RateSchedule), scheduleId);

        this.rateSchedules.Remove(schedule);
        RaisePricingChanged();
    }

    public decimal? ResolvePrice(DayOfWeek day, TimeOnly time)
    {
        var matches = this.rateSchedules
            .Where(s => s.DaysOfWeek.Contains(day) && s.StartTime <= time && time < s.EndTime)
            .OrderBy(s => s.DaysOfWeek.Length)
            .ThenBy(s => s.TimeBandWidth)
            .ToList();

        if (matches.Count > 0)
        {
            return matches[0].Price;
        }

        return DefaultPrice;
    }

    public (decimal? Price, Guid? RateScheduleId) ResolvePriceWithSource(DayOfWeek day, TimeOnly time)
    {
        var matches = this.rateSchedules
            .Where(s => s.DaysOfWeek.Contains(day) && s.StartTime <= time && time < s.EndTime)
            .OrderBy(s => s.DaysOfWeek.Length)
            .ThenBy(s => s.TimeBandWidth)
            .ToList();

        if (matches.Count > 0)
        {
            return (matches[0].Price, matches[0].Id);
        }

        return (DefaultPrice, null);
    }

    private void ValidatePriceBounds(decimal price)
    {
        if (MinPrice is not null && price < MinPrice)
        {
            throw new PriceOutOfBoundsException(price, MinPrice, MaxPrice);
        }
        if (MaxPrice is not null && price > MaxPrice)
        {
            throw new PriceOutOfBoundsException(price, MinPrice, MaxPrice);
        }
    }

    private void CheckForConflicts(RateSchedule candidate, Guid? excludeId = null)
    {
        foreach (var existing in this.rateSchedules)
        {
            if (excludeId is not null && existing.Id == excludeId)
            {
                continue;
            }
            if (candidate.SharesDayWith(existing) && candidate.OverlapsTimeWith(existing) && candidate.HasSameSpecificityAs(existing))
            {
                throw new ConflictingScheduleException(candidate.Name, existing.Name);
            }
        }
    }

    private void RaisePricingChanged()
    {
        AddDomainEvent(new PricingSettingsChanged { CourseId = CourseId });
    }
}
```

- [ ] **Step 5: Create repository interface**

```csharp
// src/backend/Teeforce.Domain/CoursePricingAggregate/ICoursePricingSettingsRepository.cs
using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate;

public interface ICoursePricingSettingsRepository : IRepository<CoursePricingSettings>
{
    void Add(CoursePricingSettings settings);
    Task<CoursePricingSettings?> GetByCourseIdAsync(Guid courseId, CancellationToken ct = default);
}
```

- [ ] **Step 6: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Domain/CoursePricingAggregate/
git commit -m "feat(domain): add CoursePricingSettings aggregate with RateSchedule owned entity

Adds the core domain model for rate schedules including conflict detection,
price resolution, and bounds validation. Part of #401."
```

---

## Task 2: Domain Tests for CoursePricingSettings

**Files:**
- Create: `tests/Teeforce.Domain.Tests/CoursePricingAggregate/CoursePricingSettingsTests.cs`

- [ ] **Step 1: Write tests for aggregate creation and default price**

```csharp
// tests/Teeforce.Domain.Tests/CoursePricingAggregate/CoursePricingSettingsTests.cs
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.CoursePricingAggregate.Exceptions;
using Teeforce.Domain.Common;

namespace Teeforce.Domain.Tests.CoursePricingAggregate;

public class CoursePricingSettingsTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var courseId = Guid.NewGuid();

        var settings = CoursePricingSettings.Create(courseId, defaultPrice: 50m);

        Assert.NotEqual(Guid.Empty, settings.Id);
        Assert.Equal(courseId, settings.CourseId);
        Assert.Equal(50m, settings.DefaultPrice);
        Assert.Null(settings.MinPrice);
        Assert.Null(settings.MaxPrice);
        Assert.Empty(settings.RateSchedules);
    }

    [Fact]
    public void Create_WithNoDefaultPrice_DefaultPriceIsNull()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Null(settings.DefaultPrice);
    }

    [Fact]
    public void UpdateDefaultPrice_SetsValueAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.ClearDomainEvents();

        settings.UpdateDefaultPrice(75m);

        Assert.Equal(75m, settings.DefaultPrice);
        var evt = Assert.Single(settings.DomainEvents);
        Assert.IsType<PricingSettingsChanged>(evt);
    }

    [Fact]
    public void UpdateDefaultPrice_ToNull_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.ClearDomainEvents();

        settings.UpdateDefaultPrice(null);

        Assert.Null(settings.DefaultPrice);
    }

    [Fact]
    public void UpdateDefaultPrice_ViolatesBounds_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(minPrice: 20m, maxPrice: 100m);
        settings.ClearDomainEvents();

        Assert.Throws<PriceOutOfBoundsException>(() => settings.UpdateDefaultPrice(10m));
        Assert.Throws<PriceOutOfBoundsException>(() => settings.UpdateDefaultPrice(150m));
    }

    [Fact]
    public void UpdateBounds_SetsValuesAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.ClearDomainEvents();

        settings.UpdateBounds(minPrice: 10m, maxPrice: 200m);

        Assert.Equal(10m, settings.MinPrice);
        Assert.Equal(200m, settings.MaxPrice);
        var evt = Assert.Single(settings.DomainEvents);
        Assert.IsType<PricingSettingsChanged>(evt);
    }

    [Fact]
    public void UpdateBounds_ExistingDefaultPriceOutOfBounds_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.ClearDomainEvents();

        Assert.Throws<PriceOutOfBoundsException>(() => settings.UpdateBounds(minPrice: 60m, maxPrice: 200m));
    }

    [Fact]
    public void UpdateBounds_ExistingSchedulePriceOutOfBounds_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 30m);
        settings.ClearDomainEvents();

        Assert.Throws<PriceOutOfBoundsException>(() => settings.UpdateBounds(minPrice: 40m, maxPrice: 200m));
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~CoursePricingSettingsTests" --no-build`
Expected: All PASS

- [ ] **Step 3: Write tests for AddSchedule and RemoveSchedule**

Add to the same test file:

```csharp
    [Fact]
    public void AddSchedule_CreatesScheduleAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.ClearDomainEvents();

        var schedule = settings.AddSchedule(
            "Weekend Morning", [DayOfWeek.Saturday, DayOfWeek.Sunday],
            new TimeOnly(6, 0), new TimeOnly(12, 0), 80m);

        Assert.Single(settings.RateSchedules);
        Assert.Equal("Weekend Morning", schedule.Name);
        Assert.Equal(80m, schedule.Price);
        Assert.Equal(new TimeOnly(6, 0), schedule.StartTime);
        Assert.Equal(new TimeOnly(12, 0), schedule.EndTime);
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], schedule.DaysOfWeek);
        var evt = Assert.Single(settings.DomainEvents);
        Assert.IsType<PricingSettingsChanged>(evt);
    }

    [Fact]
    public void AddSchedule_EmptyDays_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Bad", [], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m));
    }

    [Fact]
    public void AddSchedule_StartAfterEnd_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Bad", [DayOfWeek.Monday], new TimeOnly(14, 0), new TimeOnly(8, 0), 50m));
    }

    [Fact]
    public void AddSchedule_ZeroPrice_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Bad", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 0m));
    }

    [Fact]
    public void AddSchedule_PriceOutOfBounds_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(minPrice: 20m, maxPrice: 100m);
        settings.ClearDomainEvents();

        Assert.Throws<PriceOutOfBoundsException>(() =>
            settings.AddSchedule("Cheap", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 10m));
    }

    [Fact]
    public void RemoveSchedule_RemovesAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        settings.ClearDomainEvents();

        settings.RemoveSchedule(schedule.Id);

        Assert.Empty(settings.RateSchedules);
        var evt = Assert.Single(settings.DomainEvents);
        Assert.IsType<PricingSettingsChanged>(evt);
    }

    [Fact]
    public void RemoveSchedule_NotFound_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<EntityNotFoundException>(() => settings.RemoveSchedule(Guid.NewGuid()));
    }
```

- [ ] **Step 4: Write tests for conflict detection**

Add to the same test file:

```csharp
    [Fact]
    public void AddSchedule_ConflictingSameSpecificity_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        // Same day count (1), same time band width (6h), overlapping time on same day
        Assert.Throws<ConflictingScheduleException>(() =>
            settings.AddSchedule("Monday Overlap", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(14, 0), 60m));
    }

    [Fact]
    public void AddSchedule_OverlappingDifferentSpecificity_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        // 2-day schedule, 6h band
        settings.AddSchedule("Weekend Morning", [DayOfWeek.Saturday, DayOfWeek.Sunday], new TimeOnly(6, 0), new TimeOnly(12, 0), 80m);

        // 1-day schedule, 6h band — different specificity (fewer days), so no conflict
        var schedule = settings.AddSchedule("Saturday Morning", [DayOfWeek.Saturday], new TimeOnly(6, 0), new TimeOnly(12, 0), 90m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_OverlappingDifferentTimeBandWidth_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        // 1-day, 6h band
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        // 1-day, 2h band — different specificity (narrower), so no conflict
        var schedule = settings.AddSchedule("Monday Peak", [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(11, 0), 70m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_DifferentDaysNoConflict()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        // Same specificity but different day — no overlap
        settings.AddSchedule("Tuesday Morning", [DayOfWeek.Tuesday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_NonOverlappingTimeSameDay_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        // Same day, same specificity, but non-overlapping times
        settings.AddSchedule("Monday Afternoon", [DayOfWeek.Monday], new TimeOnly(12, 0), new TimeOnly(18, 0), 60m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }
```

- [ ] **Step 5: Write tests for UpdateSchedule**

```csharp
    [Fact]
    public void UpdateSchedule_UpdatesAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        settings.ClearDomainEvents();

        settings.UpdateSchedule(schedule.Id, "Updated Morning", [DayOfWeek.Monday, DayOfWeek.Tuesday],
            new TimeOnly(7, 0), new TimeOnly(11, 0), 55m);

        var updated = settings.RateSchedules.Single();
        Assert.Equal("Updated Morning", updated.Name);
        Assert.Equal(55m, updated.Price);
        Assert.Equal(new TimeOnly(7, 0), updated.StartTime);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], updated.DaysOfWeek);
        var evt = Assert.Single(settings.DomainEvents);
        Assert.IsType<PricingSettingsChanged>(evt);
    }

    [Fact]
    public void UpdateSchedule_NotFound_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<EntityNotFoundException>(() =>
            settings.UpdateSchedule(Guid.NewGuid(), "X", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m));
    }

    [Fact]
    public void UpdateSchedule_ConflictsWithOther_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var afternoon = settings.AddSchedule("Afternoon", [DayOfWeek.Monday], new TimeOnly(12, 0), new TimeOnly(18, 0), 60m);

        // Try to update afternoon to overlap morning with same specificity
        Assert.Throws<ConflictingScheduleException>(() =>
            settings.UpdateSchedule(afternoon.Id, "Overlap", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(14, 0), 65m));
    }

    [Fact]
    public void UpdateSchedule_SameValuesNoConflictWithSelf()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        settings.ClearDomainEvents();

        // Updating to same values should not conflict with itself
        settings.UpdateSchedule(schedule.Id, "Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 55m);

        Assert.Equal(55m, settings.RateSchedules.Single().Price);
    }
```

- [ ] **Step 6: Write tests for price resolution**

```csharp
    [Fact]
    public void ResolvePrice_NoSchedulesNoDefault_ReturnsNull()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Null(price);
    }

    [Fact]
    public void ResolvePrice_NoSchedules_ReturnsDefault()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(50m, price);
    }

    [Fact]
    public void ResolvePrice_MatchingSchedule_ReturnsSchedulePrice()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(75m, price);
    }

    [Fact]
    public void ResolvePrice_NoMatchingSchedule_FallsBackToDefault()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        // Tuesday — no schedule matches
        var price = settings.ResolvePrice(DayOfWeek.Tuesday, new TimeOnly(9, 0));

        Assert.Equal(50m, price);
    }

    [Fact]
    public void ResolvePrice_FewerDaysWins()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        // 2-day schedule
        settings.AddSchedule("Weekend", [DayOfWeek.Saturday, DayOfWeek.Sunday], new TimeOnly(6, 0), new TimeOnly(12, 0), 80m);
        // 1-day schedule — more specific
        settings.AddSchedule("Saturday Only", [DayOfWeek.Saturday], new TimeOnly(6, 0), new TimeOnly(12, 0), 90m);

        var price = settings.ResolvePrice(DayOfWeek.Saturday, new TimeOnly(9, 0));

        Assert.Equal(90m, price);
    }

    [Fact]
    public void ResolvePrice_NarrowerTimeBandWins()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        // Broad time band
        settings.AddSchedule("Monday Full", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(18, 0), 60m);
        // Narrow time band — more specific
        settings.AddSchedule("Monday Peak", [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(11, 0), 85m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(10, 0));

        Assert.Equal(85m, price);
    }

    [Fact]
    public void ResolvePrice_TimeAtStartBoundary_Matches()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        // Exactly at start time — inclusive
        Assert.Equal(75m, settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(6, 0)));
    }

    [Fact]
    public void ResolvePrice_TimeAtEndBoundary_DoesNotMatch()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        // Exactly at end time — exclusive, falls back to default
        Assert.Equal(50m, settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void ResolvePriceWithSource_ReturnsScheduleId()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        var (price, scheduleId) = settings.ResolvePriceWithSource(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(75m, price);
        Assert.Equal(schedule.Id, scheduleId);
    }

    [Fact]
    public void ResolvePriceWithSource_DefaultPrice_ReturnsNullScheduleId()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);

        var (price, scheduleId) = settings.ResolvePriceWithSource(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(50m, price);
        Assert.Null(scheduleId);
    }
```

- [ ] **Step 7: Run all tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~CoursePricingSettingsTests"`
Expected: All PASS

- [ ] **Step 8: Commit**

```bash
git add tests/Teeforce.Domain.Tests/CoursePricingAggregate/
git commit -m "test(domain): add CoursePricingSettings domain tests

Covers creation, default price, bounds, schedule CRUD, conflict detection,
and specificity-based price resolution. Part of #401."
```

---

## Task 3: TeeSheetInterval Pricing + TeeSheet.ApplyPricing

**Files:**
- Modify: `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs`
- Modify: `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs`
- Create: `tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetPricingTests.cs`

- [ ] **Step 1: Add Price and RateScheduleId to TeeSheetInterval**

In `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs`, the file should become:

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheetInterval : Entity
{
    public Guid TeeSheetId { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }
    public decimal? Price { get; private set; }
    public Guid? RateScheduleId { get; private set; }

    private TeeSheetInterval() { } // EF

    internal TeeSheetInterval(Guid teeSheetId, TimeOnly time, int capacity)
    {
        Id = Guid.CreateVersion7();
        TeeSheetId = teeSheetId;
        Time = time;
        Capacity = capacity;
    }

    internal void SetPricing(decimal? price, Guid? rateScheduleId)
    {
        Price = price;
        RateScheduleId = rateScheduleId;
    }
}
```

- [ ] **Step 2: Add ApplyPricing to TeeSheet**

In `src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs`, add after the `Unpublish` method (before `AuthorizeBooking`):

```csharp
    public void ApplyPricing(Func<DayOfWeek, TimeOnly, (decimal? Price, Guid? RateScheduleId)> resolver)
    {
        var dayOfWeek = Date.DayOfWeek;
        foreach (var interval in this.intervals)
        {
            var (price, scheduleId) = resolver(dayOfWeek, interval.Time);
            interval.SetPricing(price, scheduleId);
        }
    }
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 4: Write failing tests for TeeSheet pricing**

```csharp
// tests/Teeforce.Domain.Tests/TeeSheetAggregate/TeeSheetPricingTests.cs
using NSubstitute;
using Teeforce.Domain.TeeSheetAggregate;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Domain.Tests.TeeSheetAggregate;

public class TeeSheetPricingTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeSheetPricingTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private TeeSheet CreateSheet(DateOnly? date = null)
    {
        var settings = new ScheduleSettings(
            firstTeeTime: new TimeOnly(7, 0),
            lastTeeTime: new TimeOnly(8, 0),
            intervalMinutes: 10,
            defaultCapacity: 4);

        return TeeSheet.Draft(
            Guid.NewGuid(),
            date ?? new DateOnly(2026, 6, 1), // Monday
            settings,
            this.timeProvider);
    }

    [Fact]
    public void ApplyPricing_StampsPriceAndScheduleIdOnEachInterval()
    {
        var sheet = CreateSheet();
        var scheduleId = Guid.NewGuid();

        sheet.ApplyPricing((day, time) => (50m, scheduleId));

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Equal(50m, interval.Price);
            Assert.Equal(scheduleId, interval.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_NullPrice_SetsNullOnIntervals()
    {
        var sheet = CreateSheet();

        sheet.ApplyPricing((day, time) => (null, null));

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Null(interval.Price);
            Assert.Null(interval.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_DifferentPricesPerTime_StampsCorrectly()
    {
        var sheet = CreateSheet();
        var earlyId = Guid.NewGuid();

        sheet.ApplyPricing((day, time) =>
            time < new TimeOnly(7, 30)
                ? (60m, earlyId)
                : (40m, null));

        var earlyIntervals = sheet.Intervals.Where(i => i.Time < new TimeOnly(7, 30)).ToList();
        var lateIntervals = sheet.Intervals.Where(i => i.Time >= new TimeOnly(7, 30)).ToList();

        Assert.All(earlyIntervals, i =>
        {
            Assert.Equal(60m, i.Price);
            Assert.Equal(earlyId, i.RateScheduleId);
        });
        Assert.All(lateIntervals, i =>
        {
            Assert.Equal(40m, i.Price);
            Assert.Null(i.RateScheduleId);
        });
    }

    [Fact]
    public void ApplyPricing_PassesCorrectDayOfWeek()
    {
        // June 1, 2026 is a Monday
        var sheet = CreateSheet(date: new DateOnly(2026, 6, 1));
        DayOfWeek? capturedDay = null;

        sheet.ApplyPricing((day, time) =>
        {
            capturedDay = day;
            return (50m, null);
        });

        Assert.Equal(DayOfWeek.Monday, capturedDay);
    }

    [Fact]
    public void Intervals_InitiallyHaveNullPricing()
    {
        var sheet = CreateSheet();

        Assert.All(sheet.Intervals, interval =>
        {
            Assert.Null(interval.Price);
            Assert.Null(interval.RateScheduleId);
        });
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~TeeSheetPricingTests"`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheetInterval.cs \
        src/backend/Teeforce.Domain/TeeSheetAggregate/TeeSheet.cs \
        tests/Teeforce.Domain.Tests/TeeSheetAggregate/
git commit -m "feat(domain): add pricing support to TeeSheetInterval and TeeSheet.ApplyPricing

Intervals now carry Price and RateScheduleId. TeeSheet.ApplyPricing()
accepts a resolver function and stamps each interval. Part of #401."
```

---

## Task 4: Price on TeeTimeClaim + TeeTime Stamping

**Files:**
- Modify: `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs`
- Modify: `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs`
- Modify: `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimed.cs`
- Create: `tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimePricingTests.cs`

- [ ] **Step 1: Add Price to TeeTimeClaim**

In `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTimeClaim.cs`, the file should become:

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate;

public class TeeTimeClaim : Entity
{
    public Guid TeeTimeId { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid GolferId { get; private set; }
    public int GroupSize { get; private set; }
    public decimal? Price { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }

    private TeeTimeClaim() { } // EF

    internal TeeTimeClaim(Guid teeTimeId, Guid bookingId, Guid golferId, int groupSize, decimal? price, DateTimeOffset claimedAt)
    {
        Id = Guid.CreateVersion7();
        TeeTimeId = teeTimeId;
        BookingId = bookingId;
        GolferId = golferId;
        GroupSize = groupSize;
        Price = price;
        ClaimedAt = claimedAt;
    }
}
```

- [ ] **Step 2: Add Price to TeeTimeClaimed event**

In `src/backend/Teeforce.Domain/TeeTimeAggregate/Events/TeeTimeClaimed.cs`, add the Price field:

```csharp
using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Events;

public record TeeTimeClaimed : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required Guid TeeTimeId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GolferId { get; init; }
    public required int GroupSize { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly Time { get; init; }
    public decimal? Price { get; init; }
}
```

- [ ] **Step 3: Update TeeTime to stamp price from interval and pass through to claim/event**

In `src/backend/Teeforce.Domain/TeeTimeAggregate/TeeTime.cs`, update the static `Claim` factory to read `interval.Price`:

Change line 67:
```csharp
        teeTime.ApplyClaim(bookingId, golferId, groupSize, now);
```
to:
```csharp
        teeTime.ApplyClaim(bookingId, golferId, groupSize, interval.Price, now);
```

Change line 99:
```csharp
        ApplyClaim(bookingId, golferId, groupSize, timeProvider.GetCurrentTimestamp());
```
to:
```csharp
        ApplyClaim(bookingId, golferId, groupSize, null, timeProvider.GetCurrentTimestamp());
```

Update the `ApplyClaim` signature and body (line 102+):
```csharp
    private void ApplyClaim(Guid bookingId, Guid golferId, int groupSize, decimal? price, DateTimeOffset now)
    {
        this.claims.Add(new TeeTimeClaim(Id, bookingId, golferId, groupSize, price, now));
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
            Price = price,
        });

        AddDomainEvent(new TeeTimeAvailabilityChanged
        {
            TeeTimeId = Id,
            Remaining = Remaining,
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
```

Note: The instance `Claim` method passes `null` for price because when claiming onto an existing TeeTime, the original interval is not available. The first claim (from static factory) captures the interval price.

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 5: Write tests**

```csharp
// tests/Teeforce.Domain.Tests/TeeTimeAggregate/TeeTimePricingTests.cs
using NSubstitute;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Teeforce.Domain.TeeTimeAggregate.Events;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Domain.Tests.TeeTimeAggregate;

public class TeeTimePricingTests
{
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public TeeTimePricingTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    private (TeeSheet Sheet, TeeSheetInterval Interval) CreateSheetAndInterval(decimal? price = null, Guid? rateScheduleId = null)
    {
        var settings = new ScheduleSettings(
            firstTeeTime: new TimeOnly(9, 0),
            lastTeeTime: new TimeOnly(10, 0),
            intervalMinutes: 10,
            defaultCapacity: 4);

        var sheet = TeeSheet.Draft(Guid.NewGuid(), new DateOnly(2026, 6, 1), settings, this.timeProvider);

        if (price is not null || rateScheduleId is not null)
        {
            sheet.ApplyPricing((_, _) => (price, rateScheduleId));
        }

        sheet.Publish(this.timeProvider);
        var interval = sheet.Intervals[0];
        return (sheet, interval);
    }

    [Fact]
    public void Claim_StampsPriceFromInterval()
    {
        var scheduleId = Guid.NewGuid();
        var (sheet, interval) = CreateSheetAndInterval(price: 75m, rateScheduleId: scheduleId);
        var auth = sheet.AuthorizeBooking();

        var teeTime = TeeTime.Claim(
            interval, sheet.CourseId, sheet.Date, auth,
            Guid.NewGuid(), Guid.NewGuid(), groupSize: 2, this.timeProvider);

        var claim = teeTime.Claims.Single();
        Assert.Equal(75m, claim.Price);
    }

    [Fact]
    public void Claim_NullIntervalPrice_ClaimPriceIsNull()
    {
        var (sheet, interval) = CreateSheetAndInterval();
        var auth = sheet.AuthorizeBooking();

        var teeTime = TeeTime.Claim(
            interval, sheet.CourseId, sheet.Date, auth,
            Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        var claim = teeTime.Claims.Single();
        Assert.Null(claim.Price);
    }

    [Fact]
    public void Claim_TeeTimeClaimedEvent_CarriesPrice()
    {
        var (sheet, interval) = CreateSheetAndInterval(price: 60m);
        var auth = sheet.AuthorizeBooking();

        var teeTime = TeeTime.Claim(
            interval, sheet.CourseId, sheet.Date, auth,
            Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        var claimed = teeTime.DomainEvents.OfType<TeeTimeClaimed>().Single();
        Assert.Equal(60m, claimed.Price);
    }

    [Fact]
    public void Claim_InstanceMethod_PriceIsNull()
    {
        var (sheet, interval) = CreateSheetAndInterval(price: 60m);
        var auth = sheet.AuthorizeBooking();

        var teeTime = TeeTime.Claim(
            interval, sheet.CourseId, sheet.Date, auth,
            Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        teeTime.ClearDomainEvents();
        var auth2 = sheet.AuthorizeBooking();
        teeTime.Claim(auth2, Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        var secondClaim = teeTime.Claims[1];
        Assert.Null(secondClaim.Price);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~TeeTimePricingTests"`
Expected: All PASS

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Domain/TeeTimeAggregate/ \
        tests/Teeforce.Domain.Tests/TeeTimeAggregate/
git commit -m "feat(domain): stamp price on TeeTimeClaim at booking time

TeeTimeClaim captures interval.Price at claim time. TeeTimeClaimed event
carries Price for downstream consumers. Part of #401."
```

---

## Task 5: Booking Pricing

**Files:**
- Modify: `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs`
- Modify: `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs`
- Create: `tests/Teeforce.Domain.Tests/BookingAggregate/BookingPricingTests.cs`

- [ ] **Step 1: Add PricePerPlayer and TotalPrice to Booking**

In `src/backend/Teeforce.Domain/BookingAggregate/Booking.cs`, add properties after `PlayerCount`:

```csharp
    public decimal? PricePerPlayer { get; private set; }
    public decimal? TotalPrice { get; private set; }
```

Update `CreateConfirmed` to accept and store price. Change the method signature:

```csharp
    public static Booking CreateConfirmed(
        Guid bookingId,
        Guid courseId,
        Guid golferId,
        Guid? teeTimeId,
        DateOnly date,
        TimeOnly teeTime,
        int playerCount,
        decimal? pricePerPlayer = null)
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
            PricePerPlayer = pricePerPlayer,
            TotalPrice = pricePerPlayer is not null ? pricePerPlayer.Value * playerCount : null,
            Status = BookingStatus.Confirmed,
            CreatedAt = now
        };

        booking.AddDomainEvent(new BookingConfirmed { BookingId = bookingId });

        return booking;
    }
```

- [ ] **Step 2: Update CreateConfirmedBookingHandler to pass price**

In `src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs`:

```csharp
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
            playerCount: evt.GroupSize,
            pricePerPlayer: evt.Price);

        bookingRepository.Add(booking);
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 4: Write tests**

```csharp
// tests/Teeforce.Domain.Tests/BookingAggregate/BookingPricingTests.cs
using Teeforce.Domain.BookingAggregate;

namespace Teeforce.Domain.Tests.BookingAggregate;

public class BookingPricingTests
{
    [Fact]
    public void CreateConfirmed_WithPrice_CalculatesTotalPrice()
    {
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            playerCount: 3,
            pricePerPlayer: 50m);

        Assert.Equal(50m, booking.PricePerPlayer);
        Assert.Equal(150m, booking.TotalPrice);
    }

    [Fact]
    public void CreateConfirmed_NullPrice_BothFieldsNull()
    {
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            playerCount: 2);

        Assert.Null(booking.PricePerPlayer);
        Assert.Null(booking.TotalPrice);
    }

    [Fact]
    public void CreateConfirmed_SinglePlayer_TotalEqualsPerPlayer()
    {
        var booking = Booking.CreateConfirmed(
            bookingId: Guid.NewGuid(),
            courseId: Guid.NewGuid(),
            golferId: Guid.NewGuid(),
            teeTimeId: Guid.NewGuid(),
            date: new DateOnly(2026, 6, 1),
            teeTime: new TimeOnly(9, 0),
            playerCount: 1,
            pricePerPlayer: 75m);

        Assert.Equal(75m, booking.PricePerPlayer);
        Assert.Equal(75m, booking.TotalPrice);
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Teeforce.Domain.Tests --filter "FullyQualifiedName~BookingPricingTests"`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Domain/BookingAggregate/Booking.cs \
        src/backend/Teeforce.Api/Features/Bookings/Handlers/TeeTimeClaimed/CreateConfirmedBookingHandler.cs \
        tests/Teeforce.Domain.Tests/BookingAggregate/
git commit -m "feat(domain): add PricePerPlayer and TotalPrice to Booking

Booking.CreateConfirmed accepts optional price. Handler passes price from
TeeTimeClaimed event. Total calculated at creation. Part of #401."
```

---

## Task 6: EF Configurations

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CoursePricingSettingsConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Read the TeeTimeConfiguration and BookingConfiguration files**

Read `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs` and `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs` to understand existing structure.

- [ ] **Step 2: Create CoursePricingSettingsConfiguration**

```csharp
// src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CoursePricingSettingsConfiguration.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CourseAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class CoursePricingSettingsConfiguration : IEntityTypeConfiguration<CoursePricingSettings>
{
    public void Configure(EntityTypeBuilder<CoursePricingSettings> builder)
    {
        builder.ToTable("CoursePricingSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CourseId).IsRequired();
        builder.Property(s => s.DefaultPrice).HasPrecision(18, 2);
        builder.Property(s => s.MinPrice).HasPrecision(18, 2);
        builder.Property(s => s.MaxPrice).HasPrecision(18, 2);
        builder.Property(s => s.CreatedAt);

        builder.OwnsMany(s => s.RateSchedules, rs =>
        {
            rs.ToTable("RateSchedules");
            rs.WithOwner().HasForeignKey(x => x.CoursePricingSettingsId);
            rs.HasKey(x => x.Id);
            rs.Property(x => x.Id).ValueGeneratedNever();
            rs.Property(x => x.Name).IsRequired().HasMaxLength(200);
            rs.Property(x => x.DaysOfWeek)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<DayOfWeek[]>(v, (JsonSerializerOptions?)null) ?? [])
                .HasColumnType("nvarchar(200)");
            rs.Property(x => x.StartTime).HasColumnType("time");
            rs.Property(x => x.EndTime).HasColumnType("time");
            rs.Property(x => x.Price).HasPrecision(18, 2);
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CourseId).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
```

- [ ] **Step 3: Add Price columns to TeeSheetInterval**

In `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs`, inside the `OwnsMany(s => s.Intervals, i => { ... })` block, after `i.Property(x => x.Capacity);`, add:

```csharp
            i.Property(x => x.Price).HasPrecision(18, 2);
            i.Property(x => x.RateScheduleId);
```

- [ ] **Step 4: Add Price column to TeeTimeClaim**

Read `TeeTimeConfiguration.cs`, then inside the `OwnsMany(t => t.Claims, ...)` block, add:

```csharp
            c.Property(x => x.Price).HasPrecision(18, 2);
```

- [ ] **Step 5: Add PricePerPlayer and TotalPrice columns to Booking**

In `BookingConfiguration.cs`, add after existing property configurations:

```csharp
        builder.Property(b => b.PricePerPlayer).HasPrecision(18, 2);
        builder.Property(b => b.TotalPrice).HasPrecision(18, 2);
```

- [ ] **Step 6: Register in ApplicationDbContext**

In `src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs`:

1. Add using: `using Teeforce.Domain.CoursePricingAggregate;`
2. Add DbSet after the `TeeTimeOffers` line: `public DbSet<CoursePricingSettings> CoursePricingSettings => Set<CoursePricingSettings>();`
3. Add configuration in `OnModelCreating` after the last `ApplyConfiguration`: `modelBuilder.ApplyConfiguration(new CoursePricingSettingsConfiguration());`

- [ ] **Step 7: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CoursePricingSettingsConfiguration.cs \
        src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeSheetConfiguration.cs \
        src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/TeeTimeConfiguration.cs \
        src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/BookingConfiguration.cs \
        src/backend/Teeforce.Api/Infrastructure/Data/ApplicationDbContext.cs
git commit -m "feat(api): add EF configurations for pricing

CoursePricingSettings with owned RateSchedules table. Price columns on
TeeSheetInterval, TeeTimeClaim, and Booking. Part of #401."
```

---

## Task 7: Repository + Migration

**Files:**
- Create: `src/backend/Teeforce.Api/Infrastructure/Repositories/CoursePricingSettingsRepository.cs`
- Migration generated by EF tooling

- [ ] **Step 1: Create repository implementation**

```csharp
// src/backend/Teeforce.Api/Infrastructure/Repositories/CoursePricingSettingsRepository.cs
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CoursePricingAggregate;

namespace Teeforce.Api.Infrastructure.Repositories;

public class CoursePricingSettingsRepository(ApplicationDbContext db) : ICoursePricingSettingsRepository
{
    public async Task<CoursePricingSettings?> GetByIdAsync(Guid id) =>
        await db.CoursePricingSettings
            .Include(s => s.RateSchedules)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<CoursePricingSettings?> GetByCourseIdAsync(Guid courseId, CancellationToken ct = default) =>
        await db.CoursePricingSettings
            .Include(s => s.RateSchedules)
            .FirstOrDefaultAsync(s => s.CourseId == courseId, ct);

    public void Add(CoursePricingSettings settings) => db.CoursePricingSettings.Add(settings);
}
```

- [ ] **Step 2: Register repository in DI**

Find where repositories are registered in `Program.cs` and add:

```csharp
builder.Services.AddScoped<ICoursePricingSettingsRepository, CoursePricingSettingsRepository>();
```

Add using: `using Teeforce.Domain.CoursePricingAggregate;`

- [ ] **Step 3: Generate EF migration**

Run: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations add AddCoursePricingSettings --project src/backend/Teeforce.Api`

- [ ] **Step 4: Review the generated migration**

Read the generated migration file. Verify it creates:
- `CoursePricingSettings` table with Id, CourseId, DefaultPrice, MinPrice, MaxPrice, CreatedAt, RowVersion, UpdatedAt, UpdatedBy + unique index on CourseId
- `RateSchedules` table with Id, CoursePricingSettingsId, Name, DaysOfWeek, StartTime, EndTime, Price
- Adds `Price` (decimal?) and `RateScheduleId` (uniqueidentifier?) columns to `TeeSheetIntervals`
- Adds `Price` (decimal?) column to `TeeTimeClaims`
- Adds `PricePerPlayer` (decimal?) and `TotalPrice` (decimal?) columns to `Bookings`

- [ ] **Step 5: Build and verify**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Infrastructure/Repositories/CoursePricingSettingsRepository.cs \
        src/backend/Teeforce.Api/Program.cs \
        src/backend/Teeforce.Api/Migrations/
git commit -m "feat(api): add CoursePricingSettings repository and EF migration

Repository with Include for RateSchedules. Migration adds pricing tables
and price columns to intervals, claims, and bookings. Part of #401."
```

---

## Task 8: API Endpoints + Validators

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Pricing/Endpoints/PricingEndpoints.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`
- Create: `tests/Teeforce.Api.Tests/Features/Pricing/Validators/PricingValidatorTests.cs`

- [ ] **Step 1: Create PricingEndpoints**

```csharp
// src/backend/Teeforce.Api/Features/Pricing/Endpoints/PricingEndpoints.cs
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.CoursePricingAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.Pricing.Endpoints;

public static class PricingEndpoints
{
    [WolverineGet("/courses/{courseId}/pricing")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetPricing(
        Guid courseId,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.Ok(new GetPricingResponse(null, null, null, []));
        }

        var schedules = settings.RateSchedules.Select(s => new RateScheduleResponse(
            s.Id, s.Name, s.DaysOfWeek, s.StartTime, s.EndTime, s.Price)).ToList();

        return Results.Ok(new GetPricingResponse(settings.DefaultPrice, settings.MinPrice, settings.MaxPrice, schedules));
    }

    [WolverinePut("/courses/{courseId}/pricing/default")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateDefaultPrice(
        Guid courseId,
        UpdateDefaultPriceRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        settings.UpdateDefaultPrice(request.DefaultPrice);
        return Results.Ok(new { defaultPrice = settings.DefaultPrice });
    }

    [WolverinePut("/courses/{courseId}/pricing/bounds")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateBounds(
        Guid courseId,
        UpdateBoundsRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        settings.UpdateBounds(request.MinPrice, request.MaxPrice);
        return Results.Ok(new { minPrice = settings.MinPrice, maxPrice = settings.MaxPrice });
    }

    [WolverinePost("/courses/{courseId}/pricing/schedules")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> CreateSchedule(
        Guid courseId,
        CreateScheduleRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        var schedule = settings.AddSchedule(request.Name, request.DaysOfWeek, request.StartTime, request.EndTime, request.Price);

        return Results.Created(
            $"/courses/{courseId}/pricing/schedules/{schedule.Id}",
            new RateScheduleResponse(schedule.Id, schedule.Name, schedule.DaysOfWeek, schedule.StartTime, schedule.EndTime, schedule.Price));
    }

    [WolverinePut("/courses/{courseId}/pricing/schedules/{scheduleId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateSchedule(
        Guid courseId,
        Guid scheduleId,
        UpdateScheduleRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.NotFound(new { error = "Pricing settings not found." });
        }

        settings.UpdateSchedule(scheduleId, request.Name, request.DaysOfWeek, request.StartTime, request.EndTime, request.Price);

        var updated = settings.RateSchedules.First(s => s.Id == scheduleId);
        return Results.Ok(new RateScheduleResponse(updated.Id, updated.Name, updated.DaysOfWeek, updated.StartTime, updated.EndTime, updated.Price));
    }

    [WolverineDelete("/courses/{courseId}/pricing/schedules/{scheduleId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> DeleteSchedule(
        Guid courseId,
        Guid scheduleId,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.NotFound(new { error = "Pricing settings not found." });
        }

        settings.RemoveSchedule(scheduleId);
        return Results.NoContent();
    }

    private static async Task<CoursePricingSettings> GetOrCreateSettings(
        Guid courseId,
        ICoursePricingSettingsRepository repository,
        CancellationToken ct)
    {
        var settings = await repository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            settings = CoursePricingSettings.Create(courseId);
            repository.Add(settings);
        }
        return settings;
    }
}

// --- DTOs ---

public record GetPricingResponse(
    decimal? DefaultPrice,
    decimal? MinPrice,
    decimal? MaxPrice,
    List<RateScheduleResponse> Schedules);

public record RateScheduleResponse(
    Guid Id,
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price);

public record UpdateDefaultPriceRequest(decimal? DefaultPrice);

public record UpdateBoundsRequest(decimal? MinPrice, decimal? MaxPrice);

public record CreateScheduleRequest(
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price);

public record UpdateScheduleRequest(
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price);

// --- Validators ---

public class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DaysOfWeek).NotEmpty().WithMessage("At least one day of week is required.");
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DaysOfWeek).NotEmpty().WithMessage("At least one day of week is required.");
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class UpdateBoundsRequestValidator : AbstractValidator<UpdateBoundsRequest>
{
    public UpdateBoundsRequestValidator()
    {
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Min price must be non-negative.")
            .When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice)
            .GreaterThan(0).WithMessage("Max price must be greater than zero.")
            .When(x => x.MaxPrice is not null);
        RuleFor(x => x)
            .Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .WithMessage("Min price must be less than or equal to max price.");
    }
}
```

- [ ] **Step 2: Map new domain exceptions in DomainExceptionHandler**

In `src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs`:

1. Add using: `using Teeforce.Domain.CoursePricingAggregate.Exceptions;`
2. In the switch expression, add before `EntityNotFoundException`:

```csharp
                    ConflictingScheduleException => StatusCodes.Status409Conflict,
                    PriceOutOfBoundsException => StatusCodes.Status422UnprocessableEntity,
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 4: Write validator tests**

```csharp
// tests/Teeforce.Api.Tests/Features/Pricing/Validators/PricingValidatorTests.cs
using Teeforce.Api.Features.Pricing.Endpoints;

namespace Teeforce.Api.Tests.Features.Pricing.Validators;

public class CreateScheduleRequestValidatorTests
{
    private readonly CreateScheduleRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Empty_Name_Fails()
    {
        var request = new CreateScheduleRequest("", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Empty_DaysOfWeek_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "DaysOfWeek");
    }

    [Fact]
    public void StartTime_After_EndTime_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(14, 0), new TimeOnly(8, 0), 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "StartTime");
    }

    [Fact]
    public void Zero_Price_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 0m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
    }

    [Fact]
    public void Negative_Price_Fails()
    {
        var request = new CreateScheduleRequest("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), -10m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
    }
}

public class UpdateBoundsRequestValidatorTests
{
    private readonly UpdateBoundsRequestValidator validator = new();

    [Fact]
    public void Valid_BothNull_Passes()
    {
        var request = new UpdateBoundsRequest(null, null);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Valid_BothSet_Passes()
    {
        var request = new UpdateBoundsRequest(10m, 100m);
        Assert.True(this.validator.Validate(request).IsValid);
    }

    [Fact]
    public void Negative_MinPrice_Fails()
    {
        var request = new UpdateBoundsRequest(-5m, 100m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MinPrice");
    }

    [Fact]
    public void Min_Greater_Than_Max_Fails()
    {
        var request = new UpdateBoundsRequest(100m, 50m);
        var result = this.validator.Validate(request);
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 5: Run validator tests**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~PricingValidatorTests"`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add src/backend/Teeforce.Api/Features/Pricing/ \
        src/backend/Teeforce.Api/Infrastructure/Middleware/DomainExceptionHandler.cs \
        tests/Teeforce.Api.Tests/Features/Pricing/
git commit -m "feat(api): add pricing CRUD endpoints with validators

GET/PUT/POST/DELETE endpoints for rate schedules and default price.
Validators for schedule and bounds requests. Part of #401."
```

---

## Task 9: PricingSettingsChanged Handler

**Files:**
- Create: `src/backend/Teeforce.Api/Features/Pricing/Handlers/PricingSettingsChanged/RepriceFutureSheetsHandler.cs`
- Create: `tests/Teeforce.Api.Tests/Features/Pricing/Handlers/RepriceFutureSheetsHandlerTests.cs`
- Modify: `src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs` — add `GetFutureByCourseAsync`
- Modify: `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs` — implement new method

- [ ] **Step 1: Add GetFutureByCourseAsync to ITeeSheetRepository**

In `src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs`, add:

```csharp
    Task<List<TeeSheet>> GetFutureByCourseAsync(Guid courseId, DateOnly fromDate, CancellationToken ct = default);
```

- [ ] **Step 2: Implement in TeeSheetRepository**

In `src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs`, add:

```csharp
    public async Task<List<TeeSheet>> GetFutureByCourseAsync(Guid courseId, DateOnly fromDate, CancellationToken ct = default) =>
        await db.TeeSheets
            .Include(s => s.Intervals)
            .Where(s => s.CourseId == courseId && s.Date >= fromDate)
            .ToListAsync(ct);
```

- [ ] **Step 3: Create RepriceFutureSheetsHandler**

```csharp
// src/backend/Teeforce.Api/Features/Pricing/Handlers/PricingSettingsChanged/RepriceFutureSheetsHandler.cs
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Api.Features.Pricing.Handlers;

public static class PricingSettingsChangedRepriceFutureSheetsHandler
{
    public static async Task Handle(
        PricingSettingsChanged evt,
        ICoursePricingSettingsRepository pricingRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        ILogger logger,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(evt.CourseId, ct);
        if (settings is null)
        {
            logger.LogWarning("CoursePricingSettings not found for course {CourseId}, skipping reprice", evt.CourseId);
            return;
        }

        var today = timeProvider.GetCurrentDate();
        var sheets = await teeSheetRepository.GetFutureByCourseAsync(evt.CourseId, today, ct);

        foreach (var sheet in sheets)
        {
            sheet.ApplyPricing(settings.ResolvePriceWithSource);
        }

        logger.LogInformation("Repriced {SheetCount} future tee sheets for course {CourseId}", sheets.Count, evt.CourseId);
    }
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 5: Write handler tests**

```csharp
// tests/Teeforce.Api.Tests/Features/Pricing/Handlers/RepriceFutureSheetsHandlerTests.cs
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.Pricing.Handlers;
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.CoursePricingAggregate.Events;
using Teeforce.Domain.TeeSheetAggregate;
using ITimeProvider = Teeforce.Domain.Common.ITimeProvider;

namespace Teeforce.Api.Tests.Features.Pricing.Handlers;

public class RepriceFutureSheetsHandlerTests
{
    private readonly ICoursePricingSettingsRepository pricingRepo = Substitute.For<ICoursePricingSettingsRepository>();
    private readonly ITeeSheetRepository teeSheetRepo = Substitute.For<ITeeSheetRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    public RepriceFutureSheetsHandlerTests()
    {
        this.timeProvider.GetCurrentDate().Returns(new DateOnly(2026, 6, 1));
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_RepricesFutureSheets()
    {
        var courseId = Guid.NewGuid();
        var settings = CoursePricingSettings.Create(courseId, defaultPrice: 50m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(7, 0), new TimeOnly(12, 0), 75m);
        settings.ClearDomainEvents();

        var scheduleSettings = new ScheduleSettings(new TimeOnly(7, 0), new TimeOnly(8, 0), 10, 4);
        var sheet = TeeSheet.Draft(courseId, new DateOnly(2026, 6, 1), scheduleSettings, this.timeProvider); // Monday

        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns(settings);
        this.teeSheetRepo.GetFutureByCourseAsync(courseId, new DateOnly(2026, 6, 1), Arg.Any<CancellationToken>()).Returns([sheet]);

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await PricingSettingsChangedRepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);

        // All intervals are at 7:00, 7:10, 7:20, 7:30, 7:40, 7:50 — all within the morning schedule
        Assert.All(sheet.Intervals, i => Assert.Equal(75m, i.Price));
    }

    [Fact]
    public async Task Handle_NoPricingSettings_LogsAndReturns()
    {
        var courseId = Guid.NewGuid();
        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns((CoursePricingSettings?)null);

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await PricingSettingsChangedRepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);

        await this.teeSheetRepo.DidNotReceive().GetFutureByCourseAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoFutureSheets_CompletesWithoutError()
    {
        var courseId = Guid.NewGuid();
        var settings = CoursePricingSettings.Create(courseId, defaultPrice: 50m);
        settings.ClearDomainEvents();

        this.pricingRepo.GetByCourseIdAsync(courseId, Arg.Any<CancellationToken>()).Returns(settings);
        this.teeSheetRepo.GetFutureByCourseAsync(courseId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new List<TeeSheet>());

        var evt = new PricingSettingsChanged { CourseId = courseId };

        await PricingSettingsChangedRepriceFutureSheetsHandler.Handle(
            evt, this.pricingRepo, this.teeSheetRepo, this.timeProvider, this.logger, CancellationToken.None);

        // No exception — handler completes cleanly
    }
}
```

- [ ] **Step 6: Run handler tests**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~RepriceFutureSheetsHandlerTests"`
Expected: All PASS

- [ ] **Step 7: Commit**

```bash
git add src/backend/Teeforce.Domain/TeeSheetAggregate/ITeeSheetRepository.cs \
        src/backend/Teeforce.Api/Infrastructure/Repositories/TeeSheetRepository.cs \
        src/backend/Teeforce.Api/Features/Pricing/Handlers/ \
        tests/Teeforce.Api.Tests/Features/Pricing/Handlers/
git commit -m "feat(api): add PricingSettingsChanged handler to reprice future sheets

Handler loads all future tee sheets for the course and applies updated
pricing via CoursePricingSettings.ResolvePriceWithSource. Part of #401."
```

---

## Task 10: BulkDraftEndpoint Pricing Integration

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs`

- [ ] **Step 1: Update BulkDraftEndpoint to stamp prices at draft time**

In `src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs`:

1. Add using: `using Teeforce.Domain.CoursePricingAggregate;`
2. Add `ICoursePricingSettingsRepository pricingRepository` parameter to the `Handle` method
3. After `var settings = course.CurrentScheduleDefaults();`, load pricing:

```csharp
        var pricingSettings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
```

4. After `teeSheetRepository.Add(sheet);`, apply pricing if available:

```csharp
            if (pricingSettings is not null)
            {
                sheet.ApplyPricing(pricingSettings.ResolvePriceWithSource);
            }
```

The full updated `Handle` method:

```csharp
    public static async Task<IResult> Handle(
        Guid courseId,
        BulkDraftRequest request,
        ICourseRepository courseRepository,
        ITeeSheetRepository teeSheetRepository,
        ICoursePricingSettingsRepository pricingRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(courseId);
        var settings = course.CurrentScheduleDefaults();
        var pricingSettings = await pricingRepository.GetByCourseIdAsync(courseId, ct);

        var existingSheets = await teeSheetRepository.GetByCourseAndDatesAsync(courseId, request.Dates, ct);
        if (existingSheets.Count > 0)
        {
            throw new TeeSheetAlreadyExistsException(courseId, existingSheets[0].Date);
        }

        var results = new List<BulkDraftItem>();

        foreach (var date in request.Dates)
        {
            var sheet = TeeSheetAggregate.Draft(courseId, date, settings, timeProvider);
            if (pricingSettings is not null)
            {
                sheet.ApplyPricing(pricingSettings.ResolvePriceWithSource);
            }
            teeSheetRepository.Add(sheet);

            results.Add(new BulkDraftItem(date, sheet.Id));
        }

        return Results.Ok(new BulkDraftResponse(results));
    }
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Teeforce.Api/Features/TeeSheet/Endpoints/BulkDraftEndpoint.cs
git commit -m "feat(api): stamp prices on tee sheet intervals at draft time

BulkDraftEndpoint loads CoursePricingSettings and applies resolved prices
to each drafted tee sheet. Part of #401."
```

---

## Task 11: Data Migration — FlatRatePrice to DefaultPrice

**Files:**
- Modify: `src/backend/Teeforce.Domain/CourseAggregate/Course.cs`
- Modify: `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs`
- Modify: `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs`
- Migration generated by EF tooling

- [ ] **Step 1: Remove FlatRatePrice from Course**

In `src/backend/Teeforce.Domain/CourseAggregate/Course.cs`:
- Remove line 22: `public decimal? FlatRatePrice { get; private set; }`
- Remove line 70: `public void UpdatePricing(decimal flatRatePrice) => FlatRatePrice = flatRatePrice;`

- [ ] **Step 2: Remove FlatRatePrice from CourseConfiguration**

In `src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs`:
- Remove line 24: `builder.Property(c => c.FlatRatePrice).HasPrecision(18, 2);`

- [ ] **Step 3: Remove old pricing endpoints from CourseEndpoints**

In `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs`:
- Remove the `UpdatePricing` method (lines 212-229)
- Remove the `GetPricing` method (lines 231-248)
- Remove `PricingRequest` record (line 291)
- Remove `PricingResponse` record (line 293)
- Remove `PricingRequestValidator` class (lines 331-339)

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded. Fix any remaining references to `FlatRatePrice`, `PricingRequest`, or `PricingResponse` if the build fails.

- [ ] **Step 5: Generate data migration**

The migration should:
1. Insert a `CoursePricingSettings` row for each existing Course
2. Copy `Course.FlatRatePrice` to `CoursePricingSettings.DefaultPrice`
3. Drop `FlatRatePrice` column from `Courses`

Run: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations add MigrateFlatRatePriceToCoursePricingSettings --project src/backend/Teeforce.Api`

- [ ] **Step 6: Edit the generated migration**

The auto-generated migration will just drop the column. You need to add data migration logic in the `Up` method **before** the column drop. Add this SQL at the start of `Up`:

```csharp
// Migrate existing FlatRatePrice data to CoursePricingSettings
migrationBuilder.Sql(@"
    INSERT INTO CoursePricingSettings (Id, CourseId, DefaultPrice, MinPrice, MaxPrice, CreatedAt, UpdatedAt, UpdatedBy, RowVersion)
    SELECT
        NEWID(),
        Id,
        FlatRatePrice,
        NULL,
        NULL,
        GETUTCDATE(),
        GETUTCDATE(),
        NULL,
        0x00000000
    FROM Courses
    WHERE Id NOT IN (SELECT CourseId FROM CoursePricingSettings)
");
```

Note: `NEWID()` is used because `Guid.CreateVersion7()` is C# only. For the data migration SQL, standard GUIDs are fine.

- [ ] **Step 7: Review and verify**

Run: `dotnet build teeforce.slnx`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/backend/Teeforce.Domain/CourseAggregate/Course.cs \
        src/backend/Teeforce.Api/Infrastructure/EntityTypeConfigurations/CourseConfiguration.cs \
        src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs \
        src/backend/Teeforce.Api/Migrations/
git commit -m "feat: migrate FlatRatePrice to CoursePricingSettings.DefaultPrice

Data migration creates CoursePricingSettings rows for existing courses and
copies FlatRatePrice. Drops Course.FlatRatePrice column and removes old
pricing endpoints. Part of #401."
```

---

## Task 12: Fix Existing Tests + Format + Full Test Run

**Files:**
- Various test files that reference `FlatRatePrice`, `PricingRequest`, `PricingResponse`, or `TeeTimeClaim` constructor

- [ ] **Step 1: Find and fix all broken references**

Run: `dotnet build teeforce.slnx` and fix any compilation errors. Common issues:
- Integration tests referencing old `PUT /courses/{courseId}/pricing` endpoint
- Integration tests referencing `PricingResponse.FlatRatePrice`
- Test files constructing `TeeTimeClaim` (constructor signature changed)
- Any code referencing `Course.FlatRatePrice` or `Course.UpdatePricing`

- [ ] **Step 2: Run dotnet format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 3: Run full test suite**

Run: `dotnet test teeforce.slnx`
Expected: All PASS

- [ ] **Step 4: Run make dev to verify startup**

Run: `make dev` and verify the API starts without migration errors.

- [ ] **Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix: update existing tests and format for rate schedules changes

Fixes compilation errors from FlatRatePrice removal and TeeTimeClaim
constructor changes. Part of #401."
```

---

## Task 13: Verify Pending Model Changes + Final Review

- [ ] **Step 1: Check for pending model changes**

Run: `export PATH="$PATH:/home/aaron/.dotnet/tools" && dotnet ef migrations has-pending-model-changes --project src/backend/Teeforce.Api`
Expected: No pending changes

- [ ] **Step 2: Run full test suite one more time**

Run: `dotnet test teeforce.slnx`
Expected: All PASS

- [ ] **Step 3: Review git log**

Run: `git log --oneline main..HEAD`
Verify all commits are present and well-structured.
