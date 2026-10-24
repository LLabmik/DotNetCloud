using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Request body pipe feature that counts bytes delivered to the application.
/// </summary>
/// <remarks>
/// Wraps <see cref="IRequestBodyPipeFeature.Reader"/> so every <c>ReadAsync</c> /
/// <c>TryRead</c> result is measured. Nothing is buffered and the reader is never
/// drained ahead of the consumer, so uploads and proxied bodies stream unchanged.
/// </remarks>
internal sealed class CountingRequestBodyPipeFeature : IRequestBodyPipeFeature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CountingRequestBodyPipeFeature"/> class.
    /// </summary>
    /// <param name="inner">The feature being wrapped.</param>
    /// <param name="addIn">Callback invoked with the number of bytes received.</param>
    public CountingRequestBodyPipeFeature(IRequestBodyPipeFeature inner, Action<long> addIn)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(addIn);
        Reader = new CountingPipeReader(inner.Reader, addIn);
    }

    /// <inheritdoc />
    public PipeReader Reader { get; }
}

/// <summary>
/// Pipe reader wrapper that reports the size of each delivered buffer.
/// </summary>
internal sealed class CountingPipeReader : PipeReader
{
    private readonly PipeReader _inner;
    private readonly Action<long> _addIn;

    public CountingPipeReader(PipeReader inner, Action<long> addIn)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addIn = addIn ?? throw new ArgumentNullException(nameof(addIn));
    }

    public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        Count(result);
        return result;
    }

    public override bool TryRead(out ReadResult result)
    {
        var hasData = _inner.TryRead(out result);
        if (hasData)
        {
            Count(result);
        }

        return hasData;
    }

    public override void AdvanceTo(SequencePosition consumed) => _inner.AdvanceTo(consumed);

    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) =>
        _inner.AdvanceTo(consumed, examined);

    public override void CancelPendingRead() => _inner.CancelPendingRead();

    public override void Complete(Exception? exception = null) => _inner.Complete(exception);

    public override ValueTask CompleteAsync(Exception? exception = null) => _inner.CompleteAsync(exception);

    public override Stream AsStream(bool leaveOpen = false) => _inner.AsStream(leaveOpen);

    private void Count(ReadResult result)
    {
        var length = result.Buffer.Length;
        if (length > 0)
        {
            _addIn(length);
        }
    }
}
