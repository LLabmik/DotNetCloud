using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="CallControls"/> duration formatting, quality indicators, and callbacks.
/// </summary>
[TestClass]
public class CallControlsTests
{
    // ── FormatDuration Tests ────────────────────────────────────────

    [TestMethod]
    public void FormatDuration_ZeroSeconds_Returns00Colon00()
    {
        Assert.AreEqual("00:00", CallControls.FormatDuration(0));
    }

    [TestMethod]
    public void FormatDuration_UnderOneMinute_FormatsAsMMSS()
    {
        Assert.AreEqual("00:45", CallControls.FormatDuration(45));
    }

    [TestMethod]
    public void FormatDuration_ExactlyOneMinute_Returns01Colon00()
    {
        Assert.AreEqual("01:00", CallControls.FormatDuration(60));
    }

    [TestMethod]
    public void FormatDuration_MinutesAndSeconds_FormatsCorrectly()
    {
        Assert.AreEqual("05:30", CallControls.FormatDuration(330));
    }

    [TestMethod]
    public void FormatDuration_UnderOneHour_FormatsAsMMSS()
    {
        Assert.AreEqual("59:59", CallControls.FormatDuration(3599));
    }

    [TestMethod]
    public void FormatDuration_ExactlyOneHour_FormatsAsHMMSS()
    {
        Assert.AreEqual("1:00:00", CallControls.FormatDuration(3600));
    }

    [TestMethod]
    public void FormatDuration_HoursMinutesSeconds_FormatsCorrectly()
    {
        Assert.AreEqual("2:15:30", CallControls.FormatDuration(8130));
    }

    [TestMethod]
    public void FormatDuration_NegativeSeconds_ClampsToZero()
    {
        Assert.AreEqual("00:00", CallControls.FormatDuration(-10));
    }

    [TestMethod]
    public void FormatDuration_LargeValue_FormatsCorrectly()
    {
        Assert.AreEqual("27:46:40", CallControls.FormatDuration(100000));
    }

    [TestMethod]
    public void FormatDuration_OneSecond_Returns00Colon01()
    {
        Assert.AreEqual("00:01", CallControls.FormatDuration(1));
    }

    // ── GetQualityIndicator Tests ───────────────────────────────────

    [TestMethod]
    public void GetQualityIndicator_Good_ReturnsGreenCircle()
    {
        var controls = new TestableCallControls();
        controls.SetConnectionQuality("Good");

        Assert.AreEqual("🟢", controls.TestGetQualityIndicator());
    }

    [TestMethod]
    public void GetQualityIndicator_Fair_ReturnsYellowCircle()
    {
        var controls = new TestableCallControls();
        controls.SetConnectionQuality("Fair");

        Assert.AreEqual("🟡", controls.TestGetQualityIndicator());
    }

    [TestMethod]
    public void GetQualityIndicator_Poor_ReturnsRedCircle()
    {
        var controls = new TestableCallControls();
        controls.SetConnectionQuality("Poor");

        Assert.AreEqual("🔴", controls.TestGetQualityIndicator());
    }

    [TestMethod]
    public void GetQualityIndicator_Unknown_ReturnsWhiteCircle()
    {
        var controls = new TestableCallControls();
        controls.SetConnectionQuality("Unknown");

        Assert.AreEqual("⚪", controls.TestGetQualityIndicator());
    }

    [TestMethod]
    public void GetQualityIndicator_CaseInsensitive_Works()
    {
        var controls = new TestableCallControls();
        controls.SetConnectionQuality("GOOD");

        Assert.AreEqual("🟢", controls.TestGetQualityIndicator());
    }

    // ── FormattedDuration Property Test ─────────────────────────────

    [TestMethod]
    public void FormattedDuration_ReflectsDurationSecondsParameter()
    {
        var controls = new TestableCallControls();
        controls.SetDurationSeconds(125);

        Assert.AreEqual("02:05", controls.TestFormattedDuration);
    }

    // ── Event Callback Tests ────────────────────────────────────────

    [TestMethod]
    public async Task HandleToggleMute_InvokesCallbackWithOppositeOfCurrentState()
    {
        var controls = new TestableCallControls();
        controls.SetMuted(false);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleMute = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleMute();

        Assert.AreEqual(true, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleMute_WhenMuted_InvokesCallbackWithFalse()
    {
        var controls = new TestableCallControls();
        controls.SetMuted(true);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleMute = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleMute();

        Assert.AreEqual(false, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleCamera_InvokesCallbackWithOppositeOfCurrentState()
    {
        var controls = new TestableCallControls();
        controls.SetCameraOff(false);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleCamera = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleCamera();

        Assert.AreEqual(true, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleCamera_WhenCameraOff_InvokesCallbackWithFalse()
    {
        var controls = new TestableCallControls();
        controls.SetCameraOff(true);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleCamera = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleCamera();

        Assert.AreEqual(false, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleScreenShare_InvokesCallbackWithOppositeOfCurrentState()
    {
        var controls = new TestableCallControls();
        controls.SetScreenSharing(false);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleScreenShare = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleScreenShare();

        Assert.AreEqual(true, receivedValue);
    }

    [TestMethod]
    public async Task HandleToggleScreenShare_WhenSharing_InvokesCallbackWithFalse()
    {
        var controls = new TestableCallControls();
        controls.SetScreenSharing(true);
        bool? receivedValue = null;
        var receiver = new object();
        controls.OnToggleScreenShare = EventCallback.Factory.Create<bool>(receiver, val => receivedValue = val);

        await controls.InvokeToggleScreenShare();

        Assert.AreEqual(false, receivedValue);
    }

    [TestMethod]
    public async Task HandleHangUp_InvokesOnHangUpCallback()
    {
        var controls = new TestableCallControls();
        var invoked = false;
        var receiver = new object();
        controls.OnHangUp = EventCallback.Factory.Create(receiver, () => invoked = true);

        await controls.InvokeHangUp();

        Assert.IsTrue(invoked);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private sealed class TestableCallControls : CallControls
    {
        public string TestGetQualityIndicator() => GetQualityIndicator();
        public string TestFormattedDuration => FormattedDuration;

        public void SetConnectionQuality(string? quality) => ConnectionQuality = quality;
        public void SetDurationSeconds(int seconds) => DurationSeconds = seconds;
        public void SetMuted(bool muted) => IsMuted = muted;
        public void SetCameraOff(bool off) => IsCameraOff = off;
        public void SetScreenSharing(bool sharing) => IsScreenSharing = sharing;

        public Task InvokeToggleMute() => HandleToggleMute();
        public Task InvokeToggleCamera() => HandleToggleCamera();
        public Task InvokeToggleScreenShare() => HandleToggleScreenShare();
        public Task InvokeHangUp() => HandleHangUp();
    }
}
