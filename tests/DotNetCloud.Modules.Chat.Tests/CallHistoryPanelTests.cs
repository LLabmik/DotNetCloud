using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="CallHistoryPanel"/> outcome formatting, duration formatting,
/// time formatting, call icons, and callbacks.
/// </summary>
[TestClass]
public class CallHistoryPanelTests
{
    // ── GetOutcomeClass Tests ───────────────────────────────────────

    [TestMethod]
    public void GetOutcomeClass_Ended_ReturnsOutcomeEnded()
    {
        Assert.AreEqual("outcome-ended", CallHistoryPanel.GetOutcomeClass("Ended"));
    }

    [TestMethod]
    public void GetOutcomeClass_Missed_ReturnsOutcomeMissed()
    {
        Assert.AreEqual("outcome-missed", CallHistoryPanel.GetOutcomeClass("Missed"));
    }

    [TestMethod]
    public void GetOutcomeClass_Rejected_ReturnsOutcomeRejected()
    {
        Assert.AreEqual("outcome-rejected", CallHistoryPanel.GetOutcomeClass("Rejected"));
    }

    [TestMethod]
    public void GetOutcomeClass_Failed_ReturnsOutcomeFailed()
    {
        Assert.AreEqual("outcome-failed", CallHistoryPanel.GetOutcomeClass("Failed"));
    }

    [TestMethod]
    public void GetOutcomeClass_Unknown_ReturnsOutcomeOther()
    {
        Assert.AreEqual("outcome-other", CallHistoryPanel.GetOutcomeClass("Active"));
    }

    // ── GetCallIcon Tests ───────────────────────────────────────────

    [TestMethod]
    public void GetCallIcon_MissedCall_ReturnsMissedIcon()
    {
        var call = CreateCall(state: "Missed");
        Assert.AreEqual("📵", CallHistoryPanel.GetCallIcon(call));
    }

    [TestMethod]
    public void GetCallIcon_RejectedCall_ReturnsRejectedIcon()
    {
        var call = CreateCall(state: "Rejected");
        Assert.AreEqual("🚫", CallHistoryPanel.GetCallIcon(call));
    }

    [TestMethod]
    public void GetCallIcon_FailedCall_ReturnsWarningIcon()
    {
        var call = CreateCall(state: "Failed");
        Assert.AreEqual("⚠️", CallHistoryPanel.GetCallIcon(call));
    }

    [TestMethod]
    public void GetCallIcon_EndedVideoCall_ReturnsVideoIcon()
    {
        var call = CreateCall(state: "Ended", mediaType: "Video");
        Assert.AreEqual("📹", CallHistoryPanel.GetCallIcon(call));
    }

    [TestMethod]
    public void GetCallIcon_EndedAudioCall_ReturnsPhoneIcon()
    {
        var call = CreateCall(state: "Ended", mediaType: "Audio");
        Assert.AreEqual("📞", CallHistoryPanel.GetCallIcon(call));
    }

    // ── FormatOutcome Tests ─────────────────────────────────────────

    [TestMethod]
    public void FormatOutcome_Ended_ReturnsCompleted()
    {
        Assert.AreEqual("Completed", CallHistoryPanel.FormatOutcome("Ended"));
    }

    [TestMethod]
    public void FormatOutcome_Missed_ReturnsMissed()
    {
        Assert.AreEqual("Missed", CallHistoryPanel.FormatOutcome("Missed"));
    }

    [TestMethod]
    public void FormatOutcome_Rejected_ReturnsRejected()
    {
        Assert.AreEqual("Rejected", CallHistoryPanel.FormatOutcome("Rejected"));
    }

    [TestMethod]
    public void FormatOutcome_Failed_ReturnsFailed()
    {
        Assert.AreEqual("Failed", CallHistoryPanel.FormatOutcome("Failed"));
    }

    [TestMethod]
    public void FormatOutcome_Active_ReturnsActive()
    {
        Assert.AreEqual("Active", CallHistoryPanel.FormatOutcome("Active"));
    }

    [TestMethod]
    public void FormatOutcome_Ringing_ReturnsRinging()
    {
        Assert.AreEqual("Ringing", CallHistoryPanel.FormatOutcome("Ringing"));
    }

    [TestMethod]
    public void FormatOutcome_Unknown_ReturnsAsIs()
    {
        Assert.AreEqual("SomeState", CallHistoryPanel.FormatOutcome("SomeState"));
    }

    // ── FormatDuration Tests ────────────────────────────────────────

    [TestMethod]
    public void FormatDuration_UnderOneMinute_ShowsSeconds()
    {
        Assert.AreEqual("45s", CallHistoryPanel.FormatDuration(45));
    }

