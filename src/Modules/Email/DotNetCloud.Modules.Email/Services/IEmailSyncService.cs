namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Email account sync operations.
/// </summary>
/// <remarks>
/// The module does not schedule its own syncs. The core drives them: it asks for the accounts that
/// are due, then invokes <see cref="SyncAccountAsync"/> naming both the account and the user, and
/// the user it names must be the one the core signed an assertion for. A module-started timer had to
/// decide who each sync belonged to on its own, which is the pattern this replaces.
/// </remarks>
public interface IEmailSyncService
{
    /// <summary>
    /// Syncs one account for <paramref name="userId"/>.
    /// </summary>
    /// <param name="accountId">The account to sync.</param>
    /// <param name="userId">The owner the sync is performed for.</param>
    /// <param name="ct">A token to cancel the sync.</param>
    /// <returns>
    /// <see langword="true"/> when the account exists, belongs to <paramref name="userId"/> and was
    /// synced; otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> SyncAccountAsync(Guid accountId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lists enabled accounts whose last sync is at least <paramref name="minAge"/> old.
    /// </summary>
    /// <param name="minAge">The minimum age of the last sync; <see cref="TimeSpan.Zero"/> for all.</param>
    /// <param name="ct">A token to cancel the query.</param>
    /// <returns>The accounts that are due, oldest sync first.</returns>
    Task<IReadOnlyList<EmailSyncCandidate>> ListSyncCandidatesAsync(
        TimeSpan minAge, CancellationToken ct = default);

    /// <summary>Gets the current sync status for an account.</summary>
    Task<EmailSyncStatus?> GetSyncStatusAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// An account the core may be asked to sync, and the user it belongs to.
/// </summary>
/// <param name="AccountId">The account identifier.</param>
/// <param name="UserId">The account owner, i.e. the user the core must vouch for.</param>
/// <param name="LastSyncedAt">
/// When the account was last synced, or <see langword="null"/> when it never has been.
/// </param>
public sealed record EmailSyncCandidate(Guid AccountId, Guid UserId, DateTime? LastSyncedAt);

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
