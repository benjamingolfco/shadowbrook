namespace Shadowbrook.Domain.WalkUpWaitlist;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
