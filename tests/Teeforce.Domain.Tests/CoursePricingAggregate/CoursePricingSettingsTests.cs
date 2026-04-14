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
    public void UpdateBounds_ExistingSchedulePriceOutOfBounds_MarksInvalid()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 30m);
        settings.ClearDomainEvents();

        settings.UpdateBounds(minPrice: 40m, maxPrice: 200m);

        var schedule = settings.RateSchedules.Single();
        Assert.NotNull(schedule.InvalidReason);
    }

    [Fact]
    public void AddSchedule_WhenBoundsNotSet_ThrowsDomainException()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m));
    }

    [Fact]
    public void AddSchedule_CreatesScheduleAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
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
        settings.UpdateBounds(1m, 1000m);

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Bad", [], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m));
    }

    [Fact]
    public void AddSchedule_StartAfterEnd_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);

        Assert.Throws<DomainException>(() =>
            settings.AddSchedule("Bad", [DayOfWeek.Monday], new TimeOnly(14, 0), new TimeOnly(8, 0), 50m));
    }

    [Fact]
    public void AddSchedule_ZeroPrice_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);

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
        settings.UpdateBounds(1m, 1000m);
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

    [Fact]
    public void AddSchedule_ConflictingSameSpecificity_Throws()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        Assert.Throws<ConflictingScheduleException>(() =>
            settings.AddSchedule("Monday Overlap", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(14, 0), 60m));
    }

    [Fact]
    public void AddSchedule_OverlappingDifferentSpecificity_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Weekend Morning", [DayOfWeek.Saturday, DayOfWeek.Sunday], new TimeOnly(6, 0), new TimeOnly(12, 0), 80m);

        settings.AddSchedule("Saturday Morning", [DayOfWeek.Saturday], new TimeOnly(6, 0), new TimeOnly(12, 0), 90m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_OverlappingDifferentTimeBandWidth_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        settings.AddSchedule("Monday Peak", [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(11, 0), 70m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_DifferentDaysNoConflict()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        settings.AddSchedule("Tuesday Morning", [DayOfWeek.Tuesday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void AddSchedule_NonOverlappingTimeSameDay_Allowed()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);

        settings.AddSchedule("Monday Afternoon", [DayOfWeek.Monday], new TimeOnly(12, 0), new TimeOnly(18, 0), 60m);

        Assert.Equal(2, settings.RateSchedules.Count);
    }

    [Fact]
    public void UpdateSchedule_UpdatesAndRaisesEvent()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
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
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        var afternoon = settings.AddSchedule("Afternoon", [DayOfWeek.Monday], new TimeOnly(12, 0), new TimeOnly(18, 0), 60m);

        Assert.Throws<ConflictingScheduleException>(() =>
            settings.UpdateSchedule(afternoon.Id, "Overlap", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(14, 0), 65m));
    }

    [Fact]
    public void UpdateSchedule_SameValuesNoConflictWithSelf()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 50m);
        settings.ClearDomainEvents();

        settings.UpdateSchedule(schedule.Id, "Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 55m);

        Assert.Equal(55m, settings.RateSchedules.Single().Price);
    }

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
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(75m, price);
    }

    [Fact]
    public void ResolvePrice_NoMatchingSchedule_FallsBackToDefault()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        var price = settings.ResolvePrice(DayOfWeek.Tuesday, new TimeOnly(9, 0));

        Assert.Equal(50m, price);
    }

    [Fact]
    public void ResolvePrice_FewerDaysWins()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Weekend", [DayOfWeek.Saturday, DayOfWeek.Sunday], new TimeOnly(6, 0), new TimeOnly(12, 0), 80m);
        settings.AddSchedule("Saturday Only", [DayOfWeek.Saturday], new TimeOnly(6, 0), new TimeOnly(12, 0), 90m);

        var price = settings.ResolvePrice(DayOfWeek.Saturday, new TimeOnly(9, 0));

        Assert.Equal(90m, price);
    }

    [Fact]
    public void ResolvePrice_NarrowerTimeBandWins()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Full", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(18, 0), 60m);
        settings.AddSchedule("Monday Peak", [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(11, 0), 85m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(10, 0));

        Assert.Equal(85m, price);
    }

    [Fact]
    public void ResolvePrice_TimeAtStartBoundary_Matches()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        Assert.Equal(75m, settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(6, 0)));
    }

    [Fact]
    public void ResolvePrice_TimeAtEndBoundary_DoesNotMatch()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        Assert.Equal(50m, settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void ResolvePriceWithSource_ReturnsScheduleId()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
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

    [Fact]
    public void UpdateBounds_MarksSchedulesOutsideBoundsAsInvalid()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 150m);

        settings.UpdateBounds(minPrice: 1m, maxPrice: 100m);

        Assert.NotNull(schedule.InvalidReason);
        Assert.Contains("150.00", schedule.InvalidReason);
        Assert.Contains("100.00", schedule.InvalidReason);
    }

    [Fact]
    public void UpdateBounds_ClearsInvalidReasonWhenPriceBackInBounds()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 150m);
        settings.UpdateBounds(minPrice: 1m, maxPrice: 100m);
        Assert.NotNull(schedule.InvalidReason);

        settings.UpdateBounds(minPrice: 1m, maxPrice: 200m);

        Assert.Null(schedule.InvalidReason);
    }

    [Fact]
    public void UpdateBounds_MarksSchedulesBelowMinAsInvalid()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 20m);

        settings.UpdateBounds(minPrice: 50m, maxPrice: 200m);

        Assert.NotNull(schedule.InvalidReason);
        Assert.Contains("20.00", schedule.InvalidReason);
        Assert.Contains("50.00", schedule.InvalidReason);
    }

    [Fact]
    public void ResolvePrice_SkipsInvalidSchedules()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid(), defaultPrice: 50m);
        settings.UpdateBounds(1m, 1000m);
        settings.AddSchedule("Monday Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 150m);

        // Tighten bounds so the schedule becomes invalid
        settings.UpdateBounds(minPrice: 1m, maxPrice: 100m);

        var price = settings.ResolvePrice(DayOfWeek.Monday, new TimeOnly(9, 0));

        Assert.Equal(50m, price);
    }

    [Fact]
    public void UpdateSchedule_ClearsInvalidReason()
    {
        var settings = CoursePricingSettings.Create(Guid.NewGuid());
        settings.UpdateBounds(1m, 1000m);
        var schedule = settings.AddSchedule("Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 150m);
        settings.UpdateBounds(minPrice: 1m, maxPrice: 100m);
        Assert.NotNull(schedule.InvalidReason);

        settings.UpdateBounds(1m, 1000m);
        settings.UpdateSchedule(schedule.Id, "Morning", [DayOfWeek.Monday], new TimeOnly(6, 0), new TimeOnly(12, 0), 75m);

        Assert.Null(schedule.InvalidReason);
    }
}
