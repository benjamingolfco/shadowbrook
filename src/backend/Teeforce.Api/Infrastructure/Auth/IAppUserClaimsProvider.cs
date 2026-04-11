using Teeforce.Domain.AppUserAggregate;

namespace Teeforce.Api.Infrastructure.Auth;

public interface IAppUserClaimsProvider
{
    Task<AppUserClaimsData?> GetByIdentityIdAsync(string identityId);
}

public record AppUserClaimsData(
    Guid AppUserId,
    Guid? OrganizationId,
    AppUserRole Role,
    bool IsActive,
    bool NeedsProfileSetup);
