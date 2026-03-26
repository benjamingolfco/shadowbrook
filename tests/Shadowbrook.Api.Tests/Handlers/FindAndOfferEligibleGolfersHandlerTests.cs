using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistServices;

namespace Shadowbrook.Api.Tests.Handlers;

public class FindAndOfferEligibleGolfersHandlerTests
{
    private readonly ITeeTimeOpeningRepository openingRepo = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly IGolferWaitlistEntryRepository entryRepo = Substitute.For<IGolferWaitlistEntryRepository>();
    private readonly IWaitlistOfferRepository offerRepo = Substitute.For<IWaitlistOfferRepository>();
    private readonly IGolferRepository golferRepo = Substitute.For<IGolferRepository>();
    private readonly ICourseRepository courseRepo = Substitute.For<ICourseRepository>();
    private readonly ITextMessageService sms = Substitute.For<ITextMessageService>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();
    private readonly IShortCodeGenerator shortCodeGen = Substitute.For<IShortCodeGenerator>();
    private readonly ICourseWaitlistRepository waitlistRepo = Substitute.For<ICourseWaitlistRepository>();
    private readonly WaitlistMatchingService matchingService;
    private readonly IConfiguration config;

    public FindAndOfferEligibleGolfersHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
        this.timeProvider.GetCurrentTime().Returns(new TimeOnly(14, 15));
        this.shortCodeGen.GenerateAsync(Arg.Any<DateOnly>()).Returns("1234");
        this.matchingService = new WaitlistMatchingService(this.entryRepo);
        this.config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "App:FrontendUrl", "https://test.example.com" } })
            .Build();
    }

    private async Task<WalkUpGolferWaitlistEntry> CreateEntryAsync(Golfer golfer, int groupSize = 1)
    {
        var waitlist = await WalkUpWaitlist.OpenAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), this.shortCodeGen, this.waitlistRepo, this.timeProvider);
        this.entryRepo.GetActiveByWaitlistAndGolferAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns((GolferWaitlistEntry?)null);
        return await waitlist.Join(golfer, this.entryRepo, this.timeProvider, groupSize);
    }

    [Fact]
    public async Task Handle_NoOpening_Throws()
    {
        var openingId = Guid.NewGuid();
        this.openingRepo.GetByIdAsync(openingId).Returns((TeeTimeOpening?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FindAndOfferEligibleGolfersHandler.Handle(
                new FindAndOfferEligibleGolfers(openingId, 3),
                this.openingRepo, this.matchingService, this.offerRepo, this.golferRepo,
                this.courseRepo, this.sms, this.timeProvider, this.config, NullLogger.Instance, CancellationToken.None));

        Assert.Contains(openingId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_NoEligibleEntries_DoesNothing()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);
        this.entryRepo.FindEligibleEntriesAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GolferWaitlistEntry>());

        await FindAndOfferEligibleGolfersHandler.Handle(
            new FindAndOfferEligibleGolfers(opening.Id, 3),
            this.openingRepo, this.matchingService, this.offerRepo, this.golferRepo,
            this.courseRepo, this.sms, this.timeProvider, this.config, NullLogger.Instance, CancellationToken.None);

        this.offerRepo.DidNotReceive().Add(Arg.Any<WaitlistOffer>());
    }

    [Fact]
    public async Task Handle_EligibleEntries_CreatesOffersAndSendsSms()
    {
        var courseId = Guid.NewGuid();
        var opening = TeeTimeOpening.Create(
            courseId, new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        this.openingRepo.GetByIdAsync(opening.Id).Returns(opening);

        var course = Course.Create(Guid.NewGuid(), "Test Course", "America/Chicago");
        this.courseRepo.GetByIdAsync(courseId).Returns(course);

        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        this.golferRepo.GetByIdAsync(golfer.Id).Returns(golfer);

        var entry = await CreateEntryAsync(golfer);
        this.entryRepo.FindEligibleEntriesAsync(
            Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<GolferWaitlistEntry> { entry });

        await FindAndOfferEligibleGolfersHandler.Handle(
            new FindAndOfferEligibleGolfers(opening.Id, 3),
            this.openingRepo, this.matchingService, this.offerRepo, this.golferRepo,
            this.courseRepo, this.sms, this.timeProvider, this.config, NullLogger.Instance, CancellationToken.None);

        this.offerRepo.Received(1).Add(Arg.Any<WaitlistOffer>());
        await this.sms.Received(1).SendAsync(
            "+15551234567",
            Arg.Is<string>(m => m.Contains("2:30 PM") && m.Contains("Test Course")),
            Arg.Any<CancellationToken>());
    }
}
