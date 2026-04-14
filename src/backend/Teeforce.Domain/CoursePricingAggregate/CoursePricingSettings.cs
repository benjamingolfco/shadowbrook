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

        if (DefaultPrice is not null)
        {
            ValidatePriceBounds(DefaultPrice.Value);
        }

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

    private void RaisePricingChanged() =>
        AddDomainEvent(new PricingSettingsChanged { CourseId = CourseId });
}
