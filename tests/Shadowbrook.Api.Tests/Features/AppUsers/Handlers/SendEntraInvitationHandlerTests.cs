using NSubstitute;
using Shadowbrook.Api.Features.AppUsers.Handlers.AppUserCreated;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.Services;
using AppUserCreatedEvent = Shadowbrook.Domain.AppUserAggregate.Events.AppUserCreated;

namespace Shadowbrook.Api.Tests.Features.AppUsers.Handlers;

public class SendEntraInvitationHandlerTests
{
    private readonly IAppUserRepository appUserRepo = Substitute.For<IAppUserRepository>();
    private readonly IAppUserInvitationService invitationService = Substitute.For<IAppUserInvitationService>();

    private static IAppUserEmailUniquenessChecker NewChecker()
    {
        var checker = Substitute.For<IAppUserEmailUniquenessChecker>();
        checker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return checker;
    }

    [Fact]
    public async Task Handle_LoadsAppUserAndCallsInvite()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        this.appUserRepo.GetByIdAsync(user.Id).Returns(user);
        this.invitationService.SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>())
            .Returns("entra-oid-456");
        var evt = new AppUserCreatedEvent { AppUserId = user.Id, Email = user.Email, Role = user.Role };

        await SendEntraInvitationHandler.Handle(evt, this.appUserRepo, this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
        Assert.Equal("entra-oid-456", user.IdentityId);
        Assert.NotNull(user.InviteSentAt);
    }
}
