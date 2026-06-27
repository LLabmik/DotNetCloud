using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using DotNetCloud.Client.Core.Sync;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the <see cref="Views.SyncProgressWindow"/> that wraps
/// <see cref="TrayViewModel"/> to expose active transfers, pending counts,
/// sync state messages, error/conflict banners, session info, and pause/resume.
/// </summary>
public sealed class SyncProgressViewModel : ViewModelBase, IDisposable
{
    private readonly TrayViewModel _trayVm;
    private bool _isFullSync;
    private string? _fullSyncPhaseLabel;
    private int _fullSyncCompletedItems;
    private int _fullSyncTotalItems;
    private string _statusMessage = "Everything is up to date.";
    private string _statusSubMessage = string.Empty;
    private string _statusGlyph = "✓";
    private long _sessionBytesUploaded;
    private long _sessionBytesDownloaded;
    private string _lastSyncedAtText = string.Empty;
    private string _errorMessage = string.Empty;

    /// <summary>Initializes a new <see cref="SyncProgressViewModel"/>.</summary>
    public SyncProgressViewModel(
        TrayViewModel trayVm,
        Action? onOpenSettings = null,
        Action? onOpenConflicts = null)
    {
        _trayVm = trayVm;
        _onOpenSettings = onOpenSettings;
        _onOpenConflicts = onOpenConflicts;

        _trayVm.PropertyChanged += OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged += OnActiveTransfersChanged;
        _trayVm.SyncStatusUpdated += OnSyncStatusUpdated;
        _trayVm.SyncErrorRaised += OnSyncError;

        OpenSettingsCommand = new RelayCommand(OnOpenSettings);
        OpenConflictsCommand = new RelayCommand(OnOpenConflicts);
        PauseResumeCommand = new AsyncRelayCommand(OnPauseResumeAsync);

        UpdateDerivedProperties();
    }

    private readonly Action? _onOpenSettings;
    private readonly Action? _onOpenConflicts;

    // ── Transfer / pending items (unchanged from original) ─────────────────

    /// <summary>Active and recently completed file transfers.</summary>
    public ObservableCollection<ActiveTransferViewModel> ActiveTransfers => _trayVm.ActiveTransfers;

    /// <summary>Whether there are active (in-progress or recently completed) transfers.</summary>
    public bool HasActiveTransfers => _trayVm.ActiveTransfers.Count > 0;

    /// <summary>Total pending upload count across all accounts.</summary>
    public int TotalPendingUploads => _trayVm.Accounts.Sum(a => a.PendingUploads);

    /// <summary>Total pending download count across all accounts.</summary>
    public int TotalPendingDownloads => _trayVm.Accounts.Sum(a => a.PendingDownloads);

    /// <summary>Whether there are any pending items (uploads or downloads) queued.</summary>
    public bool HasPendingItems => TotalPendingUploads > 0 || TotalPendingDownloads > 0;

    // ── Header summary ────────────────────────────────────────────────────

    /// <summary>Summary text for the header (e.g. "3 files syncing", "Preparing…", "Up to date").</summary>
    public string SyncSummary
    {
        get
        {
            if (_isFullSync)
                return _fullSyncPhaseLabel ?? "Full sync in progress…";

            var active = _trayVm.ActiveTransfers.Count(t => !t.IsComplete);
            if (active == 0)
            {
                if (_trayVm.OverallState == TrayState.Syncing)
                {
                    if (!string.IsNullOrEmpty(_fullSyncPhaseLabel))
                        return _fullSyncPhaseLabel;
                    return "Preparing…";
                }
                return _trayVm.IsSyncing ? "Preparing…" : "Up to date";
            }

            return active == 1 ? "1 file syncing" : $"{active} files syncing";
        }
    }

    // ── Full-sync progress (unchanged) ────────────────────────────────────

