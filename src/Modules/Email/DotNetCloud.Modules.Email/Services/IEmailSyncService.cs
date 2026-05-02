namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Background service interface for email sync operations.
/// </summary>
public interface IEmailSyncService
{
    /// <summary>Triggers a manual sync for the specified account.</summary>
    Task SyncAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Triggers a manual sync for all enabled accounts.</summary>
    Task SyncAllAsync(CancellationToken ct = default);

    /// <summary>Gets the current sync status for an account.</summary>
    Task<EmailSyncStatus?> GetSyncStatusAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>Current sync status for an account.</summary>
public sealed class EmailSyncStatus
{
    /// <summary>The account ID.</summary>
    public Guid AccountId { get; set; }

    /// <summary>When the sync started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>When the sync completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Status label (Running, Completed, Failed).</summary>
    public string Status { get; set; } = "Unknown";

    /// <summary>Number of new messages synced.</summary>
    public int NewMessages { get; set; }

    /// <summary>Number of messages updated.</summary>
    public int UpdatedMessages { get; set; }

    /// <summary>Number of messages deleted.</summary>
    public int DeletedMessages { get; set; }

    /// <summary>Error message if the sync failed.</summary>
    public string? ErrorMessage { get; set; }
}
