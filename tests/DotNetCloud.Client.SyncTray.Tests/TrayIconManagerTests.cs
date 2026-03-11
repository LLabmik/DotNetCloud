using DotNetCloud.Client.SyncTray;

namespace DotNetCloud.Client.SyncTray.Tests;

[TestClass]
public sealed class TrayIconManagerTests
{
    [TestMethod]
    public void GetChatBadgeKind_WhenNoUnreadAndNoMentions_ReturnsNone()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 0, chatHasMentions: false);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.None, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenUnreadWithoutMentions_ReturnsUnread()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 3, chatHasMentions: false);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Unread, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenMentionsPresent_ReturnsMention()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 1, chatHasMentions: true);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Mention, result);
    }

    [TestMethod]
    public void GetChatBadgeKind_WhenMentionsTrueAndUnreadZero_ReturnsMention()
    {
        var result = TrayIconManager.GetChatBadgeKind(chatUnreadCount: 0, chatHasMentions: true);
        Assert.AreEqual(TrayIconManager.TrayChatBadgeKind.Mention, result);
    }
}
