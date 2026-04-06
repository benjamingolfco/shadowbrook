using Microsoft.Extensions.Logging;
using NSubstitute;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Tests.Services;

public record TestNotification(string Message) : INotification;

public class TestSmsFormatter : ISmsFormatter<TestNotification>
{
    public string Format(TestNotification notification) => notification.Message;
}

public class DefaultEmailFormatterTests
{
    [Fact]
    public void Format_UsesSmsFormatterTextAsBody()
    {
        var logger = Substitute.For<ILogger<DefaultEmailFormatter<TestNotification>>>();
        var sut = new DefaultEmailFormatter<TestNotification>(new TestSmsFormatter(), logger);

        var (subject, body) = sut.Format(new TestNotification("Hello from SMS"));

        Assert.Equal("Teeforce Notification", subject);
        Assert.Equal("Hello from SMS", body);
    }

    [Fact]
    public void Format_LogsInformationAboutFallback()
    {
        var logger = Substitute.For<ILogger<DefaultEmailFormatter<TestNotification>>>();
        var sut = new DefaultEmailFormatter<TestNotification>(new TestSmsFormatter(), logger);

        sut.Format(new TestNotification("test"));

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
