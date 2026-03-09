using System.Windows.Input;
using DotNetCloud.Client.SyncTray.Ipc;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for a single conflict record shown in the Conflicts tab.
/// </summary>
public sealed class ConflictViewModel : ViewModelBase
{
    private readonly IIpcClient _ipc;
    private readonly TrayViewModel _trayVm;
    private readonly ILogger _logger;

    private bool _isResolved;
    private string? _resolutionLabel;

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Database row ID.</summary>
    public int Id { get; }

    /// <summary>Context this conflict belongs to.</summary>
    public Guid ContextId { get; }

    /// <summary>Original (intended) local path of the conflicting file.</summary>
    public string OriginalPath { get; }

    /// <summary>File name portion of <see cref="OriginalPath"/>.</summary>
    public string FileName => Path.GetFileName(OriginalPath);

    /// <summary>Path to the conflict-copy file (empty when auto-resolved).</summary>
    public string ConflictCopyPath { get; }

    /// <summary><c>true</c> when a physical conflict copy exists.</summary>
    public bool HasConflictCopy => !string.IsNullOrEmpty(ConflictCopyPath);

    /// <summary>Local file modification time at conflict detection.</summary>
    public DateTime? LocalModifiedAt { get; }

    /// <summary>Server file modification time at conflict detection.</summary>
    public DateTime? RemoteModifiedAt { get; }

    /// <summary>UTC time the conflict was detected.</summary>
    public DateTime DetectedAt { get; }

    /// <summary>Whether the conflict has been resolved.</summary>
    public bool IsResolved
    {
        get => _isResolved;
        private set => SetProperty(ref _isResolved, value);
    }

    /// <summary>Human-readable resolution label (null when unresolved).</summary>
    public string? ResolutionLabel
    {
        get => _resolutionLabel;
        private set => SetProperty(ref _resolutionLabel, value);
    }

    /// <summary><c>true</c> when the conflict was resolved without user intervention.</summary>
    public bool AutoResolved { get; }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Keeps the local version; marks the conflict as resolved.</summary>
    public ICommand KeepLocalCommand { get; }

    /// <summary>Keeps the server version; marks the conflict as resolved.</summary>
    public ICommand KeepServerCommand { get; }

    /// <summary>Keeps both versions (no further action, conflict copy remains).</summary>
    public ICommand KeepBothCommand { get; }

    /// <summary>Opens the containing folder in the system file explorer.</summary>
    public ICommand OpenFolderCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initialises from an IPC conflict record snapshot.</summary>
    public ConflictViewModel(ConflictRecordData data, IIpcClient ipc, TrayViewModel trayVm, ILogger logger)
    {
        _ipc = ipc;
        _trayVm = trayVm;
        _logger = logger;

        Id = data.Id;
        ContextId = data.ContextId;
        OriginalPath = data.OriginalPath ?? string.Empty;
        ConflictCopyPath = data.ConflictCopyPath ?? string.Empty;
        LocalModifiedAt = data.LocalModifiedAt;
        RemoteModifiedAt = data.RemoteModifiedAt;
        DetectedAt = data.DetectedAt;
        AutoResolved = data.AutoResolved;
        IsResolved = data.IsResolved;
        ResolutionLabel = data.Resolution;

        KeepLocalCommand = new AsyncRelayCommand(
            () => IsResolved ? Task.CompletedTask : ResolveAsync("keep-local"));

        KeepServerCommand = new AsyncRelayCommand(
            () => IsResolved ? Task.CompletedTask : ResolveAsync("keep-server"));

        KeepBothCommand = new AsyncRelayCommand(
            () => IsResolved ? Task.CompletedTask : ResolveAsync("keep-both"));

        OpenFolderCommand = new RelayCommand(OpenContainingFolder);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task ResolveAsync(string resolution)
    {
        try
        {
            await _ipc.ResolveConflictAsync(ContextId, Id, resolution);
            ResolutionLabel = resolution;
            IsResolved = true;
            _trayVm.OnConflictResolved();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve conflict {Id}.", Id);
        }
    }

    private void OpenContainingFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(OriginalPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open folder for conflict {Id}.", Id);
        }
    }
}
