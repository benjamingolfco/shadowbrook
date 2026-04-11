using NSubstitute;
using Teeforce.Api.Features.Auth.Handlers;
using Teeforce.Domain.AppUserAggregate;
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
    public async Task Handle_PopulatesProfile()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Jane", "Smith"),
            this.repository);

        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
        Assert.True(user.IsIdentitySetupComplete);
    }

    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userId = Guid.NewGuid();
        this.repository.GetByIdAsync(userId).Returns((AppUser?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CompleteIdentitySetupHandler.Handle(
                new CompleteIdentitySetupCommand(userId, "oid", "Jane", "Smith"),
                this.repository));
    }

    [Fact]
    public async Task Handle_ProfileAlreadySetup_IsIdempotent()
    {
        var user = await AppUser.CreateAdmin("admin@example.com", NewChecker());
        user.CompleteProfileSetup("Jane", "Smith");
        this.repository.GetByIdAsync(user.Id).Returns(user);

        await CompleteIdentitySetupHandler.Handle(
            new CompleteIdentitySetupCommand(user.Id, "entra-oid-123", "Different", "Name"),
            this.repository);

        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Smith", user.LastName);
    }
}
