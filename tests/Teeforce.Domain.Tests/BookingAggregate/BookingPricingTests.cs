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
