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
    public void Claim_InstanceMethod_StampsPriceFromParameter()
    {
        var (sheet, interval) = CreateSheetAndInterval(price: 60m);
        var auth = sheet.AuthorizeBooking();

        var teeTime = TeeTime.Claim(
            interval, sheet.CourseId, sheet.Date, auth,
            Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, this.timeProvider);

        teeTime.ClearDomainEvents();
        var auth2 = sheet.AuthorizeBooking();
        teeTime.Claim(auth2, Guid.NewGuid(), Guid.NewGuid(), groupSize: 1, interval.Price, this.timeProvider);

        var secondClaim = teeTime.Claims[1];
        Assert.Equal(60m, secondClaim.Price);
    }
}
