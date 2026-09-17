using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// Tests for <see cref="ChatAlertPolicy"/> — the rule that decides whether an incoming chat
/// message dings the user, posts a system notification, or stays silent.
/// </summary>
[TestClass]
public sealed class ChatAlertPolicyTests
{
    private static readonly Guid CurrentUser = Guid.Parse("587d777a-4793-4248-2184-08deb47250fa");
    private static readonly Guid OtherUser = Guid.Parse("019f11a9-a1f3-7884-adad-6a117ab162eb");

    [TestMethod]
    public void Decide_ForegroundRemoteMessage_PlaysInAppSound()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: true,
            channelMuted: false,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.InAppSound, alert);
    }

    [TestMethod]
    public void Decide_ForegroundOwnMessageEcho_IsSilent()
    {
        // The server echoes a sent message back to the sender's connections; that must not ding.
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: true,
            channelMuted: false,
            senderUserId: CurrentUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.None, alert);
    }

    [TestMethod]
    public void Decide_ForegroundSoundDisabled_IsSilent()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: false,
            channelMuted: false,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.None, alert);
    }

    [TestMethod]
    public void Decide_MutedChannel_IsSilent_EvenInForeground()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: true,
            channelMuted: true,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.None, alert);
    }

    [TestMethod]
    public void Decide_MutedChannel_IsSilent_EvenInBackground()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: false,
            soundEnabled: true,
            channelMuted: true,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.None, alert);
    }

    [TestMethod]
    public void Decide_BackgroundRemoteMessage_PostsSystemNotification()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: false,
            soundEnabled: true,
            channelMuted: false,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.SystemNotification, alert);
    }

    [TestMethod]
    public void Decide_BackgroundRemoteMessage_StillNotifies_WhenInAppSoundDisabled()
    {
        // The in-app ding preference must not affect background delivery — the notification
        // carries its own sound and is the only alert possible while the app is not visible.
        var alert = ChatAlertPolicy.Decide(
            isForeground: false,
            soundEnabled: false,
            channelMuted: false,
            senderUserId: OtherUser,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.SystemNotification, alert);
    }

    [TestMethod]
    public void Decide_ForegroundWithUnknownSender_IsSilent()
    {
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: true,
            channelMuted: false,
            senderUserId: Guid.Empty,
            currentUserId: CurrentUser);

        Assert.AreEqual(ChatAlertKind.None, alert);
    }

    [TestMethod]
    public void Decide_ForegroundWithUnresolvedCurrentUser_StillDings()
    {
        // An unreadable id_token leaves the current user unknown; that must not silence alerts.
        var alert = ChatAlertPolicy.Decide(
            isForeground: true,
            soundEnabled: true,
            channelMuted: false,
            senderUserId: OtherUser,
            currentUserId: Guid.Empty);

        Assert.AreEqual(ChatAlertKind.InAppSound, alert);
    }

    [TestMethod]
    public void Decide_ExactlyOneAlertKind_IsEverReturned()
    {
        foreach (var isForeground in new[] { true, false })
        {
            foreach (var soundEnabled in new[] { true, false })
            {
                foreach (var muted in new[] { true, false })
                {
                    var alert = ChatAlertPolicy.Decide(isForeground, soundEnabled, muted, OtherUser, CurrentUser);

                    // A message must never produce both an in-app sound and a system notification.
                    Assert.IsTrue(
                        alert is ChatAlertKind.None or ChatAlertKind.InAppSound or ChatAlertKind.SystemNotification,
                        $"Unexpected alert kind {alert}.");

                    if (muted)
                        Assert.AreEqual(ChatAlertKind.None, alert);
                }
            }
        }
    }
}
