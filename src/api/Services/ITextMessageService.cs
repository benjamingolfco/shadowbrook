namespace Shadowbrook.Api.Services;

public interface ITextMessageService
{
    Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
