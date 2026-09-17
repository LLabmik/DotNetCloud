using System.Collections.Concurrent;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Shared per-user library scan progress tracker for the Photos module.
/// Mirrors <c>ScanProgressState</c> (Music) and <c>VideoScanProgressState</c> (Video) so the
/// gallery import prompt can render live scan progress while a scan is running.
/// </summary>
public sealed class PhotosScanProgressState
{
    private readonly ConcurrentDictionary<Guid, UserScanState> _states = new();

    /// <summary>
    /// Raised when any user's scan progress is updated or scanning state changes.
    /// </summary>
    public event Action? OnProgressChanged;

    /// <summary>
    /// Starts or replaces the active operation for a user.
    /// </summary>
    public CancellationTokenSource StartScan(Guid userId)
    {
        var state = _states.AddOrUpdate(
            userId,
            _ => new UserScanState(),
            (_, existing) => existing);

        CancellationTokenSource cancellationSource;
        lock (state.SyncRoot)
        {
            state.CancellationSource?.Dispose();
            cancellationSource = new CancellationTokenSource();
            state.CancellationSource = cancellationSource;
            state.IsScanning = true;
            state.CurrentProgress = null;
        }

        OnProgressChanged?.Invoke();
        return cancellationSource;
    }

    /// <summary>
    /// Gets whether the specified user has an active scan.
    /// </summary>
    public bool IsScanning(Guid userId)
    {
        if (!_states.TryGetValue(userId, out var state))
            return false;

        lock (state.SyncRoot)
        {
            return state.IsScanning;
        }
    }

    /// <summary>
    /// Gets the latest progress snapshot for a user's active scan.
    /// </summary>
    public LibraryScanProgress? GetCurrentProgress(Guid userId)
    {
        if (!_states.TryGetValue(userId, out var state))
            return null;

        lock (state.SyncRoot)
        {
            return state.CurrentProgress;
        }
    }

    /// <summary>
    /// Updates the current progress snapshot for a user's scan.
    /// Does NOT change <see cref="UserScanState.IsScanning"/> — that is managed
    /// solely by <see cref="StartScan"/> and <see cref="CompleteScan"/>.
    /// </summary>
    public void UpdateProgress(Guid userId, LibraryScanProgress progress)
    {
        var state = _states.GetOrAdd(userId, _ => new UserScanState());
        lock (state.SyncRoot)
        {
            state.CurrentProgress = progress;
        }

        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Cancels the current scan for a user, if one is active.
    /// </summary>
    public void Cancel(Guid userId)
    {
        if (!_states.TryGetValue(userId, out var state))
            return;

        CancellationTokenSource? cancellationSource;
        lock (state.SyncRoot)
        {
            cancellationSource = state.CancellationSource;
        }

        cancellationSource?.Cancel();
    }

    /// <summary>
    /// Gets the current cancellation token for a user's scan, if any.
    /// </summary>
    public CancellationToken GetCancellationToken(Guid userId)
    {
        if (!_states.TryGetValue(userId, out var state))
            return CancellationToken.None;

        lock (state.SyncRoot)
        {
            return state.CancellationSource?.Token ?? CancellationToken.None;
        }
    }

    /// <summary>
    /// Marks a user's scan as complete and releases its cancellation source.
    /// </summary>
    public void CompleteScan(Guid userId)
    {
        if (!_states.TryGetValue(userId, out var state))
            return;

        CancellationTokenSource? cancellationSource;
        lock (state.SyncRoot)
        {
            state.IsScanning = false;
            cancellationSource = state.CancellationSource;
            state.CancellationSource = null;
        }

        cancellationSource?.Dispose();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Creates an <see cref="IProgress{LibraryScanProgress}"/> that routes reports for a user.
    /// </summary>
    public IProgress<LibraryScanProgress> CreateProgressReporter(Guid userId)
    {
        return new Progress<LibraryScanProgress>(progress => UpdateProgress(userId, progress));
    }

    private sealed class UserScanState
    {
        public object SyncRoot { get; } = new();
        public bool IsScanning { get; set; }
        public LibraryScanProgress? CurrentProgress { get; set; }
        public CancellationTokenSource? CancellationSource { get; set; }
    }
}
