namespace DotNetCloud.Modules.About.Services;

/// <summary>
/// gRPC API client interface for the About module.
/// Provides system information and version details.
/// </summary>
public interface IAboutApiClient
{
    /// <summary>Gets system information from the About module.</summary>
    Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken ct = default);
}

/// <summary>
/// DTO for system information returned by the About module.
/// </summary>
public sealed record AboutInfoDto
{
    /// <summary>Application version string.</summary>
    public string Version { get; init; } = "";

    /// <summary>Runtime environment (Production, Development, etc.).</summary>
    public string Environment { get; init; } = "";

    /// <summary>.NET runtime version.</summary>
    public string RuntimeVersion { get; init; } = "";

    /// <summary>Operating system description.</summary>
    public string OsDescription { get; init; } = "";

    /// <summary>License status string.</summary>
    public string LicenseStatus { get; init; } = "";

    /// <summary>Server uptime in milliseconds.</summary>
    public string Uptime { get; init; } = "";
}
