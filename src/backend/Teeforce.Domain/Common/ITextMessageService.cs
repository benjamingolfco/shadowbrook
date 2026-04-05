namespace Teeforce.Domain.Common;

public interface ITextMessageService
{
    Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
