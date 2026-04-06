using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Notifications;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.Services;
using Wolverine;

namespace Teeforce.Api.Tests.Services;

public record FakeNotification(string Content) : INotification;

public class FakeNotificationSmsFormatter : ISmsFormatter<FakeNotification>
{
    public string Format(FakeNotification n) => $"SMS: {n.Content}";
}

public class FakeNotificationEmailFormatter : IEmailFormatter<FakeNotification>
{
    public (string Subject, string Body) Format(FakeNotification n) =>
        ("Test Subject", $"Email: {n.Content}");
}

public class NotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext db;
    private readonly IMessageBus messageBus;
    private readonly ILogger<NotificationService> logger;
    private readonly IAppUserEmailUniquenessChecker emailChecker;
    private readonly ServiceProvider serviceProvider;
    private readonly NotificationService sut;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        this.db = new ApplicationDbContext(options, userContext: null);
        this.messageBus = Substitute.For<IMessageBus>();
        this.logger = Substitute.For<ILogger<NotificationService>>();
        this.emailChecker = Substitute.For<IAppUserEmailUniquenessChecker>();
        this.emailChecker.IsEmailInUse(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var services = new ServiceCollection();
        services.AddScoped<ISmsFormatter<FakeNotification>, FakeNotificationSmsFormatter>();
        services.AddScoped<IEmailFormatter<FakeNotification>, FakeNotificationEmailFormatter>();
        this.serviceProvider = services.BuildServiceProvider();

        this.sut = new NotificationService(this.db, this.messageBus, this.serviceProvider, this.logger);
    }

    public void Dispose()
    {
        this.db.Dispose();
        this.serviceProvider.Dispose();
    }

    [Fact]
    public async Task Send_AppUserWithPhone_PublishesDeliverSms()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        typeof(AppUser).GetProperty("Phone")!.SetValue(user, "+15551234567");
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverSms>(cmd => cmd.PhoneNumber == "+15551234567" && cmd.Message == "SMS: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_AppUserWithEmailOnly_PublishesDeliverEmail()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverEmail>(cmd => cmd.EmailAddress == "jane@example.com" && cmd.Subject == "Test Subject" && cmd.Body == "Email: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_NoAppUserButGolferWithPhone_PublishesDeliverSms()
    {
        var golfer = Golfer.Create("+15559876543", "Bob", "Green");
        this.db.Golfers.Add(golfer);
        await this.db.SaveChangesAsync();

        await this.sut.Send(golfer.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(
            Arg.Is<DeliverSms>(cmd => cmd.PhoneNumber == "+15559876543" && cmd.Message == "SMS: hello"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task Send_NoContactInfo_LogsWarningAndSkips()
    {
        var unknownId = Guid.CreateVersion7();

        await this.sut.Send(unknownId, new FakeNotification("hello"));

        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverSms>(), Arg.Any<DeliveryOptions?>());
        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverEmail>(), Arg.Any<DeliveryOptions?>());
        this.logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Send_AppUserWithPhoneAndEmail_PrefersSms()
    {
        var user = await AppUser.CreateAdmin("jane@example.com", this.emailChecker);
        typeof(AppUser).GetProperty("Phone")!.SetValue(user, "+15551234567");
        this.db.AppUsers.Add(user);
        await this.db.SaveChangesAsync();

        await this.sut.Send(user.Id, new FakeNotification("hello"));

        await this.messageBus.Received(1).PublishAsync(Arg.Any<DeliverSms>(), Arg.Any<DeliveryOptions?>());
        await this.messageBus.DidNotReceive().PublishAsync(Arg.Any<DeliverEmail>(), Arg.Any<DeliveryOptions?>());
    }
}
