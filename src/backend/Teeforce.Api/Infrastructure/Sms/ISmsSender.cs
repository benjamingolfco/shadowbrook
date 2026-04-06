namespace Teeforce.Api.Infrastructure.Sms;

public interface ISmsSender
{
    Task Send(string toPhoneNumber, string message, CancellationToken ct = default);
}
