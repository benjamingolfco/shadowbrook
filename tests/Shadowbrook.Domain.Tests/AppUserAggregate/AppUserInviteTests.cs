using NSubstitute;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.AppUserAggregate.Events;
using Shadowbrook.Domain.Services;

namespace Shadowbrook.Domain.Tests.AppUserAggregate;

public class AppUserInviteTests
{
    private readonly IAppUserInvitationService invitationService = Substitute.For<IAppUserInvitationService>();

    private static IAppUserEmailUniquenessChecker NewChecker()
    {
        var checker = Substitute.For<IAppUserEmailUniquenessChecker>();
        checker.IsEmailInUseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return checker;
    }

    [Fact]
    public async Task Invite_SetsIdentityIdAndInviteSentAt()
    {
        var user = await AppUser.CreateAdminAsync("admin@example.com", NewChecker());
        this.invitationService.SendInvitationAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");

        await user.Invite(this.invitationService, CancellationToken.None);

        Assert.Equal("entra-object-id-123", user.IdentityId);
        Assert.NotNull(user.InviteSentAt);
        Assert.True(user.InviteSentAt >= DateTimeOffset.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public async Task Invite_CallsServiceWithCorrectEmail()
    {
        var user = await AppUser.CreateAdminAsync("admin@example.com", NewChecker());
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync("admin@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_RaisesAppUserInvitedEvent()
    {
        var user = await AppUser.CreateAdminAsync("admin@example.com", NewChecker());
        user.ClearDomainEvents();
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("entra-oid-456");

        await user.Invite(this.invitationService, CancellationToken.None);

        var evt = Assert.Single(user.DomainEvents);
        var invitedEvent = Assert.IsType<AppUserInvited>(evt);
        Assert.Equal(user.Id, invitedEvent.AppUserId);
        Assert.Equal("admin@example.com", invitedEvent.Email);
        Assert.Equal("entra-oid-456", invitedEvent.EntraObjectId);
    }

    [Fact]
    public async Task Invite_WhenAlreadyInvited_IsNoOp()
    {
        var user = await AppUser.CreateAdminAsync("admin@example.com", NewChecker());
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("entra-object-id-123");
        await user.Invite(this.invitationService, CancellationToken.None);

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.Received(1).SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_WhenIdentityAlreadySet_IsNoOp()
    {
        var user = await AppUser.CreateAdminAsync("admin@example.com", NewChecker());
        user.CompleteIdentitySetup("existing-oid", "Jane", "Smith");
        this.invitationService.SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("different-oid");

        await user.Invite(this.invitationService, CancellationToken.None);

        await this.invitationService.DidNotReceive().SendInvitationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
