namespace Teeforce.Api.Infrastructure.Services;

public interface ISmsSender
{
    Task Send(string toPhoneNumber, string message, CancellationToken ct = default);
}
