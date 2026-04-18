using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MemberListPanel"/> callback wiring and profile selection behavior.
/// </summary>
[TestClass]
public class MemberListPanelTests
{
    [TestMethod]
    public async Task AddPeople_InvokesOnAddPeopleCallback()
    {
        var panel = new TestableMemberListPanel();
        var receiver = new object();
        var invoked = false;
        panel.OnAddPeople = EventCallback.Factory.Create(receiver, () => invoked = true);

        await panel.InvokeAddPeopleAsync();

        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public async Task Promote_InvokesOnPromoteMemberWithUserId()
    {
        var panel = new TestableMemberListPanel();
        var member = CreateMember();
        var receiver = new object();
        Guid? receivedUserId = null;

        panel.OnPromoteMember = EventCallback.Factory.Create<Guid>(receiver, id => receivedUserId = id);
        await panel.InvokePromoteAsync(member);

        Assert.AreEqual(member.UserId, receivedUserId);
    }

    [TestMethod]
    public async Task Demote_InvokesOnDemoteMemberWithUserId()
    {
        var panel = new TestableMemberListPanel();
        var member = CreateMember();
        var receiver = new object();
        Guid? receivedUserId = null;

        panel.OnDemoteMember = EventCallback.Factory.Create<Guid>(receiver, id => receivedUserId = id);
        await panel.InvokeDemoteAsync(member);

        Assert.AreEqual(member.UserId, receivedUserId);
    }

    [TestMethod]
    public async Task Remove_InvokesOnRemoveMemberWithUserId()
    {
        var panel = new TestableMemberListPanel();
        var member = CreateMember();
        var receiver = new object();
        Guid? receivedUserId = null;

        panel.OnRemoveMember = EventCallback.Factory.Create<Guid>(receiver, id => receivedUserId = id);
        await panel.InvokeRemoveAsync(member);

        Assert.AreEqual(member.UserId, receivedUserId);
    }

    [TestMethod]
    public void ShowProfile_SetsSelectedMember()
    {
        var panel = new TestableMemberListPanel();
        var member = CreateMember();

        panel.InvokeShowProfile(member);

        Assert.IsNotNull(panel.TestSelectedMember);
        Assert.AreEqual(member.UserId, panel.TestSelectedMember!.UserId);
    }

    [TestMethod]
    public void HideProfile_ClearsSelectedMember()
    {
        var panel = new TestableMemberListPanel();
        var member = CreateMember();
        panel.InvokeShowProfile(member);

        panel.InvokeHideProfile();

        Assert.IsNull(panel.TestSelectedMember);
    }

    private static MemberViewModel CreateMember()
    {
        return new MemberViewModel
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Member One",
            Role = "Member",
            Status = "Online"
        };
    }

    private sealed class TestableMemberListPanel : MemberListPanel
    {
        public MemberViewModel? TestSelectedMember => SelectedMember;

        public Task InvokeAddPeopleAsync() => AddPeople();
        public Task InvokePromoteAsync(MemberViewModel member) => Promote(member);
        public Task InvokeDemoteAsync(MemberViewModel member) => Demote(member);
        public Task InvokeRemoveAsync(MemberViewModel member) => Remove(member);
        public void InvokeShowProfile(MemberViewModel member) => ShowProfile(member);
        public void InvokeHideProfile() => HideProfile();
    }
}
