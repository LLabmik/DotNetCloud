using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the <see cref="Views.SyncProgressWindow"/> that wraps
/// <see cref="TrayViewModel"/> to expose active transfers, pending counts,
/// and a sync summary string.
/// </summary>
public sealed class SyncProgressViewModel : ViewModelBase, IDisposable
{
    private readonly TrayViewModel _trayVm;
    private bool _isFullSync;
    private string? _fullSyncPhaseLabel;
    private int _fullSyncCompletedItems;
    private int _fullSyncTotalItems;

    /// <summary>Initializes a new <see cref="SyncProgressViewModel"/>.</summary>
    public SyncProgressViewModel(TrayViewModel trayVm)
    {
        _trayVm = trayVm;
        _trayVm.PropertyChanged += OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged += OnActiveTransfersChanged;

        UpdateDerivedProperties();
    }

    /// <summary>Active and recently completed file transfers.</summary>
    public ObservableCollection<ActiveTransferViewModel> ActiveTransfers => _trayVm.ActiveTransfers;

    /// <summary>Whether there are active (in-progress or recently completed) transfers.</summary>
    public bool HasActiveTransfers => _trayVm.ActiveTransfers.Count > 0;

    /// <summary>Total pending upload count across all accounts.</summary>
    public int TotalPendingUploads
    {
        get => _trayVm.Accounts.Sum(a => a.PendingUploads);
    }

    /// <summary>Total pending download count across all accounts.</summary>
    public int TotalPendingDownloads
    {
        get => _trayVm.Accounts.Sum(a => a.PendingDownloads);
    }

    /// <summary>Whether there are any pending items (uploads or downloads) queued.</summary>
    public bool HasPendingItems => TotalPendingUploads > 0 || TotalPendingDownloads > 0;

    /// <summary>Summary text for the header (e.g. "3 files syncing").</summary>
    public string SyncSummary
    {
        get
        {
            if (_isFullSync)
            {
                return _fullSyncPhaseLabel ?? "Full sync in progress…";
            }

            var active = _trayVm.ActiveTransfers.Count(t => !t.IsComplete);
            if (active == 0)
            {
                return _trayVm.IsSyncing ? "Preparing…" : "Up to date";
            }

            return active == 1 ? "1 file syncing" : $"{active} files syncing";
        }
    }

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

    private void OnTrayVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrayViewModel.IsSyncing)
            or nameof(TrayViewModel.OverallState))
        {
            UpdateDerivedProperties();
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

    /// <inheritdoc />
    public void Dispose()
    {
        _trayVm.PropertyChanged -= OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged -= OnActiveTransfersChanged;
    }
}
