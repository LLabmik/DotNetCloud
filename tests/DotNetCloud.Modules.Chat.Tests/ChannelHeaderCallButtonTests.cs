using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelHeader"/> video call button callbacks and HasActiveCall state.
/// </summary>
[TestClass]
public class ChannelHeaderCallButtonTests
{
    [TestMethod]
    public async Task OnAudioCallClick_InvokesOnAudioCallCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnAudioCall = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeAudioCallClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task OnVideoCallClick_InvokesOnVideoCallCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnVideoCall = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeVideoCallClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task OnJoinCallClick_InvokesOnJoinCallCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnJoinCall = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeJoinCallClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task OnCallHistoryClick_InvokesOnCallHistoryCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnCallHistory = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeCallHistoryClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task OnAddPeopleClick_InvokesOnAddPeopleCallback()
    {
        var header = new TestableChannelHeader();
        var invoked = false;
        var receiver = new object();
        header.OnAddPeople = EventCallback.Factory.Create(receiver, () => invoked = true);

        await header.InvokeAddPeopleClick();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void HasActiveCall_DefaultIsFalse()
    {
        var header = new TestableChannelHeader();

        Assert.IsFalse(header.TestHasActiveCall);
    }

    [TestMethod]
    public void HasActiveCall_WhenSetToTrue_IsTrue()
    {
        var header = new TestableChannelHeader();
        header.SetHasActiveCall(true);

        Assert.IsTrue(header.TestHasActiveCall);
    }

    private sealed class TestableChannelHeader : ChannelHeader
    {
        public bool TestHasActiveCall => HasActiveCall;

        public void SetHasActiveCall(bool value) => HasActiveCall = value;

        public Task InvokeAudioCallClick() => OnAudioCallClick();
        public Task InvokeVideoCallClick() => OnVideoCallClick();
        public Task InvokeJoinCallClick() => OnJoinCallClick();
        public Task InvokeCallHistoryClick() => OnCallHistoryClick();
        public Task InvokeAddPeopleClick() => OnAddPeopleClick();
    }
}
