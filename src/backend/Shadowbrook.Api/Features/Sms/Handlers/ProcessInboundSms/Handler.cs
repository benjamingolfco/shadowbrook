using Microsoft.Extensions.Logging;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;

namespace Shadowbrook.Api.Features.Sms.Handlers;

public record ProcessInboundSms(string FromPhoneNumber, string Body);

public static class ProcessInboundSmsHandler
{
    public static async Task Handle(
        ProcessInboundSms command,
        IGolferRepository golferRepository,
        IWaitlistOfferRepository offerRepository,
        ITextMessageService smsService,
        ILogger logger,
        CancellationToken ct)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(command.FromPhoneNumber);
        if (normalizedPhone is null)
        {
            logger.LogWarning(
                "Invalid phone number {Phone} in inbound SMS",
                command.FromPhoneNumber);
            return;
        }

        var golfer = await golferRepository.GetByPhoneAsync(normalizedPhone);
        if (golfer is null)
        {
            logger.LogWarning(
                "No golfer found for phone {Phone} in inbound SMS",
                normalizedPhone);
            return;
        }

        var offer = await offerRepository.GetMostRecentPendingWalkUpByGolferAsync(golfer.Id);
        if (offer is null)
        {
            await smsService.SendAsync(
                normalizedPhone,
                "You don't have any pending tee time offers right now.",
                ct);
            return;
        }

        // Any non-empty reply is treated as acceptance for v1
        try
        {
            offer.Accept();
        }
        catch (OfferNotPendingException)
        {
            // Race condition: offer was resolved between lookup and accept
            await smsService.SendAsync(
                normalizedPhone,
                "That tee time offer is no longer available.",
                ct);
        }
    }
}