    [TestMethod]
    public void FormatDuration_ExactMinutes_ShowsMinutesOnly()
    {
        Assert.AreEqual("5m", CallHistoryPanel.FormatDuration(300));
    }

    [TestMethod]
    public void FormatDuration_MinutesAndSeconds_ShowsBoth()
    {
        Assert.AreEqual("5m 30s", CallHistoryPanel.FormatDuration(330));
    }

    [TestMethod]
    public void FormatDuration_ExactHours_ShowsHoursOnly()
    {
        Assert.AreEqual("2h", CallHistoryPanel.FormatDuration(7200));
    }

    [TestMethod]
    public void FormatDuration_HoursAndMinutes_ShowsBoth()
    {
        Assert.AreEqual("1h 30m", CallHistoryPanel.FormatDuration(5400));
    }

    [TestMethod]
    public void FormatDuration_OneSecond_Shows1s()
    {
        Assert.AreEqual("1s", CallHistoryPanel.FormatDuration(1));
    }

    // ── FormatCallTime Tests ────────────────────────────────────────

    [TestMethod]
    public void FormatCallTime_JustNow_ReturnsJustNow()
    {
        var time = DateTime.UtcNow.AddSeconds(-30);
        Assert.AreEqual("Just now", CallHistoryPanel.FormatCallTime(time));
    }

    [TestMethod]
    public void FormatCallTime_FiveMinutesAgo_Returns5mAgo()
    {
        var time = DateTime.UtcNow.AddMinutes(-5);
        Assert.AreEqual("5m ago", CallHistoryPanel.FormatCallTime(time));
    }

    [TestMethod]
    public void FormatCallTime_ThreeHoursAgo_Returns3hAgo()
    {
        var time = DateTime.UtcNow.AddHours(-3);
        Assert.AreEqual("3h ago", CallHistoryPanel.FormatCallTime(time));
    }

    [TestMethod]
    public void FormatCallTime_TwoDaysAgo_Returns2dAgo()
    {
        var time = DateTime.UtcNow.AddDays(-2);
        Assert.AreEqual("2d ago", CallHistoryPanel.FormatCallTime(time));
    }

    [TestMethod]
    public void FormatCallTime_TwoWeeksAgo_ReturnsFormattedDate()
    {
        var time = DateTime.UtcNow.AddDays(-14);
        var expected = time.ToString("MMM d, yyyy");
        Assert.AreEqual(expected, CallHistoryPanel.FormatCallTime(time));
    }

    // ── Event Callback Tests ────────────────────────────────────────

    [TestMethod]
    public async Task HandleClose_InvokesOnCloseCallback()
    {
        var panel = new TestableCallHistoryPanel();
        var invoked = false;
        var receiver = new object();
        panel.OnClose = EventCallback.Factory.Create(receiver, () => invoked = true);

        await panel.InvokeClose();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleLoadMore_InvokesOnLoadMoreCallback()
    {
        var panel = new TestableCallHistoryPanel();
        var invoked = false;
        var receiver = new object();
        panel.OnLoadMore = EventCallback.Factory.Create(receiver, () => invoked = true);

        await panel.InvokeLoadMore();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task HandleCallBack_InvokesOnCallBackWithMediaType()
    {
        var panel = new TestableCallHistoryPanel();
        string? receivedMediaType = null;
        var receiver = new object();
        panel.OnCallBack = EventCallback.Factory.Create<string>(receiver, mt => receivedMediaType = mt);

        var call = CreateCall(mediaType: "Video");
        await panel.InvokeCallBack(call);

        Assert.AreEqual("Video", receivedMediaType);
    }

    [TestMethod]
    public async Task HandleCallBack_AudioCall_PassesAudioMediaType()
    {
        var panel = new TestableCallHistoryPanel();
        string? receivedMediaType = null;
        var receiver = new object();
        panel.OnCallBack = EventCallback.Factory.Create<string>(receiver, mt => receivedMediaType = mt);

        var call = CreateCall(mediaType: "Audio");
        await panel.InvokeCallBack(call);

        Assert.AreEqual("Audio", receivedMediaType);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static CallHistoryDto CreateCall(
        string state = "Ended",
        string mediaType = "Audio",
        int? durationSeconds = 120,
        int participantCount = 2)
    {
        return new CallHistoryDto
        {
            Id = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            InitiatorUserId = Guid.NewGuid(),
            InitiatorDisplayName = "Test User",
            State = state,
            MediaType = mediaType,
            DurationSeconds = durationSeconds,
            ParticipantCount = participantCount,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
    }

    private sealed class TestableCallHistoryPanel : CallHistoryPanel
    {
        public Task InvokeClose() => HandleClose();
        public Task InvokeLoadMore() => HandleLoadMore();
        public Task InvokeCallBack(CallHistoryDto call) => HandleCallBack(call);
    }
}
