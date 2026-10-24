using System.IO.Pipelines;
using DotNetCloud.Core.Server.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="CountingResponseBodyFeature"/> and its writer wrappers.
/// </summary>
[TestClass]
public sealed class CountingResponseBodyFeatureTests
{
    [TestMethod]
    public async Task Stream_WriteAsync_CountsBytesExactlyOnce()
    {
        var inner = new FakeResponseBodyFeature();
        long counted = 0;
        var feature = new CountingResponseBodyFeature(inner, n => counted += n);

        await feature.Stream.WriteAsync(new byte[100]);
        await feature.Stream.WriteAsync(new byte[23]);

        Assert.AreEqual(123, counted);
        Assert.AreEqual(123, inner.Stream.Length);
    }

    [TestMethod]
    public async Task Writer_WriteAsync_CountsBytesExactlyOnce()
    {
        var inner = new FakeResponseBodyFeature();
        long counted = 0;
        var feature = new CountingResponseBodyFeature(inner, n => counted += n);

        await feature.Writer.WriteAsync(new byte[64]);

        Assert.AreEqual(64, counted);
        Assert.AreEqual(64, inner.Stream.Length);
    }

    [TestMethod]
    public async Task StreamAndWriter_Writes_DoNotDoubleCount()
    {
        var inner = new FakeResponseBodyFeature();
        long counted = 0;
        var feature = new CountingResponseBodyFeature(inner, n => counted += n);

        await feature.Stream.WriteAsync(new byte[10]);
        await feature.Writer.WriteAsync(new byte[5]);
        await feature.Stream.WriteAsync(new byte[1]);

        Assert.AreEqual(16, counted, "Each byte must be counted once, regardless of write path.");
        Assert.AreEqual(16, inner.Stream.Length);
    }

    [TestMethod]
    public async Task SendFileAsync_CountsFileLengthOnce_AndDelegatesArguments()
    {
        var inner = new FakeResponseBodyFeature();
        long counted = 0;
        var feature = new CountingResponseBodyFeature(inner, n => counted += n);

        await feature.SendFileAsync("/tmp/file.bin", 32, 4096);

        Assert.AreEqual(4096, counted);
        Assert.AreEqual("/tmp/file.bin", inner.LastSendFilePath);
        Assert.AreEqual(32, inner.LastSendFileOffset);
        Assert.AreEqual(4096, inner.LastSendFileCount);
        Assert.AreEqual(0, inner.Stream.Length, "SendFile must not route bytes through the stream.");
    }

    [TestMethod]
    public async Task SendFileAsync_WithoutCount_DoesNotCount()
    {
        var inner = new FakeResponseBodyFeature();
        long counted = 0;
        var feature = new CountingResponseBodyFeature(inner, n => counted += n);

        await feature.SendFileAsync("/tmp/file.bin", 0, null);

        Assert.AreEqual(0, counted);
    }

    [TestMethod]
    public async Task DelegatingMembers_CallInnerFeature()
    {
        var inner = new FakeResponseBodyFeature();
        var feature = new CountingResponseBodyFeature(inner, _ => { });

        feature.DisableBuffering();
        await feature.StartAsync();
        await feature.CompleteAsync();

        Assert.IsTrue(inner.DisableBufferingCalled);
        Assert.IsTrue(inner.StartCalled);
        Assert.IsTrue(inner.CompleteCalled);
    }

    private sealed class FakeResponseBodyFeature : IHttpResponseBodyFeature
    {
        private readonly MemoryStream _responseStream = new();
        private readonly PipeWriter _writer;
        private bool _writerCreated;

        public FakeResponseBodyFeature()
        {
            _responseStream = new MemoryStream();
            _writer = PipeWriter.Create(_responseStream, new StreamPipeWriterOptions(leaveOpen: true));
        }

        public Stream Stream => _responseStream;

        public PipeWriter Writer
        {
            get
            {
                _writerCreated = true;
                return _writer;
            }
        }

        public bool WriterCreated => _writerCreated;
        public bool DisableBufferingCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool CompleteCalled { get; private set; }
        public string? LastSendFilePath { get; private set; }
        public long LastSendFileOffset { get; private set; }
        public long? LastSendFileCount { get; private set; }

        public void DisableBuffering() => DisableBufferingCalled = true;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        {
            LastSendFilePath = path;
            LastSendFileOffset = offset;
            LastSendFileCount = count;
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            CompleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
