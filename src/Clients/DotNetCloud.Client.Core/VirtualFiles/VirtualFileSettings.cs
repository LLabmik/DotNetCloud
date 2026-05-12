namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// Controls how files are stored locally.
/// </summary>
public enum VirtualFileStorageMode
{
    /// <summary>All files are downloaded eagerly. Current default behavior.</summary>
    DownloadAll = 0,

    /// <summary>Files are metadata-only placeholders. Content downloads on first access.</summary>
    FilesOnDemand = 1,
}

/// <summary>
/// User-configurable settings for virtual file syncing.
/// Persisted alongside other local settings JSON.
/// </summary>
public sealed class VirtualFileSettings
{
    /// <summary>Download all files eagerly, or use placeholders with on-demand hydration.</summary>
    public VirtualFileStorageMode StorageMode { get; set; } = VirtualFileStorageMode.DownloadAll;

    /// <summary>Maximum size of the local content cache in bytes. 0 = no limit.</summary>
    public long MaxCacheSizeBytes { get; set; }

    /// <summary>Set of file paths pinned for offline access (case-insensitive).</summary>
    public HashSet<string> PinList { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
