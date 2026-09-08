using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the chat incoming-message sound ("ding") — the play decision in
/// <see cref="ChatPageLayout"/> and the global chat-sound toggle on <see cref="ChannelList"/>.
/// </summary>
[TestClass]
public class ChatMessageSoundTests
{
    private static readonly Guid CurrentUser = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RemoteUser = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void ShouldPlayMessageSound_SoundEnabledRemoteSenderUnmuted_ReturnsTrue()
    {
        Assert.IsTrue(ChatPageLayout.ShouldPlayMessageSound(true, RemoteUser, CurrentUser, channelIsMuted: false));
    }

    [TestMethod]
    public void ShouldPlayMessageSound_SoundDisabled_ReturnsFalse()
    {
        Assert.IsFalse(ChatPageLayout.ShouldPlayMessageSound(false, RemoteUser, CurrentUser, channelIsMuted: false));
    }

    [TestMethod]
    public void ShouldPlayMessageSound_OwnMessage_ReturnsFalse()
    {
        // Echo of the current user's own sent message must never ding.
        Assert.IsFalse(ChatPageLayout.ShouldPlayMessageSound(true, CurrentUser, CurrentUser, channelIsMuted: false));
    }

    [TestMethod]
    public void ShouldPlayMessageSound_EmptySender_ReturnsFalse()
    {
        // System/unknown senders must never ding.
        Assert.IsFalse(ChatPageLayout.ShouldPlayMessageSound(true, Guid.Empty, CurrentUser, channelIsMuted: false));
    }

    [TestMethod]
    public void ShouldPlayMessageSound_MutedChannel_ReturnsFalse()
    {
        Assert.IsFalse(ChatPageLayout.ShouldPlayMessageSound(true, RemoteUser, CurrentUser, channelIsMuted: true));
    }

    [TestMethod]
    public async Task ToggleChatSound_InvokesOnToggleChatSoundCallback()
    {
        var list = new TestableChannelList();
        var invoked = false;
        var receiver = new object();
        list.OnToggleChatSound = EventCallback.Factory.Create(receiver, () => invoked = true);

        await list.InvokeToggleChatSound();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void IsChatSoundEnabled_DefaultIsTrue()
    {
        var list = new TestableChannelList();

        Assert.IsTrue(list.TestIsChatSoundEnabled);
    }

    [TestMethod]
    public void IsChatSoundEnabled_WhenSetToFalse_IsFalse()
    {
        var list = new TestableChannelList();
        list.SetIsChatSoundEnabled(false);

        Assert.IsFalse(list.TestIsChatSoundEnabled);
    }

    private sealed class TestableChannelList : ChannelList
    {
        public bool TestIsChatSoundEnabled => IsChatSoundEnabled;

        public void SetIsChatSoundEnabled(bool value) => IsChatSoundEnabled = value;

        public Task InvokeToggleChatSound() => ToggleChatSound();
    }
}
