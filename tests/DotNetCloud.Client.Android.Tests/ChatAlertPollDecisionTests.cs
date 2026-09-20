using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests;

/// <summary>
/// Tests for <see cref="ChatAlertPollDecision"/>: which aggregates alert, with which wording, and the
/// no-double-alert high-water mark (plan §12.9).
/// </summary>
[TestClass]
public class ChatAlertPollDecisionTests
{
    private static readonly DateTime Changed = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ChatAlertsSummary Summary(
        int unmutedUnread = 1,
        int unmutedMentions = 0,
        int unread = 1,
        int mentions = 0,
        Guid? topChannelId = null,
        DateTime? changedAt = null) => new()
        {
            Unread = unread,
            Mentions = mentions,
            UnmutedUnread = unmutedUnread,
            UnmutedMentions = unmutedMentions,
            TopChannelId = topChannelId,
            ChangedAt = changedAt ?? Changed,
        };

    [TestMethod]
    public void Decide_WhenSummaryIsNull_ThenNoAlert()
    {
        var decision = ChatAlertPollDecision.Decide(null, null);

        Assert.IsFalse(decision.ShouldAlert);
        Assert.IsNull(decision.AcknowledgedChangedAtUtc);
    }

    [TestMethod]
    public void Decide_WhenNothingUnmutedIsUnread_ThenNoAlert()
    {
        // Everything unread lives in muted channels: counted by the server, never alerted on.
        var decision = ChatAlertPollDecision.Decide(
            Summary(unmutedUnread: 0, unread: 4, mentions: 2), null);

        Assert.IsFalse(decision.ShouldAlert);
    }

    [TestMethod]
    public void Decide_WhenUnreadAndNeverAcknowledged_ThenAlertsWithMessageWording()
    {
        var channelId = Guid.CreateVersion7();

        var decision = ChatAlertPollDecision.Decide(
            Summary(topChannelId: channelId), lastAcknowledgedChangedAtUtc: null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(NotificationPayloadContract.PayloadTypeMessage, decision.PayloadType);
        Assert.AreEqual(channelId, decision.ChannelId);
        Assert.AreEqual(Changed, decision.AcknowledgedChangedAtUtc);
    }

    [TestMethod]
    public void Decide_WhenUnreadMentionExists_ThenAlertsWithMentionWording()
    {
        var decision = ChatAlertPollDecision.Decide(
            Summary(unmutedMentions: 1, mentions: 1), null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(NotificationPayloadContract.PayloadTypeMention, decision.PayloadType);
    }

    [TestMethod]
    public void Decide_WhenMentionIsOnlyInAMutedChannel_ThenMessageWording()
    {
        // The server reports the mention in the raw count but not in the unmuted subtotal, so the
        // wording must stay generic — a muted channel can never surface as "you were mentioned".
        var decision = ChatAlertPollDecision.Decide(
            Summary(unmutedUnread: 1, unmutedMentions: 0, unread: 2, mentions: 1), null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(NotificationPayloadContract.PayloadTypeMessage, decision.PayloadType);
    }

    [TestMethod]
    public void Decide_WhenChangeWasAlreadyAcknowledged_ThenNoAlert()
    {
        var decision = ChatAlertPollDecision.Decide(Summary(), lastAcknowledgedChangedAtUtc: Changed);

        Assert.IsFalse(decision.ShouldAlert);
    }

    [TestMethod]
    public void Decide_WhenAcknowledgedChangeIsNewer_ThenNoAlert()
    {
        var decision = ChatAlertPollDecision.Decide(
            Summary(), lastAcknowledgedChangedAtUtc: Changed.AddMinutes(10));

        Assert.IsFalse(decision.ShouldAlert);
    }

    [TestMethod]
    public void Decide_WhenChangeIsNewerThanAcknowledged_ThenAlerts()
    {
        var decision = ChatAlertPollDecision.Decide(
            Summary(), lastAcknowledgedChangedAtUtc: Changed.AddMinutes(-10));

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(Changed, decision.AcknowledgedChangedAtUtc);
    }

    [TestMethod]
    public void Decide_WhenReadingChangesNothing_ThenNoSecondAlert()
    {
        // Reading a channel clears unread counts but never moves the newest-message time, so a poll
        // right after the user reads must not re-alert the messages they just saw.
        var afterReading = Summary(unmutedUnread: 0, unread: 0, changedAt: Changed);

        var decision = ChatAlertPollDecision.Decide(afterReading, lastAcknowledgedChangedAtUtc: Changed);

        Assert.IsFalse(decision.ShouldAlert);
    }

    [TestMethod]
    public void Decide_WhenChangedAtIsMissing_ThenNoAlert()
    {
        // Built inline rather than via Summary(), which always supplies a timestamp.
        var summary = new ChatAlertsSummary { UnmutedUnread = 1, ChangedAt = null };

        var decision = ChatAlertPollDecision.Decide(summary, lastAcknowledgedChangedAtUtc: null);

        Assert.IsFalse(decision.ShouldAlert, "without a change value there is nothing to key the alert on");
    }

    [TestMethod]
    public void Decide_WhenTimestampArrivesWithoutKind_ThenTreatedAsUtc()
    {
        // JSON deserialization drops DateTimeKind. Reading it as local time would shift the high-water
        // mark by the UTC offset — the trap that once made calendar alarms fire hours late.
        var unspecified = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var decision = ChatAlertPollDecision.Decide(
            Summary(changedAt: unspecified), lastAcknowledgedChangedAtUtc: null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(DateTimeKind.Utc, decision.AcknowledgedChangedAtUtc!.Value.Kind);
        Assert.AreEqual(Changed, decision.AcknowledgedChangedAtUtc);
    }

    [TestMethod]
    public void Decide_WhenTimestampArrivesAsLocal_ThenConvertedToUtc()
    {
        var local = new DateTime(2026, 1, 1, 7, 0, 0, DateTimeKind.Local);

        var decision = ChatAlertPollDecision.Decide(
            Summary(changedAt: local), lastAcknowledgedChangedAtUtc: null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.AreEqual(DateTimeKind.Utc, decision.AcknowledgedChangedAtUtc!.Value.Kind);
        Assert.AreEqual(local.ToUniversalTime(), decision.AcknowledgedChangedAtUtc);
    }

    [TestMethod]
    public void Decide_WhenNoTapTargetIsKnown_ThenStillAlerts()
    {
        var decision = ChatAlertPollDecision.Decide(
            Summary(topChannelId: null), lastAcknowledgedChangedAtUtc: null);

        Assert.IsTrue(decision.ShouldAlert);
        Assert.IsNull(decision.ChannelId);
    }

    [TestMethod]
    public void EnsureUtc_WhenNull_ThenNull()
    {
        Assert.IsNull(ChatAlertPollDecision.EnsureUtc(null));
    }
}
