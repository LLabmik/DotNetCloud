using DotNetCloud.Core.Server.Supervisor;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

[TestClass]
public class GrpcChannelManagerTests
{
    private readonly GrpcChannelManager _manager;

    public GrpcChannelManagerTests()
    {
        _manager = new GrpcChannelManager(NullLogger<GrpcChannelManager>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _manager.Dispose();
    }

    [TestMethod]
    public void GetOrCreateChannelThenReturnsChannel()
    {
        var channel = _manager.GetOrCreateChannel("test.module", "http://localhost:50100");

        Assert.IsNotNull(channel);
    }

    [TestMethod]
    public void GetOrCreateChannelTwiceThenReturnsSameInstance()
    {
        var channel1 = _manager.GetOrCreateChannel("test.module", "http://localhost:50100");
        var channel2 = _manager.GetOrCreateChannel("test.module", "http://localhost:50100");

        Assert.AreSame(channel1, channel2);
    }

    [TestMethod]
    public void GetOrCreateChannelForDifferentModulesThenReturnsDifferentChannels()
    {
        var channel1 = _manager.GetOrCreateChannel("module.a", "http://localhost:50100");
        var channel2 = _manager.GetOrCreateChannel("module.b", "http://localhost:50101");

        Assert.AreNotSame(channel1, channel2);
    }

    [TestMethod]
    public async Task RemoveChannelAsyncThenChannelIsRemoved()
    {
        _manager.GetOrCreateChannel("test.module", "http://localhost:50100");

        await _manager.RemoveChannelAsync("test.module");

        // Getting the same module again should create a new channel
        var newChannel = _manager.GetOrCreateChannel("test.module", "http://localhost:50100");
        Assert.IsNotNull(newChannel);
    }

    [TestMethod]
    public async Task RemoveChannelAsyncWhenNotExistsThenNoOp()
    {
        // Should not throw
        await _manager.RemoveChannelAsync("nonexistent.module");
    }

    [TestMethod]
    public void GetOrCreateChannelAfterDisposeThenThrowsObjectDisposed()
    {
        _manager.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(
            () => _manager.GetOrCreateChannel("test.module", "http://localhost:50100"));
    }

    [TestMethod]
    public void DisposeTwiceThenDoesNotThrow()
    {
        _manager.Dispose();
        _manager.Dispose();
    }

    [TestMethod]
    public void GetCallOptionsThenDeadlineIsInFuture()
    {
        var before = DateTime.UtcNow;

        var options = _manager.GetCallOptions();

        Assert.IsNotNull(options.Deadline);
        Assert.IsTrue(options.Deadline > before);
    }

    [TestMethod]
    public void GetCallOptionsWithCustomTimeoutThenDeadlineReflectsTimeout()
    {
        var timeout = TimeSpan.FromMinutes(2);
        var before = DateTime.UtcNow;

        var options = _manager.GetCallOptions(timeout);

        Assert.IsNotNull(options.Deadline);
        // Deadline should be approximately 2 minutes from now
        var expectedMin = before.Add(timeout).AddSeconds(-1);
        Assert.IsTrue(options.Deadline >= expectedMin);
    }

    [TestMethod]
    public void GetCallOptionsWithCancellationTokenThenTokenIsSet()
    {
        using var cts = new CancellationTokenSource();

        var options = _manager.GetCallOptions(cancellationToken: cts.Token);

        Assert.AreEqual(cts.Token, options.CancellationToken);
    }
}
