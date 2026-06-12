namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// A read-only FileStream wrapper that supports reading from a file
/// that is concurrently being written by another process (ffmpeg).
///
/// When Read/ReadAsync reaches the current end of the file, it waits
/// briefly and retries, allowing the writer to produce more data.
/// Returns 0 (EOF) only when the transcode job has completed and
/// all data has been read.
///
/// Inspired by Jellyfin's ProgressiveFileStream.
/// </summary>
public sealed class ProgressiveFileStream : Stream
{
    private readonly FileStream _inner;
    private readonly Func<bool> _isWriteComplete;
    private readonly int _pollIntervalMs;
    private long _position;
    private bool _disposed;

    /// <summary>
    /// Creates a new ProgressiveFileStream.
    /// </summary>
    /// <param name="filePath">Path to the file being written by ffmpeg.</param>
    /// <param name="isWriteComplete">Function that returns true when ffmpeg has finished writing.</param>
    /// <param name="pollIntervalMs">How long to wait between retries when data is not yet available (default 250ms).</param>
    public ProgressiveFileStream(
        string filePath,
        Func<bool> isWriteComplete,
        int pollIntervalMs = 250)
    {
        // Wait for the file to be created (ffmpeg starts in the background)
        var waited = 0;
        while (!File.Exists(filePath) && !isWriteComplete() && waited < 30000)
        {
            Thread.Sleep(pollIntervalMs);
            waited += pollIntervalMs;
        }

        _inner = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        _isWriteComplete = isWriteComplete;
        _pollIntervalMs = pollIntervalMs;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = _inner.Read(buffer, offset + totalRead, count - totalRead);
            if (bytesRead > 0)
            {
                totalRead += bytesRead;
                _position += bytesRead;
            }
            else if (_isWriteComplete())
            {
                break; // Writer done, no more data
            }
            else
            {
                Thread.Sleep(_pollIntervalMs);
            }
        }
        return totalRead;
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = await _inner.ReadAsync(
                buffer.AsMemory(offset + totalRead, count - totalRead),
                cancellationToken);

            if (bytesRead > 0)
            {
                totalRead += bytesRead;
                _position += bytesRead;
            }
            else if (_isWriteComplete())
            {
                break; // Writer done, no more data
            }
            else
            {
                await Task.Delay(_pollIntervalMs, cancellationToken);
            }
        }
        return totalRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPos < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        _position = _inner.Seek(newPos, SeekOrigin.Begin);
        return _position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
