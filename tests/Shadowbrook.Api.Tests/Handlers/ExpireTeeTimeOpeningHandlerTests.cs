using NSubstitute;
using Shadowbrook.Api.Features.Waitlist.Handlers;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class ExpireTeeTimeOpeningHandlerTests
{
    private readonly ITeeTimeOpeningRepository repository = Substitute.For<ITeeTimeOpeningRepository>();
    private readonly ITimeProvider timeProvider = Substitute.For<ITimeProvider>();

    public ExpireTeeTimeOpeningHandlerTests()
    {
        this.timeProvider.GetCurrentTimestamp().Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_OpeningExists_ExpiresIt()
    {
        var opening = TeeTimeOpening.Create(
            Guid.NewGuid(), new DateOnly(2026, 3, 25), new TimeOnly(14, 30), 3, true, this.timeProvider);
        opening.ClearDomainEvents();
        this.repository.GetByIdAsync(opening.Id).Returns(opening);

        await ExpireTeeTimeOpeningHandler.Handle(new ExpireTeeTimeOpening(opening.Id), this.repository);

        Assert.Equal(TeeTimeOpeningStatus.Expired, opening.Status);
        Assert.Contains(opening.DomainEvents, e => e is TeeTimeOpeningExpired);
    }

    [Fact]
    public async Task Handle_OpeningNotFound_DoesNotThrow()
    {
        this.repository.GetByIdAsync(Arg.Any<Guid>()).Returns((TeeTimeOpening?)null);

        await ExpireTeeTimeOpeningHandler.Handle(new ExpireTeeTimeOpening(Guid.NewGuid()), this.repository);
    }
}
