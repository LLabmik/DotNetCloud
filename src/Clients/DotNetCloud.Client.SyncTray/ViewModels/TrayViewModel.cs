using System.Collections.ObjectModel;
using DotNetCloud.Client.Core;
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
    private readonly IChatSignalRClient _chatSignalRClient;
    private readonly INotificationService _notifications;
    private readonly ILogger<TrayViewModel> _logger;

    private TrayState _overallState = TrayState.Offline;
    private string _tooltip = "DotNetCloud Sync \u2014 service not running";
    private bool _isSyncing;
    private bool _isPaused;
    private int _conflictCount;
    private int _chatUnreadCount;
    private bool _chatHasMentions;
    private bool _isMuteChatNotifications;

    // Keyed by context ID for O(1) lookup on push events.
    private readonly Dictionary<Guid, AccountViewModel> _accounts = [];
    private readonly List<AccountViewModel> _accountList = [];

    // Active transfers: keyed by TransferKey (contextId:fileName:direction) for O(1) update.
    private readonly Dictionary<string, ActiveTransferViewModel> _transfersById = [];
    private readonly ObservableCollection<ActiveTransferViewModel> _activeTransfers = [];

    // Chat unread aggregation keyed by channel ID.
    private readonly Dictionary<string, ChatUnreadCountUpdatedEventArgs> _chatUnreadByChannel =
        new(StringComparer.OrdinalIgnoreCase);

    // Channel display names keyed by channel ID, populated from incoming messages.
    private readonly Dictionary<string, string> _chatChannelNames =
        new(StringComparer.OrdinalIgnoreCase);

    // 24-hour recurring conflict reminder (Task 3.5c).
    private DateTime _lastConflictNotificationUtc = DateTime.MinValue;
    private Timer? _conflictReminderTimer;

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

    /// <summary>Total number of unresolved conflict copies requiring user attention.</summary>
    public int ConflictCount
    {
        get => _conflictCount;
        private set
        {
            if (SetProperty(ref _conflictCount, value))
                OnPropertyChanged(nameof(HasConflicts));
        }
    }

    /// <summary>Whether any unresolved conflict copies exist.</summary>
    public bool HasConflicts => _conflictCount > 0;

    /// <summary>Total unread chat messages across channels.</summary>
    public int ChatUnreadCount
    {
        get => _chatUnreadCount;
        private set => SetProperty(ref _chatUnreadCount, value);
    }

    /// <summary>Whether any channel currently has unread mentions.</summary>
    public bool ChatHasMentions
    {
        get => _chatHasMentions;
        private set => SetProperty(ref _chatHasMentions, value);
    }

    /// <summary>Whether chat popup notifications are muted.</summary>
    public bool IsMuteChatNotifications
    {
        get => _isMuteChatNotifications;
        set => SetProperty(ref _isMuteChatNotifications, value);
    }

    /// <summary>Snapshot list of connected account view-models.</summary>
    public IReadOnlyList<AccountViewModel> Accounts => _accountList;

    /// <summary>Observable list of active and recently completed file transfers.</summary>
    public ObservableCollection<ActiveTransferViewModel> ActiveTransfers => _activeTransfers;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user requests opening the quick-reply window for a channel.
    /// Arguments: (channelId, channelDisplayName, serverBaseUrl).
    /// </summary>
    internal event Action<string, string, string>? OpenQuickReplyRequested;

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="TrayViewModel"/>.</summary>
    public TrayViewModel(
        IIpcClient ipc,
        IChatSignalRClient chatSignalRClient,
        INotificationService notifications,
        ILogger<TrayViewModel> logger)
    {
        _ipc = ipc;
        _chatSignalRClient = chatSignalRClient;
        _notifications = notifications;
        _logger = logger;
        _notifications.OnNotificationActivated = OnNotificationActivated;

        _ipc.ConnectionStateChanged += OnConnectionStateChanged;
        _ipc.SyncProgressReceived += OnSyncProgress;
        _ipc.SyncCompleteReceived += OnSyncComplete;
        _ipc.SyncErrorReceived += OnSyncError;
        _ipc.ConflictDetected += OnConflictDetected;
        _ipc.ConflictAutoResolved += OnConflictAutoResolved;
        _ipc.TransferProgressReceived += OnTransferProgress;
        _ipc.TransferCompleteReceived += OnTransferComplete;
        _chatSignalRClient.OnUnreadCountUpdated += OnUnreadCountUpdated;
        _chatSignalRClient.OnNewChatMessage += OnNewChatMessage;

        // Start a 1-hour periodic timer to check for stale unresolved conflicts.
        _conflictReminderTimer = new Timer(
            _ => CheckConflictReminder(),
            state: null,
            dueTime: TimeSpan.FromHours(1),
            period: TimeSpan.FromHours(1));

        _ = Task.Run(async () =>
        {
            try
            {
                await _chatSignalRClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Chat SignalR client connection failed or is unavailable.");
            }
        });
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

    /// <summary>Opens the Conflicts panel in the Settings window.</summary>
    public async Task OpenConflictsAsync()
    {
        // Implementation wires to SettingsViewModel when available;
        // the event-based navigation is handled by the UI layer.
        await Task.CompletedTask;
    }

    /// <summary>Marks a conflict as resolved; decrements the conflict count.</summary>
    public void OnConflictResolved()
    {
        if (ConflictCount > 0)
        {
            ConflictCount--;
            UpdateAggregateState();
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
        ConflictCount++;
        UpdateAggregateState();
        _lastConflictNotificationUtc = DateTime.UtcNow;
        _notifications.ShowNotification(
            "File conflict",
            $"Conflict in \"{fileName}\". A conflict copy was saved.",
            NotificationType.Warning);
    }

    private void OnConflictAutoResolved(object? sender, ConflictAutoResolvedEventData e)
    {
        var fileName = Path.GetFileName(e.LocalPath ?? string.Empty);
        _notifications.ShowNotification(
            "Conflict auto-resolved",
            $"\"{fileName}\" was automatically resolved ({e.Resolution ?? e.Strategy ?? "no details"}).",
            NotificationType.Info);
    }

    private void OnUnreadCountUpdated(object? sender, ChatUnreadCountUpdatedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.ChannelId))
            return;

        if (e.UnreadCount <= 0)
        {
            _chatUnreadByChannel.Remove(e.ChannelId);
        }
        else
        {
            _chatUnreadByChannel[e.ChannelId] = e;
        }

        ChatUnreadCount = _chatUnreadByChannel.Values.Sum(v => Math.Max(0, v.UnreadCount));
        ChatHasMentions = _chatUnreadByChannel.Values.Any(v => v.HasMention);
        UpdateAggregateState();
    }

    private void OnNewChatMessage(object? sender, ChatMessageReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.ChannelId))
            return;

        // Track channel display name for use by quick-reply and notification activation.
        if (!string.IsNullOrWhiteSpace(e.ChannelDisplayName))
            _chatChannelNames[e.ChannelId] = e.ChannelDisplayName;

        if (IsMuteChatNotifications)
            return;

        var channelName = string.IsNullOrWhiteSpace(e.ChannelDisplayName) ? "Chat" : e.ChannelDisplayName;
        var senderName = string.IsNullOrWhiteSpace(e.SenderDisplayName) ? "Unknown sender" : e.SenderDisplayName;
        var preview = string.IsNullOrWhiteSpace(e.MessagePreview) ? "(no preview)" : e.MessagePreview;
        var notificationType = e.IsMention ? NotificationType.Mention : NotificationType.Chat;
        var actionUrl = GetChatChannelUrl(e.ChannelId);
        var channelKey = $"chat-channel-{e.ChannelId}";

        _notifications.ShowNotification(
            $"{channelName} — {senderName}",
            preview,
            notificationType,
            actionUrl,
            channelKey,
            channelKey);
    }

    private void OnNotificationActivated(string actionUrl)
    {
        _logger.LogDebug("Notification activated: {Url}", actionUrl);

        // Parse the channelId query parameter from the action URL and open quick reply.
        if (!Uri.TryCreate(actionUrl, UriKind.Absolute, out var uri))
            return;

        var channelId = ParseChannelIdFromQuery(uri.Query);
        if (string.IsNullOrWhiteSpace(channelId))
            return;

        var serverBaseUrl = _accountList.FirstOrDefault()?.ServerBaseUrl;
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return;

        _chatChannelNames.TryGetValue(channelId, out var channelName);
        OpenQuickReplyRequested?.Invoke(channelId, channelName ?? "Chat", serverBaseUrl);
    }

    private static string? ParseChannelIdFromQuery(string query)
    {
        // uri.Query includes the leading '?'; strip it.
        var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var eqIdx = part.IndexOf('=');
            if (eqIdx < 0)
                continue;
            var key = part[..eqIdx];
            if (key.Equals("channelId", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(part[(eqIdx + 1)..]);
        }
        return null;
    }

    private string? GetChatChannelUrl(string channelId)
    {
        var serverBaseUrl = _accountList.FirstOrDefault()?.ServerBaseUrl;
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return null;

        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var baseUri))
            return null;

        return new Uri(baseUri, $"/apps/chat?channelId={Uri.EscapeDataString(channelId)}").ToString();
    }

    /// <summary>
    /// Returns the channel ID with the highest unread count, or <c>null</c> when
    /// there are no unread channels.  Used by the tray menu quick-reply item.
    /// </summary>
    internal string? GetMostRecentChannelId()
    {
        if (_chatUnreadByChannel.Count == 0)
            return null;

        // Prefer channels with mentions; fall back to highest unread count.
        var best = _chatUnreadByChannel.Values
            .OrderByDescending(v => v.HasMention ? 1 : 0)
            .ThenByDescending(v => v.UnreadCount)
            .FirstOrDefault();

        return best?.ChannelId;
    }

    /// <summary>
    /// Returns the display name for the given channel, or <c>"Chat"</c> when
    /// no name has been received yet.
    /// </summary>
    internal string GetChannelDisplayName(string channelId)
        => _chatChannelNames.TryGetValue(channelId, out var name) ? name : "Chat";

    /// <summary>
    /// Periodic check: if unresolved conflicts exist and the last notification
    /// was more than 24 hours ago, re-notify the user.
    /// </summary>
    private void CheckConflictReminder()
    {
        if (ConflictCount <= 0)
            return;

        var elapsed = DateTime.UtcNow - _lastConflictNotificationUtc;
        if (elapsed < TimeSpan.FromHours(24))
            return;

        _lastConflictNotificationUtc = DateTime.UtcNow;
        _notifications.ShowNotification(
            "Unresolved conflicts",
            $"You have {ConflictCount} unresolved conflict(s) that need attention.",
            NotificationType.Warning);
    }

    private void OnTransferProgress(object? sender, TransferProgressEventData e)
    {
        var key = $"{e.ContextId}:{e.FileName}:{e.Direction}";

        if (!_transfersById.TryGetValue(key, out var vm))
        {
            vm = new ActiveTransferViewModel(e.ContextId, e.FileName, e.Direction);
            _transfersById[key] = vm;
            _activeTransfers.Add(vm);
        }

        vm.Update(e.BytesTransferred, e.TotalBytes, e.ChunksCompleted, e.ChunksTotal, e.PercentComplete);
    }

    private void OnTransferComplete(object? sender, TransferCompleteEventData e)
    {
        var key = $"{e.ContextId}:{e.FileName}:{e.Direction}";

        if (!_transfersById.TryGetValue(key, out var vm))
        {
            // May arrive without prior progress event (small file, single chunk).
            vm = new ActiveTransferViewModel(e.ContextId, e.FileName, e.Direction);
            _transfersById[key] = vm;
            _activeTransfers.Add(vm);
        }

        vm.MarkComplete(e.TotalBytes);

        // Auto-dismiss after 5 seconds.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (_transfersById.Remove(key))
                _activeTransfers.Remove(vm);
        });
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
            var offlineTooltip = _ipc.IsConnected
                ? "DotNetCloud Sync \u2014 no accounts configured"
                : "DotNetCloud Sync \u2014 service not running";
            Tooltip = AppendChatSummary(offlineTooltip);
            IsSyncing = false;
            IsPaused = false;
            return;
        }

        bool hasError = _accountList.Any(a => a.State == "Error");
        bool isSyncing = _accountList.Any(a => a.State == "Syncing");
        bool allPaused = _accountList.All(a => a.State == "Paused");
        bool hasConflicts = _conflictCount > 0;

        OverallState = hasError ? TrayState.Error
            : hasConflicts ? TrayState.Conflict
            : isSyncing ? TrayState.Syncing
            : allPaused ? TrayState.Paused
            : TrayState.Idle;

        IsSyncing = isSyncing;
        IsPaused = allPaused;

        int totalUp = _accountList.Sum(a => a.PendingUploads);
        int totalDown = _accountList.Sum(a => a.PendingDownloads);

        var baseTooltip = OverallState switch
        {
            TrayState.Error => "DotNetCloud Sync \u2014 sync error (click for details)",
            TrayState.Conflict => $"DotNetCloud Sync \u2014 {_conflictCount} conflict(s) need attention",
            TrayState.Syncing => $"DotNetCloud Sync \u2014 syncing ({totalUp} \u2191  {totalDown} \u2193)",
            TrayState.Paused => "DotNetCloud Sync \u2014 paused",
            _ => $"DotNetCloud Sync \u2014 up to date ({_accountList.Count} account(s))",
        };

        Tooltip = AppendChatSummary(baseTooltip);
    }

    private string AppendChatSummary(string baseTooltip)
    {
        if (ChatUnreadCount <= 0)
            return baseTooltip;

        var mentionSuffix = ChatHasMentions ? " (mentions)" : string.Empty;
        return $"{baseTooltip} | chat: {ChatUnreadCount} unread{mentionSuffix}";
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

    /// <summary>One or more unresolved file conflicts require user attention.</summary>
    Conflict,

    /// <summary>SyncService is unreachable or no accounts are configured.</summary>
    Offline,
}
