using Teeforce.Api.Features.Bookings.Handlers;
using Teeforce.Api.Features.Waitlist.Handlers;

namespace Teeforce.Api.Tests.Features.Notifications;

public class SmsFormatterTests
{
    [Fact]
    public void BookingConfirmation_FormatsCorrectly()
    {
        var formatter = new BookingConfirmationSmsFormatter();
        var notification = new BookingConfirmation("Teeforce Golf Club", new DateOnly(2026, 7, 4), new TimeOnly(8, 0));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("8:00 AM", result);
        Assert.Contains("July 4, 2026", result);
        Assert.Contains("booked", result);
    }

    [Fact]
    public void BookingCancellation_FormatsCorrectly()
    {
        var formatter = new BookingCancellationSmsFormatter();
        var notification = new BookingCancellation("Teeforce Golf Club", new DateOnly(2026, 7, 4), new TimeOnly(8, 0));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("cancelled", result);
        Assert.Contains("July 4, 2026", result);
        Assert.Contains("8:00 AM", result);
    }

    [Fact]
    public void WaitlistJoined_FormatsCorrectly()
    {
        var formatter = new WaitlistJoinedSmsFormatter();
        var notification = new WaitlistJoined("Teeforce Golf Club");

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("waitlist", result);
    }

    [Fact]
    public void WaitlistOfferAvailable_FormatsWithClaimUrl()
    {
        var formatter = new WaitlistOfferAvailableSmsFormatter();
        var notification = new WaitlistOfferAvailable("Teeforce Golf Club", new TimeOnly(9, 30), "https://app.teeforce.com/book/walkup/abc123");

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("9:30 AM", result);
        Assert.Contains("https://app.teeforce.com/book/walkup/abc123", result);
    }

    [Fact]
    public void WaitlistOfferExpired_FormatsStaticMessage()
    {
        var formatter = new WaitlistOfferExpiredSmsFormatter();
        var notification = new WaitlistOfferExpired();

        var result = formatter.Format(notification);

        Assert.Contains("no longer available", result);
    }

    [Fact]
    public void WalkupConfirmation_FormatsCorrectly()
    {
        var formatter = new WalkupConfirmationSmsFormatter();
        var notification = new WalkupConfirmation("Teeforce Golf Club", new DateOnly(2026, 6, 15), new TimeOnly(9, 30));

        var result = formatter.Format(notification);

        Assert.Contains("Teeforce Golf Club", result);
        Assert.Contains("confirmed", result);
        Assert.Contains("June 15", result);
        Assert.Contains("9:30 AM", result);
    }
}
