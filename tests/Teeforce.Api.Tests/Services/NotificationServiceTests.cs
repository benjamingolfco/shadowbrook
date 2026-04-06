using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly ISmsSender smsSender;
    private readonly IEmailSender emailSender;
    private readonly ILogger<NotificationService> logger;
    private readonly NotificationService sut;
    private readonly IAppUserEmailUniquenessChecker emailChecker;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        this.db = new ApplicationDbContext(options, userContext: null);
        this.smsSender = Substitute.For<ISmsSender>();
        this.emailSender = Substitute.For<IEmailSender>();
        this.logger = Substitute.For<ILogger<NotificationService>>();
        this.emailChecker = Substitute.For<IAppUserEmailUniquenessChecker>();
        this.emailChecker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        this.sut = new NotificationService(this.smsSender, this.emailSender, this.db, this.logger);
    }

    public void Dispose() => this.db.Dispose();

    [Fact]
    public async Task Send_GolferWithPhone_SendsSms()
    {
        var golfer = Golfer.Create("+15551234567", "Jane", "Smith");
        this.db.Golfers.Add(golfer);
        await this.db.SaveChangesAsync();

        await this.sut.Send(golfer.Id, "Your tee time is confirmed.");

        await this.smsSender.Received(1).Send("+15551234567", "Your tee time is confirmed.", Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_NoGolferButAppUserWithEmail_SendsEmail()
    {
        var user = await AppUser.CreateAdmin("golfer@example.com", this.emailChecker);
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, "You have been invited.");

        await this.emailSender.Received(1).Send("golfer@example.com", "Teeforce Notification", "You have been invited.", Arg.Any<CancellationToken>());
        await this.smsSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_NoContactInfo_LogsWarningAndSkips()
    {
        var unknownId = Guid.CreateVersion7();

        await this.sut.Send(unknownId, "Test message.");

        await this.smsSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        this.logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Send_GolferWithPhoneAndAppUserWithEmail_PrefersSms()
    {
        var sharedId = Guid.CreateVersion7();

        var golfer = Golfer.Create("+15551112222", "Alice", "Brown");
        var golferEntry = this.db.Golfers.Add(golfer);
        golferEntry.Property("Id").CurrentValue = sharedId;

        var user = await AppUser.CreateAdmin("alice@example.com", this.emailChecker);
        var userEntry = this.db.AppUsers.Add(user);
        userEntry.Property("Id").CurrentValue = sharedId;

        await this.db.SaveChangesAsync();

        await this.sut.Send(sharedId, "Priority message.");

        await this.smsSender.Received(1).Send("+15551112222", "Priority message.", Arg.Any<CancellationToken>());
        await this.emailSender.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
