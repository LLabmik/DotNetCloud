using DotNetCloud.Core.Server.Configuration;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class SignalROptionsTests
{
    [TestMethod]
    public void WhenCreatedThenDefaultKeepAliveIs15Seconds()
    {
        var options = new SignalROptions();

        Assert.AreEqual(15, options.KeepAliveIntervalSeconds);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultClientTimeoutIs30Seconds()
    {
        var options = new SignalROptions();

        Assert.AreEqual(30, options.ClientTimeoutSeconds);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultHandshakeTimeoutIs15Seconds()
    {
        var options = new SignalROptions();

        Assert.AreEqual(15, options.HandshakeTimeoutSeconds);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultMaxParallelInvocationsIs10()
    {
        var options = new SignalROptions();

        Assert.AreEqual(10, options.MaximumParallelInvocationsPerClient);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultMaxReceiveMessageSizeIs32KB()
    {
        var options = new SignalROptions();

        Assert.AreEqual(32 * 1024, options.MaximumReceiveMessageSize);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultMaxConnectionsIsUnlimited()
    {
        var options = new SignalROptions();

        Assert.AreEqual(0, options.MaxConnections);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultHubPathIsHubsCore()
    {
        var options = new SignalROptions();

        Assert.AreEqual("/hubs/core", options.HubPath);
    }

    [TestMethod]
    public void WhenCreatedThenDetailedErrorsAreDisabled()
    {
        var options = new SignalROptions();

        Assert.IsFalse(options.EnableDetailedErrors);
    }

    [TestMethod]
    public void WhenCreatedThenWebSocketKeepAliveIs30Seconds()
    {
        var options = new SignalROptions();

        Assert.AreEqual(30, options.WebSocketKeepAliveSeconds);
    }

    [TestMethod]
    public void WhenCreatedThenAllTransportsAreEnabled()
    {
        var options = new SignalROptions();

        Assert.IsTrue(options.EnableWebSockets);
        Assert.IsTrue(options.EnableServerSentEvents);
        Assert.IsTrue(options.EnableLongPolling);
    }

    [TestMethod]
    public void WhenCreatedThenPresenceCleanupIntervalIs60Seconds()
    {
        var options = new SignalROptions();

        Assert.AreEqual(60, options.PresenceCleanupIntervalSeconds);
    }

    [TestMethod]
    public void WhenCustomizedThenValuesArePreserved()
    {
        var options = new SignalROptions
        {
            KeepAliveIntervalSeconds = 5,
            ClientTimeoutSeconds = 10,
            HandshakeTimeoutSeconds = 5,
            MaximumParallelInvocationsPerClient = 5,
            MaximumReceiveMessageSize = 65536,
            MaxConnections = 1000,
            HubPath = "/custom/hub",
            EnableDetailedErrors = true,
            WebSocketKeepAliveSeconds = 15,
            EnableWebSockets = true,
            EnableServerSentEvents = false,
            EnableLongPolling = false,
            PresenceCleanupIntervalSeconds = 120
        };

        Assert.AreEqual(5, options.KeepAliveIntervalSeconds);
        Assert.AreEqual(10, options.ClientTimeoutSeconds);
        Assert.AreEqual(5, options.HandshakeTimeoutSeconds);
        Assert.AreEqual(5, options.MaximumParallelInvocationsPerClient);
        Assert.AreEqual(65536, options.MaximumReceiveMessageSize);
        Assert.AreEqual(1000, options.MaxConnections);
        Assert.AreEqual("/custom/hub", options.HubPath);
        Assert.IsTrue(options.EnableDetailedErrors);
        Assert.AreEqual(15, options.WebSocketKeepAliveSeconds);
        Assert.IsTrue(options.EnableWebSockets);
        Assert.IsFalse(options.EnableServerSentEvents);
        Assert.IsFalse(options.EnableLongPolling);
        Assert.AreEqual(120, options.PresenceCleanupIntervalSeconds);
    }
}
