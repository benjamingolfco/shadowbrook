using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate;

public interface IAppUserRepository : IRepository<AppUser>
{
    void Add(AppUser appUser);
}
