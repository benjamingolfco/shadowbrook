using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate;

public interface IAppUserRepository : IRepository<AppUser>
{
    void Add(AppUser appUser);
}
