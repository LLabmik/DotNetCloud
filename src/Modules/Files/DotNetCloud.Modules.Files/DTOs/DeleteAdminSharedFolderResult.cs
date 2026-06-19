using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Result returned when an admin shared folder is deleted.
/// Contains the cleanup job ID for progress polling and initial stats.
/// </summary>
public sealed record DeleteAdminSharedFolderResult
{
    /// <summary>Whether the definition was successfully deleted.</summary>
    public bool Deleted { get; init; }

    /// <summary>Unique job ID for tracking cleanup progress.</summary>
    public Guid CleanupJobId { get; init; }

    /// <summary>Total number of search documents that need removal.</summary>
    public int PendingSearchRemovals { get; init; }

    /// <summary>Number of search documents successfully removed so far.</summary>
    public int SearchDocsRemoved { get; init; }

    /// <summary>Whether media cleanup is pending (executed by Core.Server asynchronously).</summary>
    public bool PendingMediaCleanup { get; init; }

    /// <summary>Mounted entries gathered before deletion, for downstream media cleanup.</summary>
    public IReadOnlyList<MountedEntryInfo> MountedEntries { get; init; } = [];
}
