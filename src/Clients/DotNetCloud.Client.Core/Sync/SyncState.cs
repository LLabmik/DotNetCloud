namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Current operational state of a sync engine instance.
/// </summary>
public enum SyncState
{
    /// <summary>Idle and fully in sync.</summary>
    Idle,

    /// <summary>Actively synchronizing changes.</summary>
    Syncing,

    /// <summary>Sync is paused by the user.</summary>
    Paused,

    /// <summary>An error has occurred; details in <see cref="SyncStatus.LastError"/>.</summary>
    Error,

    /// <summary>Cannot reach the server.</summary>
    Offline,
}
