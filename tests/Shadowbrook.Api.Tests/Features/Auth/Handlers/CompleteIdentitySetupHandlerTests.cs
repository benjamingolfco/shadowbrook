using NSubstitute;
using Shadowbrook.Api.Features.Auth.Handlers;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.AppUserAggregate.Events;
using Shadowbrook.Domain.AppUserAggregate.Exceptions;
using Shadowbrook.Domain.Common;

namespace Shadowbrook.Api.Tests.Features.Auth.Handlers;

public class CompleteIdentitySetupHandlerTests
{
    private readonly IRepository<AppUser> repository = Substitute.For<IRepository<AppUser>>();

    [Fact]
    public async Task Handle_LinksIdentityAndActivatesUser()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.ClearDomainEvents();
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Jane", "Smith"),
            this.repository);

        Assert.Equal("entra-oid-123", user.IdentityId);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.True(user.IsActive);
        Assert.Contains(user.DomainEvents, e => e is AppUserSetupCompleted);
    }

    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userId = Guid.NewGuid();
        this.repository.GetByIdAsync(userId).Returns((AppUser?)null);

        var ex = await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(userId, "oid", "Jane", "Smith"),
                this.repository));

        Assert.Contains(userId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedSameOid_IsIdempotent()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        user.ClearDomainEvents();
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Jane", "Smith"),
            this.repository);

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public async Task Handle_AlreadyLinkedDifferentOid_Throws()
    {
        var user = AppUser.CreateAdmin("admin@example.com");
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(user.Id, "different-oid", "Jane", "Smith"),
                this.repository));
    }
}
