using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Host.Controllers;

/// <summary>
/// Wraps a source stream and reports copy progress to a <see cref="StreamProgressState"/>.
/// Used during chunk reconstruction to show the user a progress bar while the temp file is being assembled.
/// </summary>
internal sealed class ProgressReportingStream : Stream
{
    private readonly Stream _inner;
    private readonly Guid _videoId;
    private readonly long _totalBytes;
    private readonly StreamProgressState _progress;
    private readonly ILogger _logger;
    private long _bytesRead;

    public ProgressReportingStream(
        Stream inner,
        Guid videoId,
        long totalBytes,
        StreamProgressState progress,
        ILogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _videoId = videoId;
        _totalBytes = totalBytes;
        _progress = progress;
        _logger = logger;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytes = _inner.Read(buffer, offset, count);
        _bytesRead += bytes;
        UpdateProgress();
        return bytes;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytes = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytes;
        UpdateProgress();
        return bytes;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void UpdateProgress()
    {
        if (_totalBytes <= 0) return;

        var percent = Math.Min(100.0, (double)_bytesRead / _totalBytes * 100.0);
        var entry = _progress.Get(_videoId);
        if (entry is not null)
        {
            entry.Percent = percent;
            entry.Message = $"Assembling video file… {percent:F0}%";
            entry.LastUpdated = DateTime.UtcNow;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Mark reconstruction as complete
            var entry = _progress.Get(_videoId);
            if (entry is not null)
            {
                entry.Stage = StreamProgressStage.Probing;
                entry.Message = "Analyzing video…";
                entry.Percent = 100;
                entry.LastUpdated = DateTime.UtcNow;
            }
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
