using NSubstitute;
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Api.Tests.Handlers;

public class CloseTeeTimeRequestHandlerTests
{
    private readonly ITeeTimeRequestRepository requestRepo = Substitute.For<ITeeTimeRequestRepository>();

    [Fact]
    public async Task Handle_RequestNotFound_DoesNothing()
    {
        this.requestRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((TeeTimeRequest?)null);

        var command = new CloseTeeTimeRequest(Guid.NewGuid());
        await CloseTeeTimeRequestHandler.Handle(command, this.requestRepo);
    }

    [Fact]
    public async Task Handle_PendingRequest_ClosesIt()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2,
            Substitute.For<ITeeTimeRequestRepository>());
        request.ClearDomainEvents();

        this.requestRepo.GetByIdAsync(request.Id).Returns(request);

        var command = new CloseTeeTimeRequest(request.Id);
        await CloseTeeTimeRequestHandler.Handle(command, this.requestRepo);

        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
        var domainEvent = Assert.Single(request.DomainEvents);
        Assert.IsType<TeeTimeRequestClosed>(domainEvent);
    }
}
