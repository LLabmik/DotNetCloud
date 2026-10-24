using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Response body feature that counts bytes written by the application without
/// buffering, re-ordering or breaking streaming or sendfile.
/// </summary>
/// <remarks>
/// Both <see cref="Stream"/> and <see cref="Writer"/> delegate to the inner feature
/// so the sendfile path is preserved. Each byte is counted exactly once: stream and
/// pipe writer writes are independent sinks in the underlying feature.
/// </remarks>
internal sealed class CountingResponseBodyFeature : IHttpResponseBodyFeature
{
    private readonly IHttpResponseBodyFeature _inner;
    private readonly Action<long> _addOut;

    private CountingWriteStream? _stream;
    private CountingPipeWriter? _writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountingResponseBodyFeature"/> class.
    /// </summary>
    /// <param name="inner">The feature being wrapped.</param>
    /// <param name="addOut">Callback invoked with the number of bytes sent.</param>
    public CountingResponseBodyFeature(IHttpResponseBodyFeature inner, Action<long> addOut)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addOut = addOut ?? throw new ArgumentNullException(nameof(addOut));
    }

    /// <inheritdoc />
    public Stream Stream => _stream ??= new CountingWriteStream(_inner.Stream, _addOut);

    /// <inheritdoc />
    public PipeWriter Writer => _writer ??= new CountingPipeWriter(_inner.Writer, _addOut);

    /// <inheritdoc />
    public void DisableBuffering() => _inner.DisableBuffering();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _inner.StartAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SendFileAsync(
        string path,
        long offset,
        long? count,
        CancellationToken cancellationToken = default)
    {
        await _inner.SendFileAsync(path, offset, count, cancellationToken).ConfigureAwait(false);

        if (count is > 0)
        {
            _addOut(count.Value);
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync() => _inner.CompleteAsync();
}

/// <summary>
/// Write-only stream wrapper that reports the number of bytes written.
/// </summary>
internal sealed class CountingWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly Action<long> _addOut;

    public CountingWriteStream(Stream inner, Action<long> addOut)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addOut = addOut ?? throw new ArgumentNullException(nameof(addOut));
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        _addOut(count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        _addOut(buffer.Length);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _addOut(buffer.Length);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // The inner stream belongs to the wrapped feature — never dispose it.
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Pipe writer wrapper that reports the number of committed bytes.
/// </summary>
internal sealed class CountingPipeWriter : PipeWriter
{
    private readonly PipeWriter _inner;
    private readonly Action<long> _addOut;

    public CountingPipeWriter(PipeWriter inner, Action<long> addOut)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addOut = addOut ?? throw new ArgumentNullException(nameof(addOut));
    }

    public override bool CanGetUnflushedBytes => _inner.CanGetUnflushedBytes;

    public override long UnflushedBytes => _inner.UnflushedBytes;

    public override void Advance(int bytes)
    {
        _inner.Advance(bytes);
        if (bytes > 0)
        {
            _addOut(bytes);
        }
    }

    public override Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);

    public override Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);

    public override void CancelPendingFlush() => _inner.CancelPendingFlush();

    public override void Complete(Exception? exception = null) => _inner.Complete(exception);

    public override ValueTask CompleteAsync(Exception? exception = null) => _inner.CompleteAsync(exception);

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
        _inner.FlushAsync(cancellationToken);

    public override async ValueTask<FlushResult> WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.WriteAsync(source, cancellationToken).ConfigureAwait(false);
        if (source.Length > 0)
        {
            _addOut(source.Length);
        }

        return result;
    }
}
