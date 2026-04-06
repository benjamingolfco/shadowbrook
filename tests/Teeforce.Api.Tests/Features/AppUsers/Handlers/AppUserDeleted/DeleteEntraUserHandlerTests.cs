using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Features.AppUsers.Handlers.AppUserDeleted;
using Teeforce.Domain.Services;
using AppUserDeletedEvent = Teeforce.Domain.AppUserAggregate.Events.AppUserDeleted;

namespace Teeforce.Api.Tests.Features.AppUsers.Handlers.AppUserDeleted;

public class DeleteEntraUserHandlerTests
{
    private readonly IAppUserDeletionService deletionService = Substitute.For<IAppUserDeletionService>();
    private readonly ILogger logger = Substitute.For<ILogger>();

    [Fact]
    public async Task Handle_WithIdentityId_CallsDeletionService()
    {
        var evt = new AppUserDeletedEvent
        {
            AppUserId = Guid.CreateVersion7(),
            IdentityId = "entra-oid-123",
        };

        await DeleteEntraUserHandler.Handle(evt, this.deletionService, this.logger, CancellationToken.None);

        await this.deletionService.Received(1).DeleteAsync("entra-oid-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNullIdentityId_SkipsDeletionService()
    {
        var evt = new AppUserDeletedEvent
        {
            AppUserId = Guid.CreateVersion7(),
            IdentityId = null,
        };

        await DeleteEntraUserHandler.Handle(evt, this.deletionService, this.logger, CancellationToken.None);

        await this.deletionService.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
