using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;

namespace Teeforce.Api.Infrastructure.Services.Notifications.Sms.Http;

// If API key reloading is ever needed, switch IOptions<TelnyxOptions> to IOptionsMonitor<TelnyxOptions>.
public class TelnyxAuthHandler(IOptions<TelnyxOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
