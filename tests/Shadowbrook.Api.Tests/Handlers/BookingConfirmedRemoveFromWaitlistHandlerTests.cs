using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.BookingAggregate.Events;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class BookingConfirmedRemoveFromWaitlistHandlerTests
{
    private readonly IBookingRepository bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();

    public BookingConfirmedRemoveFromWaitlistHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(new TimeOnly(10, 0));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
    }

    private async Task<WalkUpGolferWaitlistEntry> CreateEntryAsync(Golfer golfer, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 26), this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        return await waitlist.Join(golfer, this.entryRepo, this.timeProvider, "UTC", groupSize);
    }

    [Fact]
    public async Task Handle_EntryExists_RemovesEntry()
    {
        var courseId = Guid.NewGuid();
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var date = new DateOnly(2026, 3, 26);
        var teeTime = new TimeOnly(14, 0);
        var bookingId = Guid.NewGuid();

        var booking = Booking.Create(
            bookingId,
            courseId,
            golfer.Id,
            date,
            teeTime,
            "Jane Smith",
            1);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), date, this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        var entry = await CreateEntryAsync(golfer);
        entry.ClearDomainEvents();

        this.bookingRepo.GetByIdAsync(bookingId).Returns(booking);
        this.waitlistRepo.GetByCourseDateAsync(courseId, date).Returns(waitlist);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id).Returns(entry);

        var evt = new BookingConfirmed { BookingId = bookingId, GolferId = golfer.Id };

        await BookingConfirmedRemoveFromWaitlistHandler.Handle(
            evt,
            this.bookingRepo,
            this.waitlistRepo,
            this.entryRepo,
            this.timeProvider,
            NullLogger.Instance);

        Assert.Contains(entry.DomainEvents, e => e is GolferRemovedFromWaitlist);
    }

    [Fact]
    public async Task Handle_NoWaitlistFound_LogsWarningAndReturns()
    {
        var courseId = Guid.NewGuid();
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var date = new DateOnly(2026, 3, 26);
        var bookingId = Guid.NewGuid();

        var booking = Booking.Create(
            bookingId,
            courseId,
            golfer.Id,
            date,
            new TimeOnly(14, 0),
            "Jane Smith",
            1);

        this.bookingRepo.GetByIdAsync(bookingId).Returns(booking);
        this.waitlistRepo.GetByCourseDateAsync(courseId, date).Returns((WalkUpWaitlist?)null);

        var evt = new BookingConfirmed { BookingId = bookingId, GolferId = golfer.Id };

        await BookingConfirmedRemoveFromWaitlistHandler.Handle(
            evt,
            this.bookingRepo,
            this.waitlistRepo,
            this.entryRepo,
            this.timeProvider,
            NullLogger.Instance);

        await this.entryRepo.DidNotReceive().GetActiveByWaitlistAndGolferAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>());
    }

    [Fact]
    public async Task Handle_NoEntryFound_LogsWarningAndReturns()
    {
        var courseId = Guid.NewGuid();
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        var date = new DateOnly(2026, 3, 26);
        var bookingId = Guid.NewGuid();

        var booking = Booking.Create(
            bookingId,
            courseId,
            golfer.Id,
            date,
            new TimeOnly(14, 0),
            "Jane Smith",
            1);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), date, this.shortCodeGen, this.waitlistRepo, this.timeProvider);

        this.bookingRepo.GetByIdAsync(bookingId).Returns(booking);
        this.waitlistRepo.GetByCourseDateAsync(courseId, date).Returns(waitlist);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id)
            .Returns((GolferWaitlistEntry?)null);

        var evt = new BookingConfirmed { BookingId = bookingId, GolferId = golfer.Id };

        await BookingConfirmedRemoveFromWaitlistHandler.Handle(
            evt,
            this.bookingRepo,
            this.waitlistRepo,
            this.entryRepo,
            this.timeProvider,
            NullLogger.Instance);

        // Just verify no exception thrown - entry not found is logged and handled
        Assert.True(true);
    }
}
