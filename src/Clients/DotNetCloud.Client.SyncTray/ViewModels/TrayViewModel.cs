using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the tray icon.  Aggregates sync status across all connected
/// accounts and exposes the properties that drive the tray icon, tooltip, and
/// context menu.
/// </summary>
public sealed class TrayViewModel : ViewModelBase
{
    private readonly IIpcClient _ipc;
    private readonly INotificationService _notifications;
    private readonly ILogger<TrayViewModel> _logger;

    private TrayState _overallState = TrayState.Offline;
    private string _tooltip = "DotNetCloud Sync \u2014 service not running";
    private bool _isSyncing;
    private bool _isPaused;

    // Keyed by context ID for O(1) lookup on push events.
    private readonly Dictionary<Guid, AccountViewModel> _accounts = [];
    private readonly List<AccountViewModel> _accountList = [];

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Aggregate tray state computed from all sync contexts.</summary>
    public TrayState OverallState
    {
        get => _overallState;
        private set => SetProperty(ref _overallState, value);
    }

    /// <summary>Tooltip text shown on tray icon hover.</summary>
    public string Tooltip
    {
        get => _tooltip;
        private set => SetProperty(ref _tooltip, value);
    }

    /// <summary>Whether any context is currently syncing.</summary>
    public bool IsSyncing
    {
        get => _isSyncing;
        private set => SetProperty(ref _isSyncing, value);
    }

    /// <summary>Whether all contexts are paused.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    /// <summary>Snapshot list of connected account view-models.</summary>
    public IReadOnlyList<AccountViewModel> Accounts => _accountList;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="TrayViewModel"/>.</summary>
    public TrayViewModel(IIpcClient ipc, INotificationService notifications, ILogger<TrayViewModel> logger)
    {
        _ipc = ipc;
        _notifications = notifications;
        _logger = logger;

        _ipc.ConnectionStateChanged += OnConnectionStateChanged;
        _ipc.SyncProgressReceived += OnSyncProgress;
        _ipc.SyncCompleteReceived += OnSyncComplete;
        _ipc.SyncErrorReceived += OnSyncError;
        _ipc.ConflictDetected += OnConflictDetected;
    }

    // ── Commands (called from TrayIconManager / menu) ─────────────────────

