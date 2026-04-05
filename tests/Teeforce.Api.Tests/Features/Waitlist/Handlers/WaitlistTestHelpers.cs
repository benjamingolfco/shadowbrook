using NSubstitute;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;

namespace Teeforce.Api.Tests.Features.Waitlist.Handlers;

internal static class WaitlistTestHelpers
{
    /// <summary>
    /// Creates a WaitlistOffer via the public domain API: WalkUpWaitlist.Join → entry.CreateOffer.
    /// </summary>
    internal static async Task<WaitlistOffer> CreateOfferAsync(
        ITimeProvider timeProvider,
        TeeTimeOpening opening,
        int groupSize = 1)
    {
        var shortCodeGenerator = Substitute.For<IShortCodeGenerator>();
        shortCodeGenerator.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");

        var waitlistRepository = Substitute.For<ICourseWaitlistRepository>();

        var entryRepository = Substitute.For<IGolferWaitlistEntryRepository>();
        entryRepository.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);

        var joinProvider = Substitute.For<ITimeProvider>();
        joinProvider.GetCurrentTimestamp().Returns(timeProvider.GetCurrentTimestamp());
        joinProvider.GetCurrentTimeByTimeZone(Arg.Any<string>()).Returns(opening.TeeTime.Time);
        joinProvider.GetCurrentDateByTimeZone(Arg.Any<string>()).Returns(opening.TeeTime.Date);

        var waitlist = await WalkUpWaitlist.OpenAsync(
            opening.CourseId, opening.TeeTime.Date,
            shortCodeGenerator, waitlistRepository, joinProvider);

        var golfer = Golfer.Create("+15551234567", "Test", "Golfer");
        var entry = await waitlist.Join(golfer, entryRepository, joinProvider, "UTC", groupSize);
        return entry.CreateOffer(opening, timeProvider);
    }

    internal static TeeTimeOpening CreateOpening(
        ITimeProvider timeProvider,
        Guid? courseId = null,
        DateOnly? date = null,
        TimeOnly? teeTime = null,
        int slotsAvailable = 4) =>
        TeeTimeOpening.Create(
            courseId ?? Guid.NewGuid(),
            date ?? new DateOnly(2026, 3, 25),
            teeTime ?? new TimeOnly(10, 0),
            slotsAvailable,
            operatorOwned: false,
            timeProvider);
}
