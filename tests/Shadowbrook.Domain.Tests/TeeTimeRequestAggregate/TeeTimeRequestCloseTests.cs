using NSubstitute;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Events;

namespace Shadowbrook.Domain.Tests.TeeTimeRequestAggregate;

public class TeeTimeRequestCloseTests
{
    private readonly ITeeTimeRequestRepository repository = Substitute.For<ITeeTimeRequestRepository>();

    [Fact]
    public async Task Close_PendingRequest_SetsClosedStatusAndRaisesEvent()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2, this.repository);
        request.ClearDomainEvents();

        request.Close();

        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
        var domainEvent = Assert.Single(request.DomainEvents);
        var closed = Assert.IsType<TeeTimeRequestClosed>(domainEvent);
        Assert.Equal(request.Id, closed.TeeTimeRequestId);
    }

    [Fact]
    public async Task Close_AlreadyClosed_DoesNothing()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 2, this.repository);
        request.Close();
        request.ClearDomainEvents();

        request.Close();

        Assert.Empty(request.DomainEvents);
        Assert.Equal(TeeTimeRequestStatus.Closed, request.Status);
    }

    [Fact]
    public async Task Close_FulfilledRequest_DoesNothing()
    {
        var request = await TeeTimeRequest.CreateAsync(
            Guid.NewGuid(), new DateOnly(2026, 3, 20), new TimeOnly(10, 0), 1, this.repository);
        request.Fill(Guid.NewGuid(), groupSize: 1, Guid.NewGuid(), Guid.NewGuid());
        request.ClearDomainEvents();

        request.Close();

        Assert.Empty(request.DomainEvents);
        Assert.Equal(TeeTimeRequestStatus.Fulfilled, request.Status);
    }
}
