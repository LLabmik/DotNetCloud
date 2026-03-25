using DotNetCloud.Client.Core.Sync;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for a single connected DotNetCloud account displayed in the
/// Settings window account list.
/// </summary>
public sealed class AccountViewModel : ViewModelBase
{
    private string _state;
    private int _pendingUploads;
    private int _pendingDownloads;
    private DateTime? _lastSyncedAt;
    private string? _lastError;

    /// <summary>Unique context identifier (matches the SyncService context ID).</summary>
    public Guid ContextId { get; }

    /// <summary>Human-readable display name (e.g. <c>Ben @ cloud.example.com</c>).</summary>
    public string DisplayName { get; }

    /// <summary>Server base URL.</summary>
    public string ServerBaseUrl { get; }

    /// <summary>Absolute local sync folder path.</summary>
    public string LocalFolderPath { get; }

    /// <summary>Current sync state string (e.g. <c>Idle</c>, <c>Syncing</c>).</summary>
    public string State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    /// <summary>Number of files pending upload.</summary>
    public int PendingUploads
    {
        get => _pendingUploads;
        set => SetProperty(ref _pendingUploads, value);
    }

    /// <summary>Number of files pending download.</summary>
    public int PendingDownloads
    {
        get => _pendingDownloads;
        set => SetProperty(ref _pendingDownloads, value);
    }

    /// <summary>UTC timestamp of the last successful sync pass.</summary>
    public DateTime? LastSyncedAt
    {
        get => _lastSyncedAt;
        set => SetProperty(ref _lastSyncedAt, value);
    }

    /// <summary>Last error message, or <see langword="null"/> when healthy.</summary>
    public string? LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }

    /// <summary>Initializes a new <see cref="AccountViewModel"/> from a <see cref="SyncContextRegistration"/>.</summary>
    public AccountViewModel(SyncContextRegistration registration)
    {
        ContextId = registration.Id;
        DisplayName = registration.DisplayName;
        ServerBaseUrl = registration.ServerBaseUrl;
        LocalFolderPath = registration.LocalFolderPath;
        _state = "Idle";
    }
}
