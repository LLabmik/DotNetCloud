using System.IO.Pipelines;
using System.Text;
using DotNetCloud.Core.Server.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="CountingRequestBodyPipeFeature"/>.
/// </summary>
[TestClass]
public sealed class CountingRequestBodyPipeFeatureTests
{
    [TestMethod]
    public async Task ReadAsync_DeliveredBytes_AreCounted()
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(new byte[128]);
        await pipe.Writer.CompleteAsync();

        long counted = 0;
        var feature = new CountingRequestBodyPipeFeature(new FakeRequestPipeFeature(pipe.Reader), n => counted += n);

        var result = await feature.Reader.ReadAsync();
        var length = result.Buffer.Length;
        feature.Reader.AdvanceTo(result.Buffer.End);

        Assert.AreEqual(128L, length);
        Assert.AreEqual(128, counted);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyCompleteRead_CountsNothing()
    {
        var pipe = new Pipe();
        await pipe.Writer.CompleteAsync();

        long counted = 0;
        var feature = new CountingRequestBodyPipeFeature(new FakeRequestPipeFeature(pipe.Reader), n => counted += n);

        var result = await feature.Reader.ReadAsync();

        Assert.IsTrue(result.IsCompleted);
        Assert.AreEqual(0, counted);
    }

    [TestMethod]
    public async Task ReadAsync_PartialBuffers_AccumulateExactly()
    {
        var pipe = new Pipe();
        long counted = 0;
        var feature = new CountingRequestBodyPipeFeature(new FakeRequestPipeFeature(pipe.Reader), n => counted += n);

        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("hello"));
        var first = await feature.Reader.ReadAsync();
        feature.Reader.AdvanceTo(first.Buffer.End);

        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(" world"));
        var second = await feature.Reader.ReadAsync();
        feature.Reader.AdvanceTo(second.Buffer.End);

        Assert.AreEqual(11, counted);
    }

    [TestMethod]
    public async Task Reader_DelegatesAdvanceAndCompletion()
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(new byte[4]);

        var fake = new FakeRequestPipeFeature(pipe.Reader);
        var feature = new CountingRequestBodyPipeFeature(fake, _ => { });

        var result = await feature.Reader.ReadAsync();
        feature.Reader.AdvanceTo(result.Buffer.End);
        await feature.Reader.CompleteAsync();

        Assert.AreEqual(1, fake.AdvanceCount);
        Assert.IsTrue(fake.Completed);
    }

    private sealed class FakeRequestPipeFeature : IRequestBodyPipeFeature
    {
        private readonly TrackingPipeReader _reader;

        public FakeRequestPipeFeature(PipeReader reader) => _reader = new TrackingPipeReader(reader);

        public PipeReader Reader => _reader;

        public int AdvanceCount => _reader.AdvanceCount;

        public bool Completed => _reader.Completed;
    }

    private sealed class TrackingPipeReader(PipeReader inner) : PipeReader
    {
        public int AdvanceCount { get; private set; }

        public bool Completed { get; private set; }

        public override void AdvanceTo(SequencePosition consumed)
        {
            AdvanceCount++;
            inner.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            AdvanceCount++;
            inner.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead() => inner.CancelPendingRead();

        public override void Complete(Exception? exception = null)
        {
            Completed = true;
            inner.Complete(exception);
        }

        public override ValueTask CompleteAsync(Exception? exception = null)
        {
            Completed = true;
            return inner.CompleteAsync(exception);
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) =>
            inner.ReadAsync(cancellationToken);

        public override bool TryRead(out ReadResult result) => inner.TryRead(out result);

        public override Stream AsStream(bool leaveOpen = false) => inner.AsStream(leaveOpen);
    }
}
