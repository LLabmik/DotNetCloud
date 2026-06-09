namespace DotNetCloud.Core.DTOs.Media;

/// <summary>
/// Represents a discovered file candidate during a media library scan.
/// Returned by the Files module's scan RPC — contains minimal metadata
/// needed for Core.Server to decide whether to index the file.
/// </summary>
public sealed record MediaFileCandidateDto
{
    /// <summary>FileNode ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>File name with extension.</summary>
    public required string Name { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>MIME type of the file.</summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>Whether the node is a virtual mount (not a real FileNode).</summary>
    public bool IsVirtual { get; init; }

    /// <summary>Optional collection/album/source name for grouping.</summary>
    public string? SourceName { get; init; }

    /// <summary>Optional relative sub-folder path within the source root.</summary>
    public string? SubFolderPath { get; init; }
}

/// <summary>
/// Result of a media folder scan operation returned by the Files module gRPC service.
/// </summary>
public sealed record MediaScanCandidatesResult
{
    /// <summary>Whether the scan completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Error message if the scan failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Total number of matching files found.</summary>
    public int TotalFound { get; init; }

    /// <summary>The discovered file candidates.</summary>
    public IReadOnlyList<MediaFileCandidateDto> Candidates { get; init; } = [];
}
