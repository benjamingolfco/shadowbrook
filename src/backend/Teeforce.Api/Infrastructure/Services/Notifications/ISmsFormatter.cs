using Teeforce.Domain.Common;

namespace Teeforce.Api.Infrastructure.Services;

public interface ISmsFormatter<in T> where T : INotification
{
    string Format(T notification);
}
