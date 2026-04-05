namespace Shadowbrook.Api.Infrastructure.Auth;

public class AuthSettings
{
    public const string SectionName = "Auth";

    public bool UseDevAuth { get; set; }
    public string SeedAdminEmails { get; set; } = string.Empty;

    public string[] GetSeedAdminEmailsList() =>
        SeedAdminEmails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
