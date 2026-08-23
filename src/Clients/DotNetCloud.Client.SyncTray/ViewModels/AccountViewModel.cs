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

    /// <summary>The local sync folders under this account (one per sync context).</summary>
    public IReadOnlyList<SyncFolderViewModel> Folders { get; }

    /// <summary>Initializes a new <see cref="AccountViewModel"/> from a <see cref="SyncContextRegistration"/>.</summary>
    public AccountViewModel(SyncContextRegistration registration)
    {
        ContextId = registration.Id;
        DisplayName = registration.DisplayName;
        ServerBaseUrl = registration.ServerBaseUrl;
        LocalFolderPath = registration.LocalFolderPath;
        _state = "Idle";
        Folders = [new SyncFolderViewModel(registration)];
    }
}

/// <summary>
/// View-model for a single local sync folder shown under an account.
/// </summary>
public sealed class SyncFolderViewModel : ViewModelBase
{
    /// <summary>Unique context identifier (matches the sync context ID).</summary>
    public Guid ContextId { get; }

    /// <summary>Absolute local sync folder path.</summary>
    public string LocalFolderPath { get; }

    /// <summary>Remote folder display path, or <c>"Whole account"</c> when not scoped.</summary>
    public string RemoteFolderPath { get; }

    /// <summary>Initializes a new <see cref="SyncFolderViewModel"/> from a <see cref="SyncContextRegistration"/>.</summary>
    public SyncFolderViewModel(SyncContextRegistration registration)
    {
        ContextId = registration.Id;
        LocalFolderPath = registration.LocalFolderPath;
        RemoteFolderPath = string.IsNullOrWhiteSpace(registration.ServerFolderDisplayPath)
            ? "Whole account"
            : registration.ServerFolderDisplayPath;
    }
}
