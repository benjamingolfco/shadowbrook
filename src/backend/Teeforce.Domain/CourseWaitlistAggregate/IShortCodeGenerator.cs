namespace Shadowbrook.Domain.CourseWaitlistAggregate;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
