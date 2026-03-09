using Avalonia.Threading;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for a single active or recently completed file transfer shown in
/// the Transfers tab of the Settings window.
/// </summary>
public sealed class ActiveTransferViewModel : ViewModelBase
{
    private double _percentComplete;
    private long _bytesTransferred;
    private long _speedBytesPerSec;
    private string _eta = string.Empty;
    private bool _isComplete;

    // Speed calculation
    private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    private long _lastBytesTransferred;
    private DateTime _lastSpeedSample = DateTime.UtcNow;

    /// <summary>Unique key combining context ID + file name + direction for deduplication.</summary>
    public string Key { get; }

    /// <summary>File name (leaf only).</summary>
    public string FileName { get; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    public string Direction { get; }

    /// <summary>Direction indicator suitable for display (↑ upload, ↓ download).</summary>
    public string DirectionGlyph => Direction == "upload" ? "↑" : "↓";

    /// <summary>Total file size in bytes.</summary>
    public long TotalBytes { get; private set; }

    /// <summary>Bytes transferred so far.</summary>
    public long BytesTransferred
    {
        get => _bytesTransferred;
        private set => SetProperty(ref _bytesTransferred, value);
    }

    /// <summary>Percentage complete (0–100), clamped.</summary>
    public double PercentComplete
    {
        get => _percentComplete;
        private set => SetProperty(ref _percentComplete, Math.Clamp(value, 0, 100));
    }

    /// <summary>Current transfer speed in bytes/sec (rolling sample).</summary>
    public long SpeedBytesPerSec
    {
        get => _speedBytesPerSec;
        private set => SetProperty(ref _speedBytesPerSec, value);
    }

    /// <summary>Human-readable ETA string (e.g. "~3s", "~2m", or "—" when unknown).</summary>
    public string Eta
    {
        get => _eta;
        private set => SetProperty(ref _eta, value);
    }

    /// <summary>Whether the transfer has completed.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetProperty(ref _isComplete, value);
    }

    /// <summary>Human-readable speed string (e.g. "3.2 MB/s").</summary>
    public string SpeedLabel =>
        SpeedBytesPerSec >= 1024 * 1024
            ? $"{SpeedBytesPerSec / 1024.0 / 1024.0:F1} MB/s"
            : $"{SpeedBytesPerSec / 1024.0:F0} KB/s";

    /// <summary>Human-readable transferred/total string (e.g. "12.3 MB / 100 MB").</summary>
    public string BytesLabel =>
        $"{FormatBytes(BytesTransferred)} / {FormatBytes(TotalBytes)}";

    /// <summary>Initializes a new <see cref="ActiveTransferViewModel"/>.</summary>
    public ActiveTransferViewModel(Guid contextId, string fileName, string direction)
    {
        Key = $"{contextId}:{fileName}:{direction}";
        FileName = fileName;
        Direction = direction;
    }

    /// <summary>Updates the view-model with a fresh progress snapshot.</summary>
    public void Update(long bytesTransferred, long totalBytes, int chunksCompleted, int chunksTotal, double percentComplete)
    {
        TotalBytes = totalBytes;

        // Rolling speed sample (every ≥250ms for smoothness).
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastSpeedSample).TotalSeconds;
        if (elapsed >= 0.25)
        {
            var bytesDelta = bytesTransferred - _lastBytesTransferred;
            SpeedBytesPerSec = elapsed > 0 ? (long)(bytesDelta / elapsed) : 0;
            _lastBytesTransferred = bytesTransferred;
            _lastSpeedSample = now;
            OnPropertyChanged(nameof(SpeedLabel));
        }

        BytesTransferred = bytesTransferred;
        PercentComplete = percentComplete;
        OnPropertyChanged(nameof(BytesLabel));

        // ETA
        if (SpeedBytesPerSec > 0 && totalBytes > bytesTransferred)
        {
            var remaining = totalBytes - bytesTransferred;
            var etaSec = remaining / (double)SpeedBytesPerSec;
            Eta = etaSec < 60
                ? $"~{(int)etaSec}s"
                : $"~{(int)(etaSec / 60)}m";
        }
        else
        {
            Eta = "—";
        }
    }

    /// <summary>Marks the transfer as complete and schedules auto-dismiss after 5 seconds.</summary>
    public void MarkComplete(long totalBytes)
    {
        TotalBytes = totalBytes;
        BytesTransferred = totalBytes;
        PercentComplete = 100;
        SpeedBytesPerSec = 0;
        Eta = "Done";
        IsComplete = true;
        OnPropertyChanged(nameof(BytesLabel));
        OnPropertyChanged(nameof(SpeedLabel));
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024L * 1024 * 1024
            ? $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB"
            : bytes >= 1024 * 1024
                ? $"{bytes / 1024.0 / 1024.0:F1} MB"
                : $"{bytes / 1024.0:F0} KB";
}
