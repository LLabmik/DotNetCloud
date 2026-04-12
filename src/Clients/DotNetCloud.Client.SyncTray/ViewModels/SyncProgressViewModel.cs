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
            var active = _trayVm.ActiveTransfers.Count(t => !t.IsComplete);
            if (active == 0)
            {
                return _trayVm.IsSyncing ? "Preparing…" : "Up to date";
            }

            return active == 1 ? "1 file syncing" : $"{active} files syncing";
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

    /// <inheritdoc />
    public void Dispose()
    {
        _trayVm.PropertyChanged -= OnTrayVmPropertyChanged;
        _trayVm.ActiveTransfers.CollectionChanged -= OnActiveTransfersChanged;
    }
}
