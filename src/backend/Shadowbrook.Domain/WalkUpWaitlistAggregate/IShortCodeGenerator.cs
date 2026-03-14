namespace Shadowbrook.Domain.WalkUpWaitlistAggregate;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
