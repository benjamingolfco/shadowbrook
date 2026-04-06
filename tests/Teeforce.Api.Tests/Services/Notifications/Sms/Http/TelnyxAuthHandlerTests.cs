using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Services.Notifications.Sms.Http;

namespace Teeforce.Api.Tests.Services.Notifications.Sms.Http;

public class TelnyxAuthHandlerTests
{
    private static HttpMessageInvoker CreateInvoker(
        TelnyxOptions telnyxOptions,
        Action<HttpRequestMessage> capture)
    {
        var inner = new CapturingHandler(req =>
        {
            capture(req);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new TelnyxAuthHandler(Options.Create(telnyxOptions)) { InnerHandler = inner };
        return new HttpMessageInvoker(handler);
    }

    [Fact]
    public async Task SendAsync_SetsAuthorizationHeader()
    {
        AuthenticationHeaderValue? capturedAuth = null;
        var invoker = CreateInvoker(
            new TelnyxOptions { ApiKey = "test-api-key" },
            req => capturedAuth = req.Headers.Authorization);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://api.telnyx.com/v2/messages"),
            CancellationToken.None);

        Assert.NotNull(capturedAuth);
        Assert.Equal("Bearer", capturedAuth!.Scheme);
        Assert.Equal("test-api-key", capturedAuth.Parameter);
    }

    [Fact]
    public async Task SendAsync_OverwritesExistingAuthorizationHeader()
    {
        AuthenticationHeaderValue? capturedAuth = null;
        var invoker = CreateInvoker(
            new TelnyxOptions { ApiKey = "test-api-key" },
            req => capturedAuth = req.Headers.Authorization);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.telnyx.com/v2/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stale-key");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(capturedAuth);
        Assert.Equal("Bearer", capturedAuth!.Scheme);
        Assert.Equal("test-api-key", capturedAuth.Parameter);
    }

    // Documents current behavior pending Open Question #1 (empty key → "Bearer " sent to Telnyx, 401 response).
    // If a startup validator for TelnyxOptions is added later, this test will need updating.
    [Fact]
    public async Task SendAsync_EmptyApiKey_SetsBearerWithEmptyParameter()
    {
        AuthenticationHeaderValue? capturedAuth = null;
        var invoker = CreateInvoker(
            new TelnyxOptions { ApiKey = string.Empty },
            req => capturedAuth = req.Headers.Authorization);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://api.telnyx.com/v2/messages"),
            CancellationToken.None);

        Assert.NotNull(capturedAuth);
        Assert.Equal("Bearer", capturedAuth!.Scheme);
        Assert.Equal(string.Empty, capturedAuth.Parameter);
    }

    [Fact]
    public async Task SendAsync_CallsInnerHandler()
    {
        var innerCalled = false;
        var invoker = CreateInvoker(
            new TelnyxOptions { ApiKey = "test-api-key" },
            _ => innerCalled = true);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://api.telnyx.com/v2/messages"),
            CancellationToken.None);

        Assert.True(innerCalled);
    }

    private class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }
}
