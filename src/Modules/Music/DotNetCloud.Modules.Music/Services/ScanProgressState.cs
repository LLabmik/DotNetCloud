using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Scoped state service that bridges <see cref="IProgress{LibraryScanProgress}"/> callbacks
/// to Blazor's <c>StateHasChanged()</c> pattern. One instance per Blazor circuit/tab.
/// </summary>
public sealed class ScanProgressState
{
    private LibraryScanProgress? _currentProgress;

    /// <summary>Whether a library scan is currently in progress.</summary>
    public bool IsScanning { get; private set; }

    /// <summary>The latest progress snapshot from the running scan, or null if no scan is active.</summary>
    public LibraryScanProgress? CurrentProgress => _currentProgress;

    /// <summary>
    /// Raised when scan progress is updated or scanning state changes.
    /// Subscribe to this in Blazor components to call <c>StateHasChanged()</c>.
    /// </summary>
    public event Action? OnProgressChanged;

    /// <summary>
    /// Marks the beginning of a library scan.
    /// </summary>
    public void StartScan()
    {
        IsScanning = true;
        _currentProgress = null;
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Updates the current scan progress. Typically called from an <see cref="IProgress{T}"/> callback.
    /// </summary>
    /// <param name="progress">Latest progress snapshot.</param>
    public void UpdateProgress(LibraryScanProgress progress)
    {
        _currentProgress = progress;
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Marks the scan as complete and clears scanning state.
    /// </summary>
    public void CompleteScan()
    {
        IsScanning = false;
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Creates an <see cref="IProgress{LibraryScanProgress}"/> that routes reports to <see cref="UpdateProgress"/>.
    /// </summary>
    public IProgress<LibraryScanProgress> CreateProgressReporter()
    {
        return new Progress<LibraryScanProgress>(UpdateProgress);
    }
}
