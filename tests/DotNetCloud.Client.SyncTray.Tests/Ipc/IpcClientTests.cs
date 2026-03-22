using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using DotNetCloud.Client.SyncService.Ipc;
using DotNetCloud.Client.SyncTray.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.SyncTray.Tests.Ipc;

[TestClass]
public sealed class IpcClientTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    // ── Default state ─────────────────────────────────────────────────────

    [TestMethod]
    public void IsConnected_DefaultsToFalse()
    {
        var client = new IpcClient(NullLogger<IpcClient>.Instance);
        Assert.IsFalse(client.IsConnected);
    }

    // ── Event deserialization ─────────────────────────────────────────────

    [TestMethod]
    public async Task ConnectAsync_ParsesSyncProgressEvent()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        SyncProgressEventData? received = null;
        client.SyncProgressReceived += (_, e) => received = e;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        await SimulateServerAsync(serverStream,
            new IpcMessage
            {
                Type = "event", Event = IpcEvents.SyncProgress,
                ContextId = Guid.NewGuid(), Success = true,
                Data = new { state = "Syncing", pendingUploads = 3, pendingDownloads = 1 },
            }, cts.Token);

        await WaitForCondition(() => received is not null, cts.Token);
        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));

        Assert.IsNotNull(received, "SyncProgressReceived should have been raised.");
        Assert.AreEqual("Syncing", received!.State);
        Assert.AreEqual(3, received.PendingUploads);
    }

    [TestMethod]
    public async Task ConnectAsync_ParsesSyncCompleteEvent()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        SyncCompleteEventData? received = null;
        client.SyncCompleteReceived += (_, e) => received = e;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        await SimulateServerAsync(serverStream,
            new IpcMessage
            {
                Type = "event", Event = IpcEvents.SyncComplete,
                ContextId = Guid.NewGuid(), Success = true,
                Data = new { lastSyncedAt = DateTime.UtcNow, conflicts = 2 },
            }, cts.Token);

        await WaitForCondition(() => received is not null, cts.Token);
        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));

        Assert.IsNotNull(received);
        Assert.AreEqual(2, received!.Conflicts);
    }

    [TestMethod]
    public async Task ConnectAsync_ParsesErrorEvent()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        SyncErrorEventData? received = null;
        client.SyncErrorReceived += (_, e) => received = e;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        await SimulateServerAsync(serverStream,
            new IpcMessage
            {
                Type = "event", Event = IpcEvents.Error,
                ContextId = Guid.NewGuid(), Success = true,
                Data = new { error = "Disk full" },
            }, cts.Token);

        await WaitForCondition(() => received is not null, cts.Token);
        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));

        Assert.IsNotNull(received);
        Assert.AreEqual("Disk full", received!.Error);
    }

    [TestMethod]
    public async Task ConnectAsync_ParsesConflictEvent()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        SyncConflictEventData? received = null;
        client.ConflictDetected += (_, e) => received = e;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        await SimulateServerAsync(serverStream,
            new IpcMessage
            {
                Type = "event", Event = IpcEvents.ConflictDetected,
                ContextId = Guid.NewGuid(), Success = true,
                Data = new { originalPath = "docs/report.docx", conflictCopyPath = "docs/report (conflict).docx" },
            }, cts.Token);

        await WaitForCondition(() => received is not null, cts.Token);
        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));

        Assert.IsNotNull(received);
        Assert.AreEqual("docs/report.docx", received!.OriginalPath);
    }

    [TestMethod]
    public async Task ConnectAsync_RaisesConnectionStateChangedOnConnect()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        bool? connectedState = null;
        client.ConnectionStateChanged += (_, c) => connectedState = c;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        // Server sends subscribe response to allow connect to complete.
        _ = Task.Run(async () =>
        {
            var writer = new StreamWriter(serverStream, Encoding.UTF8) { AutoFlush = true };
            var reader = new StreamReader(serverStream, Encoding.UTF8);
            await reader.ReadLineAsync(cts.Token);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage
            {
                Type = "response", Command = IpcCommands.Subscribe, Success = true,
                Data = new { subscribed = true },
            }, JsonOptions));
            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await WaitForCondition(() => connectedState is not null, cts.Token);

        Assert.IsTrue(connectedState);

        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));
    }

    [TestMethod]
    public async Task ListContextsAsync_ConcurrentDuplicateRequests_ShareSingleResponse()
    {
        var (clientStream, serverStream) = DuplexStreamPair.Create();
        var client = ClientFromStream(clientStream);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectTask = client.ConnectAsync(cts.Token);

        _ = Task.Run(async () =>
        {
            var writer = new StreamWriter(serverStream, Encoding.UTF8) { AutoFlush = true };
            var reader = new StreamReader(serverStream, Encoding.UTF8);

            await reader.ReadLineAsync(cts.Token);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage
            {
                Type = "response",
                Command = IpcCommands.Subscribe,
                Success = true,
                Data = new { subscribed = true },
            }, JsonOptions));

            var listContextsRequest = await reader.ReadLineAsync(cts.Token);
            Assert.IsNotNull(listContextsRequest);
            StringAssert.Contains(listContextsRequest, IpcCommands.ListContexts);

            await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage
            {
                Type = "response",
                Command = IpcCommands.ListContexts,
                Success = true,
                Data = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(),
                        displayName = "testdude@llabmik.net @ mint22",
                        serverBaseUrl = "https://mint22.kimball.home:5443",
                        localFolderPath = "/home/benk/synctray",
                        state = "Idle",
                        pendingUploads = 0,
                        pendingDownloads = 0,
                    },
                },
            }, JsonOptions));

            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await WaitForCondition(() => client.IsConnected, cts.Token);

        var first = client.ListContextsAsync(cts.Token);
        var second = client.ListContextsAsync(cts.Token);
        await Task.WhenAll(first, second);

        Assert.AreEqual(1, first.Result.Count);
        Assert.AreEqual(1, second.Result.Count);
        Assert.AreEqual(first.Result[0].DisplayName, second.Result[0].DisplayName);

        cts.Cancel();
        await Task.WhenAny(connectTask, Task.Delay(500));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="IpcClient"/> whose transport is the given
    /// in-memory stream (bypasses Named Pipe / Unix socket creation).
    /// </summary>
    private static IpcClient ClientFromStream(Stream stream) =>
        new(ct => Task.FromResult(stream), NullLogger<IpcClient>.Instance);

    /// <summary>
    /// Simulates the SyncService: reads the subscribe command, responds,
    /// pushes the provided event, then keeps the connection alive until cancelled.
    /// </summary>
    private static async Task SimulateServerAsync(
        Stream server, IpcMessage pushEvent, CancellationToken ct)
    {
        var writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true };
        var reader = new StreamReader(server, Encoding.UTF8);

        // Read subscribe command.
        await reader.ReadLineAsync(ct);

        // Respond to subscribe.
        await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcMessage
        {
            Type = "response",
            Command = IpcCommands.Subscribe,
            Success = true,
            Data = new { subscribed = true },
        }, JsonOptions));

        // Push the event.
        await writer.WriteLineAsync(JsonSerializer.Serialize(pushEvent, JsonOptions));

        // Keep alive until cancelled.
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }
    }

    /// <summary>Polls until <paramref name="condition"/> is true or the token is cancelled.</summary>
    private static async Task WaitForCondition(Func<bool> condition, CancellationToken ct)
    {
        while (!condition() && !ct.IsCancellationRequested)
            await Task.Delay(20, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Creates a pair of in-memory bidirectional streams backed by
/// <see cref="Pipe"/> instances.
/// </summary>
internal static class DuplexStreamPair
{
    /// <summary>Creates a connected (clientStream, serverStream) pair.</summary>
    public static (Stream clientStream, Stream serverStream) Create()
    {
        var serverToClient = new Pipe();
        var clientToServer = new Pipe();

        var clientStream = new DuplexPipeStream(clientToServer.Writer, serverToClient.Reader);
        var serverStream = new DuplexPipeStream(serverToClient.Writer, clientToServer.Reader);

        return (clientStream, serverStream);
    }

    private sealed class DuplexPipeStream : Stream
    {
        private readonly PipeWriter _writer;
        private readonly Stream _readerStream;

        public DuplexPipeStream(PipeWriter writer, PipeReader reader)
        {
            _writer = writer;
            _readerStream = reader.AsStream();
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            _readerStream.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            _readerStream.ReadAsync(buffer, offset, count, ct);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            _readerStream.ReadAsync(buffer, ct);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writer.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
            _writer.FlushAsync().AsTask().GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await _writer.WriteAsync(buffer.AsMemory(offset, count), ct);
            await _writer.FlushAsync(ct);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await _writer.WriteAsync(buffer, ct);
            await _writer.FlushAsync(ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Complete();
                _readerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
