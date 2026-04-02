using Shadowbrook.Domain.AppUserAggregate;

namespace Shadowbrook.Api.Infrastructure.Auth;

public interface IAppUserClaimsProvider
{
    Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId);
    Task<AppUserClaimsData?> GetByEmailUnlinkedAsync(string email);
}

public record AppUserClaimsData(
    Guid AppUserId,
    Guid? OrganizationId,
    AppUserRole Role,
    bool IsActive);
