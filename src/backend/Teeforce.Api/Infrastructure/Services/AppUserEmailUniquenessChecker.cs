using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.Services;

namespace Shadowbrook.Api.Infrastructure.Services;

public class AppUserEmailUniquenessChecker(ApplicationDbContext db) : IAppUserEmailUniquenessChecker
{
    public Task<bool> IsEmailInUse(string email, CancellationToken ct = default) =>
        db.AppUsers.AnyAsync(u => u.Email == email, ct);
}