    /// <summary>Whether a full re-sync is currently in progress.</summary>
    public bool IsFullSync
    {
        get => _isFullSync;
        private set
        {
            _isFullSync = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FullSyncProgressPercent));
        }
    }

    /// <summary>Human-readable phase label for the current full-sync operation.</summary>
    public string? FullSyncPhaseLabel
    {
        get => _fullSyncPhaseLabel;
        private set
        {
            _fullSyncPhaseLabel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Number of items completed in the current full-sync phase.</summary>
    public int FullSyncCompletedItems
    {
        get => _fullSyncCompletedItems;
        private set
        {
            _fullSyncCompletedItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FullSyncProgressPercent));
        }
    }

    /// <summary>Total number of items to process in the current full-sync phase.</summary>
    public int FullSyncTotalItems
    {
        get => _fullSyncTotalItems;
        private set
        {
            _fullSyncTotalItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FullSyncProgressPercent));
        }
    }

    /// <summary>Progress percentage for the current full-sync phase (0-100).</summary>
    public double FullSyncProgressPercent
    {
        get
        {
            if (_fullSyncTotalItems <= 0)
                return _isFullSync ? 0 : 100;
            return Math.Round((double)_fullSyncCompletedItems / _fullSyncTotalItems * 100, 1);
        }
    }

    /// <summary>Whether the full-sync progress bar should be shown.</summary>
    public bool ShowFullSyncProgress => _isFullSync;

    /// <summary>Formatted progress text for the full-sync status (e.g. "12 of 45 files").</summary>
    public string FullSyncProgressText
    {
        get
        {
            if (_fullSyncTotalItems <= 0)
                return "";
            return $"{_fullSyncCompletedItems} of {_fullSyncTotalItems} files";
        }
    }

    // ── State-aware status message (replaces hardcoded empty state text) ──

    /// <summary>Primary status message shown in the center of the dialog.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Secondary status detail shown below the primary message.</summary>
    public string StatusSubMessage
    {
        get => _statusSubMessage;
        private set => SetProperty(ref _statusSubMessage, value);
    }

    /// <summary>Large Unicode glyph representing the current state.</summary>
    public string StatusGlyph
    {
        get => _statusGlyph;
        private set => SetProperty(ref _statusGlyph, value);
    }

    // ── Error / conflict banner ───────────────────────────────────────────

    /// <summary>Whether the error banner should be visible.</summary>
    public bool HasErrors => _trayVm.OverallState == TrayState.Error;

    /// <summary>Whether the conflict banner should be visible.</summary>
    public bool HasConflicts => _trayVm.ConflictCount > 0;

    /// <summary>Whether any banner (error or conflict) is visible.</summary>
    public bool IsBannerVisible => HasErrors || HasConflicts;

    /// <summary>Text to display inside the banner.</summary>
    public string BannerMessage
    {
        get
        {
            if (HasErrors && !string.IsNullOrEmpty(_errorMessage))
                return $"Sync error: {_errorMessage}";
            if (HasErrors)
                return "Sync error";
            if (HasConflicts)
            {
                var count = _trayVm.ConflictCount;
                return count == 1
                    ? "1 conflict needs attention"
                    : $"{count} conflicts need attention";
            }
            return string.Empty;
        }
    }

    /// <summary>Whether the banner shows an error (vs. conflict) — drives styling.</summary>
    public bool IsErrorBanner => HasErrors;

    /// <summary>Whether the banner shows a conflict — drives styling.</summary>
    public bool IsConflictBanner => HasConflicts && !HasErrors;

    // ── Footer info ───────────────────────────────────────────────────────

    /// <summary>Formatted footer string combining last-synced and session transfer data.</summary>
    public string FooterText
    {
        get
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(_lastSyncedAtText))
                parts.Add($"Last synced: {_lastSyncedAtText}");

            if (_sessionBytesUploaded > 0 || _sessionBytesDownloaded > 0)
            {
                var sessionText = $"↑ {FormatBytes(_sessionBytesUploaded)} · ↓ {FormatBytes(_sessionBytesDownloaded)}";
                parts.Add(sessionText);
            }

            return parts.Count > 0 ? string.Join("  ·  ", parts) : string.Empty;
        }
    }

    /// <summary>Whether the pause/resume button should be shown.</summary>
    public bool ShowPauseResume => true;

    /// <summary>Text for the pause/resume button.</summary>
    public string PauseResumeText => _trayVm.IsPaused ? "Resume" : "Pause";

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Opens the Settings window (for error details).</summary>
    public ICommand OpenSettingsCommand { get; }

    /// <summary>Opens the Settings window to the Conflicts tab.</summary>
    public ICommand OpenConflictsCommand { get; }

    /// <summary>Toggles between pause and resume.</summary>
    public ICommand PauseResumeCommand { get; }

    // ── SyncStatus update callback ────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="TrayIconManager"/> when a <see cref="SyncProgress"/> event
    /// arrives from the sync engine, carrying the full <see cref="SyncStatus"/>.
    /// </summary>
    public void OnSyncStatusUpdated(SyncStatus status)
    {
        // Forward phase label for both full and regular sync cycles.
        if (!string.IsNullOrEmpty(status.FullSyncPhaseLabel))
        {
            FullSyncPhaseLabel = status.FullSyncPhaseLabel;
        }

        // Track session bytes (accumulated over the sync pass).
        if (status.BytesUploaded > 0)
            _sessionBytesUploaded = status.BytesUploaded;
        if (status.BytesDownloaded > 0)
            _sessionBytesDownloaded = status.BytesDownloaded;

        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(SyncSummary));
        RefreshStatusMessage();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void OnTrayVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TrayViewModel.IsSyncing):
            case nameof(TrayViewModel.OverallState):
            case nameof(TrayViewModel.ConflictCount):
            case nameof(TrayViewModel.IsPaused):
                UpdateDerivedProperties();
                break;
        }
    }

    private void OnActiveTransfersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDerivedProperties();
    }

    private void UpdateDerivedProperties()
    {
        OnPropertyChanged(nameof(HasActiveTransfers));
        OnPropertyChanged(nameof(TotalPendingUploads));
        OnPropertyChanged(nameof(TotalPendingDownloads));
        OnPropertyChanged(nameof(HasPendingItems));
        OnPropertyChanged(nameof(SyncSummary));
        RefreshStatusMessage();
    }

    internal void RefreshStatusMessage()
    {
        var state = _trayVm.OverallState;
        var hasTransfers = HasActiveTransfers;

        // When transfers are present, keep the default transfer list visible.
        if (hasTransfers)
        {
            StatusMessage = string.Empty;
            StatusSubMessage = string.Empty;
            return;
        }

        switch (state)
        {
            case TrayState.Idle:
                StatusGlyph = "✓";
                StatusMessage = "Everything is up to date.";
                StatusSubMessage = ComputeLastSyncedText();
                break;

            case TrayState.Syncing:
                StatusGlyph = "⟳";
                StatusMessage = !string.IsNullOrEmpty(_fullSyncPhaseLabel)
                    ? _fullSyncPhaseLabel
                    : "Preparing to sync…";
                StatusSubMessage = "Scanning for changes…";
                break;

            case TrayState.Error:
                StatusGlyph = "✗";
                StatusMessage = "Sync error";
                StatusSubMessage = string.IsNullOrEmpty(_errorMessage)
                    ? "An error occurred during sync"
                    : _errorMessage;
                break;

            case TrayState.Conflict:
                StatusGlyph = "⚠";
                var c = _trayVm.ConflictCount;
                StatusMessage = "Conflicts need attention";
                StatusSubMessage = c == 1
                    ? "1 unresolved conflict"
                    : $"{c} unresolved conflicts";
                break;

            case TrayState.Paused:
                StatusGlyph = "⏸";
                StatusMessage = "Sync paused";
                StatusSubMessage = "Resume sync to continue";
                break;

            case TrayState.Offline:
                StatusGlyph = "○";
                StatusMessage = "Offline";
                StatusSubMessage = "Waiting to reconnect…";
                break;

            default:
                StatusGlyph = "✓";
                StatusMessage = "Everything is up to date.";
                StatusSubMessage = string.Empty;
                break;
        }

        // Update banner and footer properties.
        UpdateBannerProperties();
        OnPropertyChanged(nameof(PauseResumeText));
        OnPropertyChanged(nameof(FooterText));
    }

    private void UpdateBannerProperties()
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(IsBannerVisible));
        OnPropertyChanged(nameof(BannerMessage));
        OnPropertyChanged(nameof(IsErrorBanner));
        OnPropertyChanged(nameof(IsConflictBanner));
    }

    private string ComputeLastSyncedText()
    {
        var lastSynced = _trayVm.Accounts
            .Select(a => a.LastSyncedAt)
            .Where(d => d.HasValue)
            .DefaultIfEmpty()
            .Max();

        if (lastSynced is null)
            return string.Empty;

        var elapsed = DateTime.UtcNow - lastSynced.Value;
        if (elapsed.TotalMinutes < 1)
            return "just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays}d ago";
        return lastSynced.Value.ToLocalTime().ToString("MMM dd, HH:mm");
    }

    private void OnOpenSettings()
    {
        _onOpenSettings?.Invoke();
    }

    private void OnOpenConflicts()
    {
        _onOpenConflicts?.Invoke();
    }

    private async Task OnPauseResumeAsync()
    {
        if (_trayVm.IsPaused)
            await _trayVm.ResumeAllAsync();
        else
            await _trayVm.PauseAllAsync();

        OnPropertyChanged(nameof(PauseResumeText));
        RefreshStatusMessage();
    }

    /// <summary>
    /// Updates the full-sync progress from the engine's status.
    /// Called by the parent view when a <see cref="SyncStatus"/> update arrives
    /// with <see cref="SyncStatus.IsFullSync"/> set to <see langword="true"/>.
    /// </summary>
    public void UpdateFullSyncProgress(bool isFullSync, string? phaseLabel, int completedItems, int totalItems)
    {
        IsFullSync = isFullSync;
        FullSyncPhaseLabel = phaseLabel;
        FullSyncCompletedItems = completedItems;
        FullSyncTotalItems = totalItems;
        OnPropertyChanged(nameof(FullSyncProgressPercent));
        OnPropertyChanged(nameof(FullSyncProgressText));
        OnPropertyChanged(nameof(ShowFullSyncProgress));
        OnPropertyChanged(nameof(SyncSummary));
    }

    /// <summary>
    /// Receives sync error messages from the engine to display in the error banner.
    /// </summary>
    public void OnSyncError(string errorMessage)
    {
        _errorMessage = errorMessage;
        RefreshStatusMessage();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / 1024.0 / 1024.0:F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F0} KB";
        return bytes == 0 ? "0 B" : $"{bytes} B";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _trayVm.PropertyChanged -= OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged -= OnActiveTransfersChanged;
        _trayVm.SyncStatusUpdated -= OnSyncStatusUpdated;
        _trayVm.SyncErrorRaised -= OnSyncError;
    }
}
