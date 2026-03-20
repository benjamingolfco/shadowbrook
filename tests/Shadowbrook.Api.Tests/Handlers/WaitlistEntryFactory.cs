using NSubstitute;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Tests.Handlers;

/// <summary>
/// Creates <see cref="GolferWaitlistEntry"/> instances through the domain factory method
/// <see cref="WalkUpWaitlist.Join"/>, since the constructor is internal to the Domain assembly.
/// </summary>
internal static class WaitlistEntryFactory
{
    /// <summary>
    /// Creates a <see cref="GolferWaitlistEntry"/> for the given golfer via
    /// <see cref="WalkUpWaitlist.Join"/>. The entry's <see cref="GolferWaitlistEntry.GolferId"/>
    /// will be <paramref name="golfer"/>.Id.
    /// </summary>
    public static async Task<GolferWaitlistEntry> CreateAsync(Golfer golfer, int groupSize = 1)
    {
        var waitlistRepo = Substitute.For<IWalkUpWaitlistRepository>();
        waitlistRepo.GetByCourseDateAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>()).Returns((WalkUpWaitlist?)null);

        var shortCode = Substitute.For<IShortCodeGenerator>();
        shortCode.GenerateAsync(Arg.Any<DateOnly>()).Returns("ABC123");

        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), shortCode, waitlistRepo);

        var entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
        entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);

        return await waitlist.Join(golfer, entryRepo, groupSize);
    }

    /// <summary>
    /// Creates a <see cref="GolferWaitlistEntry"/> with an arbitrary golfer ID.
    /// Use when the test only needs the entry's ID, not the golfer's identity.
    /// </summary>
    public static Task<GolferWaitlistEntry> CreateAsync(int groupSize = 1)
    {
        var golfer = Golfer.Create("+15559990000", "Test", "Golfer");
        return CreateAsync(golfer, groupSize);
    }
}
