using DotNetCloud.Core.Models;

namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Tracks the cleanup progress when an admin shared folder is deleted.
/// Persisted in the Files module database so the admin UI can poll for status.
/// </summary>
public sealed class AdminSharedFolderCleanupStatus
{
    /// <summary>Unique cleanup job identifier.</summary>
    public Guid CleanupJobId { get; set; } = Guid.CreateVersion7();

    /// <summary>The deleted shared folder definition ID.</summary>
    public Guid SharedFolderId { get; set; }

    /// <summary>Display name of the deleted folder.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Current cleanup phase.</summary>
    public CleanupPhase Phase { get; set; } = CleanupPhase.DeletingDefinition;

    /// <summary>Number of search documents removed so far.</summary>
    public int SearchDocsRemoved { get; set; }

    /// <summary>Total number of search documents to remove.</summary>
    public int SearchDocsTotal { get; set; }

    /// <summary>Number of affected users for media cleanup.</summary>
    public int AffectedUsers { get; set; }

    /// <summary>Number of users processed so far in media cleanup.</summary>
    public int UsersCleaned { get; set; }

    /// <summary>Number of media entities (tracks/videos/photos) removed.</summary>
    public int MediaEntitiesRemoved { get; set; }

    /// <summary>When the cleanup job started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the cleanup job completed (or failed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if the cleanup failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether the cleanup job is complete (succeeded or failed).</summary>
    public bool IsComplete => Phase == CleanupPhase.Complete || Phase == CleanupPhase.Failed;
}
