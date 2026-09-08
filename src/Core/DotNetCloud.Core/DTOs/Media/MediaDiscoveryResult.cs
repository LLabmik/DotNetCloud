namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Result of a read-only media library discovery. Reports files that are present
/// in the configured sources but not yet indexed by the module, without importing
/// them. Used by the Video/Music module pages to prompt users that new media files
/// are available and offer a "Scan Now" import action.
/// </summary>
public sealed class MediaDiscoveryResult
{
    /// <summary>Whether the discovery completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when discovery failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Total matching media files found across all sources.</summary>
    public int TotalFound { get; set; }

    /// <summary>Files already indexed by the module (they would be skipped on import).</summary>
    public int AlreadyIndexed { get; set; }

    /// <summary>Files that are new (not yet indexed) and would be imported by a scan.</summary>
    public int NewFileCount { get; set; }

    /// <summary>Names of up to 20 new files, sorted alphabetically, for display.</summary>
    public List<string> SampleFileNames { get; set; } = [];
}
