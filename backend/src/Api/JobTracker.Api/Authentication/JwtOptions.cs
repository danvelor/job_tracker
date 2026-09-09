namespace JobTracker.Api.Authentication;

/// <summary>
/// Bound from configuration. The key is never committed: appsettings.json
/// carries issuer and audience only, and the key arrives from user secrets in
/// development and from the environment everywhere else.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;
}
