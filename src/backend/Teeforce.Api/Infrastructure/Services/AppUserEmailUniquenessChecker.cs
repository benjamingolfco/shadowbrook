using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.Services;

namespace Teeforce.Api.Infrastructure.Services;

public class AppUserEmailUniquenessChecker(ApplicationDbContext db) : IAppUserEmailUniquenessChecker
{
    public Task<bool> IsEmailInUse(string email, CancellationToken ct = default) =>
        db.AppUsers.AnyAsync(u => u.Email == email, ct);
}
