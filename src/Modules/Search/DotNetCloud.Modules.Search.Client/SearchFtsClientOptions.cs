namespace DotNetCloud.Modules.Search.Client;

/// <summary>
/// Configuration options for the Search FTS gRPC client.
/// </summary>
public sealed class SearchFtsClientOptions
{
    /// <summary>
    /// The section name used in configuration (e.g., appsettings.json).
    /// </summary>
    public const string SectionName = "SearchModule";

    /// <summary>
    /// The gRPC address of the Search module (e.g., "http://localhost:5080", "unix:///var/run/dotnetcloud/search.sock").
    /// When null or empty, the FTS client is disabled and modules fall back to local search.
    /// </summary>
    public string? SearchModuleAddress { get; set; }

    /// <summary>
    /// Timeout for gRPC calls to the Search module. Default: 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
