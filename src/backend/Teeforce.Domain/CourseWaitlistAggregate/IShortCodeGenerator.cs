namespace Teeforce.Domain.CourseWaitlistAggregate;

public interface IShortCodeGenerator
{
    Task<string> GenerateAsync(DateOnly date);
}
