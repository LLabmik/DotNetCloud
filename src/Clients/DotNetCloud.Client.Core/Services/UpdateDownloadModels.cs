namespace DotNetCloud.Client.Core.Services;

/// <summary>
/// Progress snapshot emitted while downloading an update asset.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>Number of bytes downloaded so far.</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>Total size in bytes, or <c>null</c> when unknown.</summary>
    public long? TotalBytes { get; init; }

    /// <summary>Progress from 0.0 to 1.0 (0.0 when the total is unknown).</summary>
    public double Percent { get; init; }

    /// <summary>Average download speed in bytes per second.</summary>
    public double BytesPerSecond { get; init; }
}

/// <summary>
/// Result of a completed update download.
/// </summary>
public sealed class DownloadedUpdate
{
    /// <summary>Full path to the downloaded file.</summary>
    public required string FilePath { get; init; }

    /// <summary>File name of the downloaded asset.</summary>
    public required string FileName { get; init; }

    /// <summary>Size of the downloaded file in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Whether the SHA256 checksum was verified. <c>false</c> means no checksum was available.</summary>
    public bool Sha256Verified { get; init; }
}
