using Microsoft.AspNetCore.Components;

using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="DirectMessageView"/> DM user-search and channel-ready behavior.
/// </summary>
[TestClass]
public class DirectMessageViewTests
{
    [TestMethod]
    public void WhenSearchQueryProvidedThenSuggestionsAreFilteredByDisplayNameAndUsername()
    {
        var view = CreateView();
        view.AssignUserSuggestions(
        [
            new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Alex Carter", Username = "acarter" },
            new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Beatrice Kim", Username = "bea" },
            new MemberViewModel { UserId = Guid.NewGuid(), DisplayName = "Charlie Stone", Username = "cstone" }
        ]);

        view.SetSearchQuery("bea");
        CollectionAssert.AreEqual(new[] { "Beatrice Kim" }, view.VisibleSuggestions.Select(user => user.DisplayName).ToArray());

        view.SetSearchQuery("cst");
        CollectionAssert.AreEqual(new[] { "Charlie Stone" }, view.VisibleSuggestions.Select(user => user.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task WhenUserSelectedThenDmChannelReadyCallbackReceivesCreatedChannel()
    {
        var selectedUser = new MemberViewModel
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Beatrice Kim",
            Username = "bea"
        };

        var expectedChannelId = Guid.NewGuid();
        var view = new TestableDirectMessageView(_ => Task.FromResult<ChannelViewModel?>(new ChannelViewModel
        {
            Id = expectedChannelId,
            Name = "beatrice-kim",
            Type = "DirectMessage"
        }));

        view.AssignUserSuggestions([selectedUser]);

        ChannelViewModel? callbackChannel = null;
        var callbackReceiver = new object();
        view.OnDmChannelReady = EventCallback.Factory.Create<ChannelViewModel>(callbackReceiver, channel => callbackChannel = channel);

        await view.SelectUserAsync(selectedUser);

        Assert.IsNotNull(callbackChannel);
        Assert.AreEqual(expectedChannelId, callbackChannel.Id);
        Assert.AreEqual("beatrice-kim", callbackChannel.Name);
        Assert.AreEqual("DirectMessage", callbackChannel.Type);
        Assert.IsFalse(view.IsSearchVisibleForTest);
    }

    [TestMethod]
    public async Task WhenGroupMemberAddedThenMemberCountIncreasesAndGroupIndicatorIsShown()
    {
        var dmPeer = new MemberViewModel
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Alex Carter",
            Username = "acarter"
        };

        var newMember = new MemberViewModel
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Beatrice Kim",
            Username = "bea"
        };

        Guid? addedToChannel = null;
        Guid? addedUserId = null;

        var view = new TestableDirectMessageView(
            _ => Task.FromResult<ChannelViewModel?>(null),
            (channelId, userId) =>
            {
                addedToChannel = channelId;
                addedUserId = userId;
                return Task.FromResult(true);
            });

        var channelId = Guid.NewGuid();
        view.AssignActiveDm(dmPeer, channelId, 2);
        view.AssignUserSuggestions([dmPeer, newMember]);

        MemberViewModel? callbackUser = null;
        var callbackReceiver = new object();
        view.OnGroupMemberAdded = EventCallback.Factory.Create<MemberViewModel>(callbackReceiver, member => callbackUser = member);

        view.ToggleAddPeoplePickerForTest();
        await view.AddGroupMemberForTestAsync(newMember);

        Assert.AreEqual(channelId, addedToChannel);
        Assert.AreEqual(newMember.UserId, addedUserId);
        Assert.IsNotNull(callbackUser);
        Assert.AreEqual(newMember.UserId, callbackUser.UserId);
        Assert.AreEqual(3, view.EffectiveMemberCountForTest);
        Assert.IsTrue(view.IsGroupConversationForTest);
        Assert.IsFalse(view.IsAddPeoplePickerVisibleForTest);
    }

    private static TestableDirectMessageView CreateView()
    {
        return new TestableDirectMessageView(_ => Task.FromResult<ChannelViewModel?>(null));
    }

    private sealed class TestableDirectMessageView : DirectMessageView
    {
        private readonly Func<Guid, Task<ChannelViewModel?>> _dmFactory;
        private readonly Func<Guid, Guid, Task<bool>> _addMemberFactory;

        public TestableDirectMessageView(
            Func<Guid, Task<ChannelViewModel?>> dmFactory,
            Func<Guid, Guid, Task<bool>>? addMemberFactory = null)
        {
            _dmFactory = dmFactory;
            _addMemberFactory = addMemberFactory ?? ((_, _) => Task.FromResult(false));
        }

        public IReadOnlyList<MemberViewModel> VisibleSuggestions => FilteredDmUserSuggestions;

        public bool IsSearchVisibleForTest => IsDmSearchVisible;

        public bool IsAddPeoplePickerVisibleForTest => IsAddPeoplePickerVisible;

        public int EffectiveMemberCountForTest => EffectiveMemberCount;

        public bool IsGroupConversationForTest => IsGroupConversation;

        public void AssignUserSuggestions(IReadOnlyList<MemberViewModel> users)
        {
            UserSuggestions = users.ToList();
            base.OnParametersSet();
        }

        public void AssignActiveDm(MemberViewModel otherUser, Guid channelId, int memberCount)
        {
            OtherUser = otherUser;
            ActiveChannelId = channelId;
            ChannelMemberCount = memberCount;
            base.OnParametersSet();
        }

        public void SetSearchQuery(string query)
        {
            DmUserSearchQuery = query;
        }

        public Task SelectUserAsync(MemberViewModel user)
        {
            return SelectDmUserAsync(user);
        }

        public void ToggleAddPeoplePickerForTest()
        {
            ToggleAddPeoplePicker();
        }

        public Task AddGroupMemberForTestAsync(MemberViewModel user)
        {
            return AddGroupMemberAsync(user);
        }

        protected override Task<ChannelViewModel?> GetOrCreateDmChannelAsync(Guid otherUserId)
        {
            return _dmFactory(otherUserId);
        }

        protected override Task<bool> AddMemberToDmAsync(Guid channelId, Guid userId)
        {
            return _addMemberFactory(channelId, userId);
        }
    }
}
