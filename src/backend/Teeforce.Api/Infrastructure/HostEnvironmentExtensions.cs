namespace Teeforce.Api.Infrastructure;

public static class HostEnvironmentExtensions
{
    public static bool IsIntegrationTesting(this IHostEnvironment environment) =>
        environment.IsEnvironment("Testing");
}
