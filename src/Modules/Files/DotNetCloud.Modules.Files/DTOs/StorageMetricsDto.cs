namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Reports storage usage and content-hash deduplication savings.
/// </summary>
public sealed record StorageMetricsDto
{
    /// <summary>Bytes actually stored on disk (unique chunks only, each counted once).</summary>
    public long PhysicalStorageBytes { get; init; }

    /// <summary>Bytes that would be stored if deduplication were disabled (sum of all version sizes).</summary>
    public long LogicalStorageBytes { get; init; }

    /// <summary>Bytes saved by deduplication (LogicalStorageBytes − PhysicalStorageBytes).</summary>
    public long DeduplicationSavingsBytes { get; init; }

    /// <summary>Total number of unique chunks stored.</summary>
    public int TotalUniqueChunks { get; init; }

    /// <summary>Total number of file versions stored across all files.</summary>
    public int TotalVersions { get; init; }

    /// <summary>Total number of files (non-deleted).</summary>
    public int TotalFiles { get; init; }
}
