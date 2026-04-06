using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Teeforce.Api.Infrastructure.Configuration;

namespace Teeforce.Api.Infrastructure.Services;

public class TelnyxSmsSender(
    HttpClient httpClient,
    IOptions<TelnyxOptions> options,
    ILogger<TelnyxSmsSender> logger) : ISmsSender
{
    public async Task Send(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        var payload = new { from = options.Value.FromNumber, to = toPhoneNumber, text = message };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/messages");
        // Authorization header is set by TelnyxAuthHandler in the HttpClient pipeline.
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Telnyx API error {StatusCode}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        logger.LogInformation("SMS sent to {PhoneNumber} via Telnyx", toPhoneNumber);
    }
}
