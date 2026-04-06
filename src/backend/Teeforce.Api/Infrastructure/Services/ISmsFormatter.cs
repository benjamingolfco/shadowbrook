using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface ISmsFormatter
{
    string Format(INotification notification);
}

public abstract class SmsFormatter<T> : ISmsFormatter where T : INotification
{
    public string Format(INotification notification) => FormatMessage((T)notification);

    protected abstract string FormatMessage(T notification);
}
