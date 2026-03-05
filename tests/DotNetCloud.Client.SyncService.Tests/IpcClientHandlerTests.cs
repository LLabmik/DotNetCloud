using System.Text;
using System.Text.Json;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.SyncService.ContextManager;
using DotNetCloud.Client.SyncService.Ipc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Client.SyncService.Tests;

/// <summary>Tests for <see cref="IpcClientHandler"/> command dispatch.</summary>
[TestClass]
public class IpcClientHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sends one command line, runs the handler for one iteration, then returns the response line.
    /// </summary>
    private static async Task<IpcMessage?> SendCommandAsync(
        string commandJson,
        ISyncContextManager contextManager)
    {
        var inputBytes = Encoding.UTF8.GetBytes(commandJson + "\n");
        var outputBuffer = new MemoryStream();

        // Compose: input stream → handler reads from it, writes to output
        var inputStream = new MemoryStream(inputBytes);
        var combinedStream = new DuplexStream(inputStream, outputBuffer);

        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger>();
        var handler = new IpcClientHandler(combinedStream, contextManager, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await handler.HandleAsync(cts.Token);

        outputBuffer.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(outputBuffer).ReadLineAsync();
        if (responseJson is null) return null;

        return JsonSerializer.Deserialize<IpcMessage>(responseJson, JsonOptions);
    }

    // ── list-contexts ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_ListContextsCommand_ReturnsSuccessResponse()
    {
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.GetContextsAsync())
            .ReturnsAsync([]);

        var command = new IpcCommand { Command = IpcCommands.ListContexts };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.AreEqual("response", response.Type);
        Assert.IsTrue(response.Success);
        Assert.AreEqual(IpcCommands.ListContexts, response.Command);
    }

    // ── get-status ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_GetStatusCommand_MissingContextId_ReturnsError()
    {
        var managerMock = new Mock<ISyncContextManager>();

        var command = new IpcCommand { Command = IpcCommands.GetStatus };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.AreEqual("response", response.Type);
        Assert.IsFalse(response.Success);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error, "contextId");
    }

    [TestMethod]
    public async Task HandleAsync_GetStatusCommand_ContextNotFound_ReturnsError()
    {
        var contextId = Guid.NewGuid();
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.GetStatusAsync(contextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncStatus?)null);

        var command = new IpcCommand { Command = IpcCommands.GetStatus, ContextId = contextId };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
    }

    [TestMethod]
    public async Task HandleAsync_GetStatusCommand_ContextFound_ReturnsStatus()
    {
        var contextId = Guid.NewGuid();
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.GetStatusAsync(contextId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncStatus { State = SyncState.Idle });

        var command = new IpcCommand { Command = IpcCommands.GetStatus, ContextId = contextId };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        Assert.AreEqual(contextId, response.ContextId);
    }

    // ── pause / resume ────────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_PauseCommand_CallsPauseAsync()
    {
        var contextId = Guid.NewGuid();
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.PauseAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IpcCommand { Command = IpcCommands.Pause, ContextId = contextId };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        managerMock.Verify(m => m.PauseAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_ResumeCommand_CallsResumeAsync()
    {
        var contextId = Guid.NewGuid();
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.ResumeAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IpcCommand { Command = IpcCommands.Resume, ContextId = contextId };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        managerMock.Verify(m => m.ResumeAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── remove-account ────────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_RemoveAccountCommand_CallsRemoveContextAsync()
    {
        var contextId = Guid.NewGuid();
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.Setup(m => m.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new IpcCommand { Command = IpcCommands.RemoveAccount, ContextId = contextId };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        managerMock.Verify(m => m.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── subscribe / unsubscribe ───────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_SubscribeCommand_ReturnsSubscribedTrue()
    {
        var managerMock = new Mock<ISyncContextManager>();
        managerMock.SetupAdd(m => m.SyncProgress += It.IsAny<EventHandler<SyncProgressEventArgs>>());
        managerMock.SetupAdd(m => m.SyncComplete += It.IsAny<EventHandler<SyncCompleteEventArgs>>());
        managerMock.SetupAdd(m => m.SyncError += It.IsAny<EventHandler<SyncErrorEventArgs>>());
        managerMock.SetupAdd(m => m.ConflictDetected += It.IsAny<EventHandler<SyncConflictDetectedEventArgs>>());

        var command = new IpcCommand { Command = IpcCommands.Subscribe };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
    }

    // ── unknown command ───────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_UnknownCommand_ReturnsErrorResponse()
    {
        var managerMock = new Mock<ISyncContextManager>();

        var command = new IpcCommand { Command = "does-not-exist" };
        var response = await SendCommandAsync(
            JsonSerializer.Serialize(command, JsonOptions), managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.IsNotNull(response.Error);
    }

    // ── malformed JSON ────────────────────────────────────────────────────

    [TestMethod]
    public async Task HandleAsync_InvalidJson_ReturnsErrorResponse()
    {
        var managerMock = new Mock<ISyncContextManager>();

        var response = await SendCommandAsync("{ not valid json !!!}", managerMock.Object);

        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
    }
}

// ── Test helper ──────────────────────────────────────────────────────────────

/// <summary>
/// A <see cref="Stream"/> that wraps separate read and write streams,
/// used to inject pre-built input and capture output in unit tests.
/// </summary>
internal sealed class DuplexStream(Stream readStream, Stream writeStream) : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        readStream.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        readStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        readStream.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) =>
        writeStream.Write(buffer, offset, count);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        writeStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        writeStream.WriteAsync(buffer, cancellationToken);

    public override void Flush() => writeStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => writeStream.FlushAsync(cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
