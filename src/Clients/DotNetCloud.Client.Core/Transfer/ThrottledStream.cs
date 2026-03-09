namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Stream wrapper that rate-limits reads and writes using a token-bucket algorithm.
/// Pass <c>bytesPerSecond &lt;= 0</c> for unlimited (pass-through) mode.
/// </summary>
public sealed class ThrottledStream : Stream
{
    private readonly Stream _inner;
    private readonly long _bytesPerSecond;
    private readonly bool _leaveOpen;

    private long _availableTokens;
    private long _lastRefillTicks;
    private readonly object _tokenLock = new();

    /// <summary>Initializes a new <see cref="ThrottledStream"/>.</summary>
    /// <param name="inner">The underlying stream to wrap.</param>
    /// <param name="bytesPerSecond">Maximum throughput in bytes/second. 0 or negative = unlimited.</param>
    /// <param name="leaveOpen">If <c>true</c>, the inner stream is not disposed when this stream is disposed.</param>
    public ThrottledStream(Stream inner, long bytesPerSecond, bool leaveOpen = false)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _bytesPerSecond = bytesPerSecond;
        _leaveOpen = leaveOpen;
        _availableTokens = bytesPerSecond > 0 ? bytesPerSecond : 0;
        _lastRefillTicks = Environment.TickCount64;
    }

    /// <inheritdoc/>
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => _inner.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => _inner.CanWrite;

    /// <inheritdoc/>
    public override long Length => _inner.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc/>
    public override void Flush() => _inner.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc/>
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesPerSecond <= 0)
            return _inner.Read(buffer, offset, count);

        ThrottleSync(count);
        return _inner.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_bytesPerSecond <= 0)
            return await _inner.ReadAsync(buffer, offset, count, cancellationToken);

        await ThrottleAsync(count, cancellationToken);
        return await _inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_bytesPerSecond <= 0)
            return await _inner.ReadAsync(buffer, cancellationToken);

        await ThrottleAsync(buffer.Length, cancellationToken);
        return await _inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_bytesPerSecond <= 0)
        {
            _inner.Write(buffer, offset, count);
            return;
        }

        ThrottleSync(count);
        _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_bytesPerSecond <= 0)
        {
            await _inner.WriteAsync(buffer, offset, count, cancellationToken);
            return;
        }

        await ThrottleAsync(count, cancellationToken);
        await _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_bytesPerSecond <= 0)
        {
            await _inner.WriteAsync(buffer, cancellationToken);
            return;
        }

        await ThrottleAsync(buffer.Length, cancellationToken);
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _inner.Dispose();

        base.Dispose(disposing);
    }

    private void ThrottleSync(int byteCount)
    {
        var waitMs = CalculateWaitMs(byteCount);
        if (waitMs > 0)
            Thread.Sleep(waitMs);
    }

    private async Task ThrottleAsync(int byteCount, CancellationToken cancellationToken)
    {
        var waitMs = CalculateWaitMs(byteCount);
        if (waitMs > 0)
            await Task.Delay(waitMs, cancellationToken);
    }

    private int CalculateWaitMs(int byteCount)
    {
        lock (_tokenLock)
        {
            RefillTokens();

            _availableTokens -= byteCount;
            if (_availableTokens >= 0)
                return 0;

            // Calculate how long to wait for the deficit to be filled.
            var deficit = -_availableTokens;
            var waitMs = (int)(deficit * 1000 / _bytesPerSecond);
            return Math.Max(waitMs, 1);
        }
    }

    private void RefillTokens()
    {
        var now = Environment.TickCount64;
        var elapsedMs = now - _lastRefillTicks;
        if (elapsedMs <= 0)
            return;

        var newTokens = _bytesPerSecond * elapsedMs / 1000;
        _availableTokens = Math.Min(_availableTokens + newTokens, _bytesPerSecond);
        _lastRefillTicks = now;
    }
}
