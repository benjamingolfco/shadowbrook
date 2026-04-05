using NSubstitute;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.AppUserAggregate.Events;
using Teeforce.Domain.AppUserAggregate.Exceptions;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Tests.Features.Auth.Handlers;

public class CompleteIdentitySetupHandlerTests
{
    private readonly IAppUserRepository repository = Substitute.For<IAppUserRepository>();

    private static IAppUserEmailUniquenessChecker NewChecker()
    {
        var checker = Substitute.For<IAppUserEmailUniquenessChecker>();
        checker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return checker;
    }

    [Fact]
    public async Task Handle_LinksIdentityAndActivatesUser()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
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
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
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
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.CompleteIdentitySetup("entra-oid-123", "Jane", "Smith");
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(user.Id, "different-oid", "Jane", "Smith"),
                this.repository));
    }
}
