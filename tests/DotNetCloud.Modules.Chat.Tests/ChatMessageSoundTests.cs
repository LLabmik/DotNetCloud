using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the chat incoming-message sound ("ding") — the play decision in
/// <see cref="ChatPageLayout"/> and the <see cref="ChannelHeader"/> sound toggle.
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
    public async Task OnToggleChatSoundClick_InvokesOnToggleChatSoundCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnToggleChatSound = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeToggleChatSoundClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void IsChatSoundEnabled_DefaultIsTrue()
    {
        var header = new TestableChannelHeader();

        Assert.IsTrue(header.TestIsChatSoundEnabled);
    }

    [TestMethod]
    public void IsChatSoundEnabled_WhenSetToFalse_IsFalse()
    {
        var header = new TestableChannelHeader();
        header.SetIsChatSoundEnabled(false);

        Assert.IsFalse(header.TestIsChatSoundEnabled);
    }

    private sealed class TestableChannelHeader : ChannelHeader
    {
        public bool TestIsChatSoundEnabled => IsChatSoundEnabled;

        public void SetIsChatSoundEnabled(bool value) => IsChatSoundEnabled = value;

        public Task InvokeToggleChatSoundClick() => OnToggleChatSoundClick();
    }
}
