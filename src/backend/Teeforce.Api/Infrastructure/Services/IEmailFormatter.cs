using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface IEmailFormatter
{
    (string Subject, string Body) Format(INotification notification);
}

public abstract class EmailFormatter<T> : IEmailFormatter where T : INotification
{
    public (string Subject, string Body) Format(INotification notification) => FormatMessage((T)notification);

    protected abstract (string Subject, string Body) FormatMessage(T notification);
}
