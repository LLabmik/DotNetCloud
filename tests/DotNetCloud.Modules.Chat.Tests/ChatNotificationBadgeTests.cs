using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatNotificationBadge"/> unread-count state management.
/// </summary>
[TestClass]
public class ChatNotificationBadgeTests
{
    /// <summary>
    /// Test accessor subclass that exposes protected computed properties.
    /// </summary>
    private sealed class TestableBadge : ChatNotificationBadge
    {
        public int TestTotalUnread => TotalUnread;
        public int TestTotalMentions => TotalMentions;
        public bool TestHasMentions => HasMentions;
    }

    [TestMethod]
    public void WhenNoUpdatesThenTotalUnreadIsZero()
    {
        var badge = new TestableBadge();

        Assert.AreEqual(0, badge.TestTotalUnread);
    }

    [TestMethod]
    public void WhenUnreadCountUpdatedThenTotalUnreadReflectsCount()
    {
        var badge = new TestableBadge();
        var channelId = Guid.NewGuid();

        badge.ApplyUnreadCountUpdate(channelId, 5);

        Assert.AreEqual(5, badge.TestTotalUnread);
    }

    [TestMethod]
    public void WhenMultipleChannelsUpdatedThenTotalUnreadSumsAll()
    {
        var badge = new TestableBadge();

        badge.ApplyUnreadCountUpdate(Guid.NewGuid(), 3);
        badge.ApplyUnreadCountUpdate(Guid.NewGuid(), 7);

        Assert.AreEqual(10, badge.TestTotalUnread);
    }

    [TestMethod]
    public void WhenSameChannelUpdatedTwiceThenLatestCountReplacesPrevious()
    {
        var badge = new TestableBadge();
        var channelId = Guid.NewGuid();

        badge.ApplyUnreadCountUpdate(channelId, 5);
        badge.ApplyUnreadCountUpdate(channelId, 2);

        Assert.AreEqual(2, badge.TestTotalUnread);
    }

    [TestMethod]
    public void WhenChannelResetToZeroThenTotalUnreadExcludesThatChannel()
    {
        var badge = new TestableBadge();
        var channel1 = Guid.NewGuid();
        var channel2 = Guid.NewGuid();

        badge.ApplyUnreadCountUpdate(channel1, 4);
        badge.ApplyUnreadCountUpdate(channel2, 6);
        badge.ApplyUnreadCountUpdate(channel1, 0);

        Assert.AreEqual(6, badge.TestTotalUnread);
    }

    [TestMethod]
    public void WhenTotalUnreadIsZeroThenHasMentionsIsFalse()
    {
        var badge = new TestableBadge();

        Assert.IsFalse(badge.TestHasMentions);
    }

    [TestMethod]
    public void WhenMentionCountIsNonZeroThenHasMentionsIsTrue()
    {
        var badge = new TestableBadge();

        badge.ApplyMentionCountUpdate(Guid.NewGuid(), 1);

        Assert.IsTrue(badge.TestHasMentions);
    }

    [TestMethod]
    public void WhenMentionCountUpdatedThenHasMentionsIsTrue()
    {
        var badge = new TestableBadge();

        badge.ApplyMentionCountUpdate(Guid.NewGuid(), 2);

        Assert.IsTrue(badge.TestHasMentions);
    }

    [TestMethod]
    public void WhenMentionCountResetToZeroThenHasMentionsIsFalse()
    {
        var badge = new TestableBadge();
        var channelId = Guid.NewGuid();

        badge.ApplyMentionCountUpdate(channelId, 3);
        badge.ApplyMentionCountUpdate(channelId, 0);

        Assert.IsFalse(badge.TestHasMentions);
    }

    [TestMethod]
    public void WhenOnlyUnreadWithNoMentionsThenHasMentionsIsFalse()
    {
        var badge = new TestableBadge();

        badge.ApplyUnreadCountUpdate(Guid.NewGuid(), 5);

        Assert.IsFalse(badge.TestHasMentions);
    }

    [TestMethod]
    public void WhenMultipleChannelMentionCountsThenTotalMentionsSumsAll()
    {
        var badge = new TestableBadge();

        badge.ApplyMentionCountUpdate(Guid.NewGuid(), 2);
        badge.ApplyMentionCountUpdate(Guid.NewGuid(), 3);

        Assert.AreEqual(5, badge.TestTotalMentions);
    }
}

