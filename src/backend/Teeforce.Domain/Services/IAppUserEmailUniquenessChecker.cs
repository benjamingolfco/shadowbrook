namespace Teeforce.Domain.Services;

public interface IAppUserEmailUniquenessChecker
{
    Task<bool> IsEmailInUse(string email, CancellationToken ct = default);
}