    /// <summary>Refreshes account list from the IPC service.</summary>
    public async Task RefreshAccountsAsync()
    {
        try
        {
            var contexts = await _ipc.ListContextsAsync();
            UpdateAccounts(contexts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh accounts from SyncService.");
        }
    }

    /// <summary>Triggers an immediate sync for all accounts.</summary>
    public async Task SyncNowAllAsync()
    {
        foreach (var account in _accountList)
        {
            try
            {
                await _ipc.SyncNowAsync(account.ContextId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SyncNow failed for context {Id}.", account.ContextId);
            }
        }
    }

    /// <summary>Triggers an immediate sync for a specific context.</summary>
    public Task SyncNowAsync(Guid contextId) => _ipc.SyncNowAsync(contextId);

    /// <summary>Pauses sync for all accounts.</summary>
    public async Task PauseAllAsync()
    {
        foreach (var account in _accountList)
        {
            try
            {
                await _ipc.PauseAsync(account.ContextId);
                account.State = "Paused";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pause failed for context {Id}.", account.ContextId);
            }
        }

        UpdateAggregateState();
    }

    /// <summary>Resumes sync for all paused accounts.</summary>
    public async Task ResumeAllAsync()
    {
        foreach (var account in _accountList)
        {
            try
            {
                await _ipc.ResumeAsync(account.ContextId);
                account.State = "Idle";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Resume failed for context {Id}.", account.ContextId);
            }
        }

        UpdateAggregateState();
    }

    /// <summary>Removes an account and its sync context.</summary>
    public async Task RemoveAccountAsync(Guid contextId)
    {
        try
        {
            await _ipc.RemoveAccountAsync(contextId);
            if (_accounts.Remove(contextId, out var vm))
            {
                _accountList.Remove(vm);
                UpdateAggregateState();
                OnPropertyChanged(nameof(Accounts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove account {Id}.", contextId);
        }
    }

    // ── IPC event handlers ────────────────────────────────────────────────
    // NOTE: These handlers run on the thread that raises the event (IPC reader
    // thread or test thread).  Avalonia's property-binding infrastructure handles
    // cross-thread marshal automatically, so no manual Dispatcher.UIThread.Post
    // is required.

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (!connected)
        {
            _accounts.Clear();
            _accountList.Clear();
            OverallState = TrayState.Offline;
            Tooltip = "DotNetCloud Sync \u2014 service not running";
            OnPropertyChanged(nameof(Accounts));
            return;
        }

        // Connected — refresh accounts from the service (fire and forget; errors are logged).
        _ = Task.Run(async () =>
        {
            await RefreshAccountsAsync();
            UpdateAggregateState();
        });
    }

    private void OnSyncProgress(object? sender, SyncProgressEventData e)
    {
        if (_accounts.TryGetValue(e.ContextId, out var vm))
        {
            vm.State = e.State;
            vm.PendingUploads = e.PendingUploads;
            vm.PendingDownloads = e.PendingDownloads;
        }

        UpdateAggregateState();
    }

    private void OnSyncComplete(object? sender, SyncCompleteEventData e)
    {
        if (_accounts.TryGetValue(e.ContextId, out var vm))
        {
            vm.State = "Idle";
            vm.LastSyncedAt = e.LastSyncedAt;
            vm.PendingUploads = 0;
            vm.PendingDownloads = 0;

            if (e.Conflicts > 0)
            {
                _notifications.ShowNotification(
                    "Sync conflict detected",
                    $"{e.Conflicts} conflict(s) in {vm.DisplayName}. Conflict copies have been created.",
                    NotificationType.Warning);
            }
        }

        UpdateAggregateState();
    }

    private void OnSyncError(object? sender, SyncErrorEventData e)
    {
        if (_accounts.TryGetValue(e.ContextId, out var vm))
        {
            vm.State = "Error";
            vm.LastError = e.Error;

            _notifications.ShowNotification(
                "Sync error",
                $"{vm.DisplayName}: {e.Error}",
                NotificationType.Error);
        }

        UpdateAggregateState();
    }

    private void OnConflictDetected(object? sender, SyncConflictEventData e)
    {
        var fileName = Path.GetFileName(e.OriginalPath);
        _notifications.ShowNotification(
            "File conflict",
            $"Conflict in \"{fileName}\". A conflict copy was saved.",
            NotificationType.Warning);
    }

    // ── Aggregate state computation ───────────────────────────────────────

    private void UpdateAccounts(IReadOnlyList<DotNetCloud.Client.SyncService.Ipc.ContextInfo> contexts)
    {
        var seen = new HashSet<Guid>();

        foreach (var ctx in contexts)
        {
            seen.Add(ctx.Id);
            if (_accounts.TryGetValue(ctx.Id, out var vm))
            {
                vm.UpdateFrom(ctx);
            }
            else
            {
                var newVm = new AccountViewModel(ctx);
                _accounts[ctx.Id] = newVm;
                _accountList.Add(newVm);
            }
        }

        // Remove accounts no longer present.
        var removed = _accounts.Keys.Except(seen).ToList();
        foreach (var id in removed)
        {
            if (_accounts.Remove(id, out var vm))
                _accountList.Remove(vm);
        }

        OnPropertyChanged(nameof(Accounts));
        UpdateAggregateState();
    }

    private void UpdateAggregateState()
    {
        if (!_ipc.IsConnected || _accountList.Count == 0)
        {
            OverallState = TrayState.Offline;
            Tooltip = _ipc.IsConnected
                ? "DotNetCloud Sync \u2014 no accounts configured"
                : "DotNetCloud Sync \u2014 service not running";
            IsSyncing = false;
            IsPaused = false;
            return;
        }

        bool hasError = _accountList.Any(a => a.State == "Error");
        bool isSyncing = _accountList.Any(a => a.State == "Syncing");
        bool allPaused = _accountList.All(a => a.State == "Paused");

        OverallState = hasError ? TrayState.Error
            : isSyncing ? TrayState.Syncing
            : allPaused ? TrayState.Paused
            : TrayState.Idle;

        IsSyncing = isSyncing;
        IsPaused = allPaused;

        int totalUp = _accountList.Sum(a => a.PendingUploads);
        int totalDown = _accountList.Sum(a => a.PendingDownloads);

        Tooltip = OverallState switch
        {
            TrayState.Error => "DotNetCloud Sync \u2014 sync error (click for details)",
            TrayState.Syncing => $"DotNetCloud Sync \u2014 syncing ({totalUp} \u2191  {totalDown} \u2193)",
            TrayState.Paused => "DotNetCloud Sync \u2014 paused",
            _ => $"DotNetCloud Sync \u2014 up to date ({_accountList.Count} account(s))",
        };
    }
}

/// <summary>Overall tray icon state, derived from all sync contexts.</summary>
public enum TrayState
{
    /// <summary>All contexts synced; no issues.</summary>
    Idle,

    /// <summary>One or more contexts are actively syncing.</summary>
    Syncing,

    /// <summary>All contexts are paused.</summary>
    Paused,

    /// <summary>One or more contexts have encountered an error.</summary>
    Error,

    /// <summary>SyncService is unreachable or no accounts are configured.</summary>
    Offline,
}
