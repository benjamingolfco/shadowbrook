using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Tests.Services;

public record TestNotification(string Message) : INotification;

public class TestSmsFormatter : SmsFormatter<TestNotification>
{
    protected override string FormatMessage(TestNotification notification) => notification.Message;
}

public class DefaultEmailFormatterTests
{
    [Fact]
    public void Format_UsesSmsFormatterTextAsBody()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<ISmsFormatter, TestSmsFormatter>(typeof(TestNotification));
        var sp = services.BuildServiceProvider();
        var logger = Substitute.For<ILogger<DefaultEmailFormatter>>();

        var sut = new DefaultEmailFormatter(sp, logger);
        var notification = new TestNotification("Hello from SMS");

        var (subject, body) = sut.Format(notification);

        Assert.Equal("Teeforce Notification", subject);
        Assert.Equal("Hello from SMS", body);
    }

    [Fact]
    public void Format_LogsInformationAboutFallback()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<ISmsFormatter, TestSmsFormatter>(typeof(TestNotification));
        var sp = services.BuildServiceProvider();
        var logger = Substitute.For<ILogger<DefaultEmailFormatter>>();

        var sut = new DefaultEmailFormatter(sp, logger);
        sut.Format(new TestNotification("test"));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
