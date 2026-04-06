using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface IEmailFormatter<in T> where T : INotification
{
    (string Subject, string Body) Format(T notification);
}
