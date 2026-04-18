using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="IncomingCallNotification"/> initials delegation and callbacks.
/// </summary>
[TestClass]
public class IncomingCallNotificationTests
{
    // ── GetInitials Delegation Tests ────────────────────────────────

    [TestMethod]
    public void GetInitials_DelegatesToVideoCallDialog()
    {
        Assert.AreEqual("JD", IncomingCallNotification.GetInitials("John Doe"));
    }

    [TestMethod]
    public void GetInitials_WithNull_ReturnsQuestionMark()
    {
        Assert.AreEqual("?", IncomingCallNotification.GetInitials(null));
    }

    [TestMethod]
    public void GetInitials_WithSingleName_ReturnsTwoChars()
    {
        Assert.AreEqual("AL", IncomingCallNotification.GetInitials("Alice"));
    }

    // ── Event Callback Tests ────────────────────────────────────────

    [TestMethod]
    public async Task HandleAcceptVideo_InvokesOnAcceptVideoCallback()
    {
        var notification = new TestableIncomingCallNotification();
        var invoked = false;
        var receiver = new object();
        notification.OnAcceptVideo = EventCallback.Factory.Create(receiver, () => invoked = true);

        await notification.InvokeAcceptVideo();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleAcceptAudio_InvokesOnAcceptAudioCallback()
    {
        var notification = new TestableIncomingCallNotification();
        var invoked = false;
        var receiver = new object();
        notification.OnAcceptAudio = EventCallback.Factory.Create(receiver, () => invoked = true);

        await notification.InvokeAcceptAudio();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleReject_InvokesOnRejectCallback()
    {
        var notification = new TestableIncomingCallNotification();
        var invoked = false;
        var receiver = new object();
        notification.OnReject = EventCallback.Factory.Create(receiver, () => invoked = true);

        await notification.InvokeReject();

        Assert.IsTrue(invoked);
    }

    // ── Parameter Tests ─────────────────────────────────────────────

    [TestMethod]
    public void DefaultMediaType_IsAudio()
    {
        var notification = new TestableIncomingCallNotification();

        Assert.AreEqual("Audio", notification.TestMediaType);
    }

    [TestMethod]
    public void DefaultIsRinging_IsTrue()
    {
        var notification = new TestableIncomingCallNotification();

        Assert.IsTrue(notification.TestIsRinging);
    }

    [TestMethod]
    public void WhenRemainingSecondsSet_ValueIsAccessible()
    {
        var notification = new TestableIncomingCallNotification();
        notification.SetRemainingSeconds(25);

        Assert.AreEqual(25, notification.TestRemainingSeconds);
    }

    [TestMethod]
    public void WhenCallerNameSet_ValueIsAccessible()
    {
        var notification = new TestableIncomingCallNotification();
        notification.SetCallerName("Alice Smith");

        Assert.AreEqual("Alice Smith", notification.TestCallerName);
    }

    [TestMethod]
    public void WhenChannelNameSet_ValueIsAccessible()
    {
        var notification = new TestableIncomingCallNotification();
        notification.SetChannelName("general");

        Assert.AreEqual("general", notification.TestChannelName);
    }

    [TestMethod]
    public void WhenMediaTypeSetToVideo_ValueIsAccessible()
    {
        var notification = new TestableIncomingCallNotification();
        notification.SetMediaType("Video");

        Assert.AreEqual("Video", notification.TestMediaType);
    }

    [TestMethod]
    public void IsMidCallInvite_DefaultIsFalse()
    {
        var notification = new TestableIncomingCallNotification();

        Assert.IsFalse(notification.TestIsMidCallInvite);
    }

    [TestMethod]
    public void ParticipantCount_DefaultIsZero()
    {
        var notification = new TestableIncomingCallNotification();

        Assert.AreEqual(0, notification.TestParticipantCount);
    }

    [TestMethod]
    public void MidCallInviteParameters_WhenSet_AreAccessible()
    {
        var notification = new TestableIncomingCallNotification();
        notification.SetIsMidCallInvite(true);
        notification.SetParticipantCount(4);

        Assert.IsTrue(notification.TestIsMidCallInvite);
        Assert.AreEqual(4, notification.TestParticipantCount);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private sealed class TestableIncomingCallNotification : IncomingCallNotification
    {
        public TestableIncomingCallNotification()
        {
            // Inject a mock IJSRuntime so StopRingtoneAsync doesn't throw
            var jsProp = typeof(IncomingCallNotification)
                .GetProperty("JS", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            jsProp.SetValue(this, new Mock<IJSRuntime>().Object);
        }

        public string TestMediaType => MediaType;
        public bool TestIsRinging => IsRinging;
        public int TestRemainingSeconds => RemainingSeconds;
        public string TestCallerName => CallerName;
        public string TestChannelName => ChannelName;
        public bool TestIsMidCallInvite => IsMidCallInvite;
        public int TestParticipantCount => ParticipantCount;

        public void SetRemainingSeconds(int seconds) => RemainingSeconds = seconds;
        public void SetCallerName(string name) => CallerName = name;
        public void SetChannelName(string name) => ChannelName = name;
        public void SetMediaType(string type) => MediaType = type;
        public void SetIsMidCallInvite(bool value) => IsMidCallInvite = value;
        public void SetParticipantCount(int count) => ParticipantCount = count;

        public Task InvokeAcceptVideo() => HandleAcceptVideo();
        public Task InvokeAcceptAudio() => HandleAcceptAudio();
        public Task InvokeReject() => HandleReject();
    }
}
