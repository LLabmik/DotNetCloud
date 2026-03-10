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
    public void WhenTotalUnreadIsNonZeroThenHasMentionsIsTrue()
    {
        var badge = new TestableBadge();

        badge.ApplyUnreadCountUpdate(Guid.NewGuid(), 1);

        Assert.IsTrue(badge.TestHasMentions);
    }
}
