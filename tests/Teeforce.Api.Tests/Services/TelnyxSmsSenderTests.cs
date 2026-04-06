using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Sms;

namespace Teeforce.Api.Tests.Services;

public class TelnyxSmsSenderTests
{
    private static TelnyxSmsSender CreateSender(
        HttpMessageHandler handler,
        string fromNumber = "+10001112222")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com") };
        var options = Options.Create(new TelnyxOptions { FromNumber = fromNumber });
        var logger = Substitute.For<ILogger<TelnyxSmsSender>>();
        return new TelnyxSmsSender(httpClient, options, logger);
    }

    [Fact]
    public async Task Send_PostsCorrectPayloadToTelnyxApi()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"id":"msg-123"}}""")
            };
        });

        var sender = CreateSender(handler);
        await sender.Send("+15559876543", "Hello from Teeforce", CancellationToken.None);

        Assert.NotNull(capturedBody);
        var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("+10001112222", doc.RootElement.GetProperty("from").GetString());
        Assert.Equal("+15559876543", doc.RootElement.GetProperty("to").GetString());
        Assert.Equal("Hello from Teeforce", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Send_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"errors":[{"detail":"Invalid API key"}]}""")
        });

        var sender = CreateSender(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sender.Send("+15551234567", "Test", CancellationToken.None));
    }

    private class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }
}
