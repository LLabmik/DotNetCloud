using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.Modules.Tracks.UI;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="ChatActivityIndicator"/> component state management
/// and <see cref="NullChatActivitySignalRService"/> null-object stub.
/// </summary>
[TestClass]
public class ChatActivityIndicatorTests
{
    // ─── Null-object stub tests ───────────────────────────────

    [TestMethod]
    public void NullStub_IsActive_ReturnsFalse()
    {
        IChatActivitySignalRService stub = new TestableNullChatActivityService();

        Assert.IsFalse(stub.IsActive);
    }

    [TestMethod]
    public void NullStub_NoEventsAreFired()
    {
        IChatActivitySignalRService stub = new TestableNullChatActivityService();
        var messageFired = false;
        var channelFired = false;

        stub.MessageReceived += (_, _, _) => messageFired = true;
        stub.ChannelChanged += (_, _) => channelFired = true;

        Assert.IsFalse(messageFired);
        Assert.IsFalse(channelFired);
    }

    // ─── Component tests (via test accessor) ──────────────────

    [TestMethod]
    public void WhenServiceIsInactive_IsChatAvailableReturnsFalse()
    {
        var service = new FakeChatActivityService(isActive: false);
        var component = CreateComponent(service);

        Assert.IsFalse(component.TestIsChatAvailable);
    }

    [TestMethod]
    public void WhenServiceIsActive_IsChatAvailableReturnsTrue()
    {
        var service = new FakeChatActivityService(isActive: true);
        var component = CreateComponent(service);

        Assert.IsTrue(component.TestIsChatAvailable);
    }

    [TestMethod]
    public void WhenMessageReceived_LatestMessageIsSet()
    {
        var service = new FakeChatActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();
        var channelId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        service.RaiseMessageReceived(channelId, senderId, timestamp);

        Assert.IsNotNull(component.TestLatestMessage);
        Assert.AreEqual(channelId, component.TestLatestMessage.ChannelId);
        Assert.AreEqual(senderId, component.TestLatestMessage.SenderUserId);
    }

    [TestMethod]
    public void WhenChannelChanged_ChannelEventIsSet()
    {
        var service = new FakeChatActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();
        var channelId = Guid.NewGuid();

        service.RaiseChannelChanged(channelId, "created");

        Assert.IsNotNull(component.TestChannelEvent);
        Assert.AreEqual(channelId, component.TestChannelEvent.ChannelId);
        Assert.AreEqual("created", component.TestChannelEvent.Action);
    }

    [TestMethod]
    public void WhenChannelEventDismissed_ChannelEventIsNull()
    {
        var service = new FakeChatActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        service.RaiseChannelChanged(Guid.NewGuid(), "deleted");
        Assert.IsNotNull(component.TestChannelEvent);

        component.SimulateDismissChannelEvent();
        Assert.IsNull(component.TestChannelEvent);
    }

    [TestMethod]
    public void WhenDisposed_EventsAreUnsubscribed()
    {
        var service = new FakeChatActivityService(isActive: true);
        var component = CreateComponent(service);
        component.SimulateOnInitialized();

        component.Dispose();

        // Fire events after disposal — should not throw or update state
        service.RaiseMessageReceived(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        service.RaiseChannelChanged(Guid.NewGuid(), "created");

        Assert.IsNull(component.TestLatestMessage);
        Assert.IsNull(component.TestChannelEvent);
    }

    [TestMethod]
    public void FormatTime_JustNow()
    {
        Assert.AreEqual("just now", TestableChatActivityIndicator.TestFormatTime(DateTime.UtcNow));
    }

    [TestMethod]
    public void FormatTime_HoursAgo()
    {
        Assert.AreEqual("3h ago", TestableChatActivityIndicator.TestFormatTime(DateTime.UtcNow.AddHours(-3)));
    }

    // ─── Helpers ──────────────────────────────────────────────

    private static TestableChatActivityIndicator CreateComponent(FakeChatActivityService service)
    {
        return new TestableChatActivityIndicator(service);
    }

    /// <summary>
    /// Test accessor subclass exposing protected members of <see cref="ChatActivityIndicator"/>.
    /// </summary>
    private sealed class TestableChatActivityIndicator : ChatActivityIndicator
    {
        public TestableChatActivityIndicator(IChatActivitySignalRService service)
        {
            var prop = typeof(ChatActivityIndicator).GetProperty("ChatActivity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prop!.SetValue(this, service);
        }

        public bool TestIsChatAvailable => IsChatAvailable;

        public MessageEvent? TestLatestMessage => GetType()
            .BaseType!.GetField("_latestMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this) as MessageEvent;

        public ChannelEvent? TestChannelEvent => GetType()
            .BaseType!.GetField("_channelEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(this) as ChannelEvent;

        public void SimulateOnInitialized() => OnInitialized();
        public void SimulateDismissChannelEvent() => DismissChannelEvent();

        public static string TestFormatTime(DateTime utcTime) => FormatTime(utcTime);
    }

    /// <summary>
    /// Fake service for testing — allows raising events on demand.
    /// </summary>
    private sealed class FakeChatActivityService : IChatActivitySignalRService
    {
        public FakeChatActivityService(bool isActive) => IsActive = isActive;

        public bool IsActive { get; }
        public event Action<Guid, Guid, DateTime>? MessageReceived;
        public event Action<Guid, string>? ChannelChanged;

        public void RaiseMessageReceived(Guid channelId, Guid senderId, DateTime timestamp) =>
            MessageReceived?.Invoke(channelId, senderId, timestamp);
        public void RaiseChannelChanged(Guid channelId, string action) =>
            ChannelChanged?.Invoke(channelId, action);
    }

    /// <summary>
    /// Testable null stub — matches the null-object contract.
    /// </summary>
    private sealed class TestableNullChatActivityService : IChatActivitySignalRService
    {
        public bool IsActive => false;
#pragma warning disable CS0067
        public event Action<Guid, Guid, DateTime>? MessageReceived;
        public event Action<Guid, string>? ChannelChanged;
#pragma warning restore CS0067
    }
}
