namespace Shadowbrook.Domain.Services;

public interface IAppUserEmailUniquenessChecker
{
    Task<bool> IsEmailInUseAsync(string email, CancellationToken ct = default);
}
