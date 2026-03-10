using Microsoft.AspNetCore.Components;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the direct message view component.
/// Provides a streamlined chat view for 1:1 conversations.
/// </summary>
public partial class DirectMessageView : ComponentBase
{
    [Inject] private ChatApiClient? ChatApiClient { get; set; }

    private string _dmUserSearchQuery = string.Empty;
    private string _addPeopleSearchQuery = string.Empty;
    private bool _isDmSearchVisible;
    private bool _isAddPeoplePickerVisible;
    private bool _isCreatingDm;
    private bool _isAddingMember;
    private string? _dmSearchError;
    private string? _addPeopleError;
    private int _effectiveMemberCount;
    private readonly HashSet<Guid> _addedMemberIds = [];

    /// <summary>The other user in the DM conversation.</summary>
    [Parameter]
    public MemberViewModel? OtherUser { get; set; }

    /// <summary>Messages in the conversation.</summary>
    [Parameter]
    public List<MessageViewModel> Messages { get; set; } = [];

    /// <summary>Whether messages are loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Whether there are more messages to load.</summary>
    [Parameter]
    public bool HasMoreMessages { get; set; }

    /// <summary>Error message shown when message retrieval fails.</summary>
    [Parameter]
    public string? MessageError { get; set; }

    /// <summary>Users currently typing.</summary>
    [Parameter]
    public List<TypingUserViewModel> TypingUsers { get; set; } = [];

    /// <summary>Available members to surface as @mention suggestions.</summary>
    [Parameter]
    public List<MemberViewModel> MentionSuggestions { get; set; } = [];

    /// <summary>Message being replied to.</summary>
    [Parameter]
    public MessageViewModel? ReplyToMessage { get; set; }

    /// <summary>Callback to load more messages.</summary>
    [Parameter]
    public EventCallback OnLoadMore { get; set; }

    /// <summary>Callback when a reaction is toggled.</summary>
    [Parameter]
    public EventCallback<(Guid MessageId, string Emoji)> OnReactionToggle { get; set; }

    /// <summary>Callback when a message is sent.</summary>
    [Parameter]
    public EventCallback<(string Content, Guid? ReplyToMessageId)> OnSend { get; set; }

    /// <summary>Callback to cancel a reply.</summary>
    [Parameter]
    public EventCallback OnCancelReply { get; set; }

    /// <summary>Callback when user starts typing.</summary>
    [Parameter]
    public EventCallback OnTyping { get; set; }

    /// <summary>Callback when the attach button is clicked.</summary>
    [Parameter]
    public EventCallback OnAttach { get; set; }

    /// <summary>Callback when an image is pasted in the message composer.</summary>
    [Parameter]
    public EventCallback<PastedImageData> OnPasteImage { get; set; }

    /// <summary>Available users for starting a new direct message.</summary>
    [Parameter]
    public List<MemberViewModel> UserSuggestions { get; set; } = [];

    /// <summary>Raised when a DM channel is ready after selecting a user.</summary>
    [Parameter]
    public EventCallback<ChannelViewModel> OnDmChannelReady { get; set; }

    /// <summary>Active DM channel ID for member management operations.</summary>
    [Parameter]
    public Guid? ActiveChannelId { get; set; }

    /// <summary>Known member count for the active DM channel.</summary>
    [Parameter]
    public int ChannelMemberCount { get; set; }

    /// <summary>Raised when a new member is added to the active DM.</summary>
    [Parameter]
    public EventCallback<MemberViewModel> OnGroupMemberAdded { get; set; }

    /// <summary>Gets the mention suggestions to pass into the composer.</summary>
    protected List<MemberViewModel> ComposerMentionSuggestions => MentionSuggestions.Count > 0
        ? MentionSuggestions
        : OtherUser is null
            ? []
            : [OtherUser];

    /// <summary>Current DM user-search input text.</summary>
    protected string DmUserSearchQuery
    {
        get => _dmUserSearchQuery;
        set => _dmUserSearchQuery = value;
    }

    /// <summary>Whether the DM user search panel is visible.</summary>
    protected bool IsDmSearchVisible => _isDmSearchVisible;

    /// <summary>Whether a DM create operation is currently running.</summary>
    protected bool IsCreatingDm => _isCreatingDm;

    /// <summary>User-visible error message from DM creation.</summary>
    protected string? DmSearchError => _dmSearchError;

    /// <summary>Current add-people search input text.</summary>
    protected string AddPeopleSearchQuery
    {
        get => _addPeopleSearchQuery;
        set => _addPeopleSearchQuery = value;
    }

    /// <summary>Whether add-people picker is visible.</summary>
    protected bool IsAddPeoplePickerVisible => _isAddPeoplePickerVisible;

    /// <summary>Whether a member add operation is currently running.</summary>
    protected bool IsAddingMember => _isAddingMember;

    /// <summary>User-visible error for group member operations.</summary>
    protected string? AddPeopleError => _addPeopleError;

    /// <summary>Current effective member count used by header display.</summary>
    protected int EffectiveMemberCount => _effectiveMemberCount;

    /// <summary>Whether the current DM conversation is a group (more than 2 members).</summary>
    protected bool IsGroupConversation => EffectiveMemberCount > 2;

    /// <summary>Filtered user suggestions for starting a direct message.</summary>
    protected List<MemberViewModel> FilteredDmUserSuggestions
    {
        get
        {
            var availableUsers = UserSuggestions
                .Where(user => OtherUser is null || user.UserId != OtherUser.UserId)
                .ToList();

            if (string.IsNullOrWhiteSpace(_dmUserSearchQuery))
            {
                return availableUsers;
            }

            return availableUsers
                .Where(user =>
                    user.DisplayName.Contains(_dmUserSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains(_dmUserSearchQuery, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

    /// <summary>Filtered user suggestions for adding members to the active DM.</summary>
    protected List<MemberViewModel> FilteredAddPeopleSuggestions
    {
        get
        {
            var excludedUserIds = MentionSuggestions
                .Select(user => user.UserId)
                .Concat(_addedMemberIds)
                .ToHashSet();

            if (OtherUser is not null)
            {
                excludedUserIds.Add(OtherUser.UserId);
            }

            var availableUsers = UserSuggestions
                .Where(user => !excludedUserIds.Contains(user.UserId))
                .ToList();

            if (string.IsNullOrWhiteSpace(_addPeopleSearchQuery))
            {
                return availableUsers;
            }

            return availableUsers
                .Where(user =>
                    user.DisplayName.Contains(_addPeopleSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains(_addPeopleSearchQuery, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (OtherUser is null)
        {
            _isDmSearchVisible = true;
            _isAddPeoplePickerVisible = false;
            _effectiveMemberCount = 0;
            return;
        }

        if (ChannelMemberCount > 0)
        {
            _effectiveMemberCount = ChannelMemberCount;
            return;
        }

        if (_effectiveMemberCount <= 0)
        {
            _effectiveMemberCount = 2;
        }
    }

    /// <summary>Toggles visibility of the new-DM search panel.</summary>
    protected void ToggleDmSearch()
    {
        _isDmSearchVisible = !_isDmSearchVisible;
        _dmSearchError = null;
    }

    /// <summary>Selects a user from search results and opens/creates a DM channel.</summary>
    protected async Task SelectDmUserAsync(MemberViewModel user)
    {
        if (_isCreatingDm)
        {
            return;
        }

        _isCreatingDm = true;
        _dmSearchError = null;

        try
        {
            var channel = await GetOrCreateDmChannelAsync(user.UserId);
            if (channel is null)
            {
                _dmSearchError = "Unable to create a direct message channel for that user.";
                return;
            }

            OtherUser = user;
            _dmUserSearchQuery = string.Empty;
            _isDmSearchVisible = false;
            _effectiveMemberCount = Math.Max(channel.MemberCount, 2);
            _addedMemberIds.Clear();
            _addPeopleSearchQuery = string.Empty;
            _addPeopleError = null;
            _isAddPeoplePickerVisible = false;

            await OnDmChannelReady.InvokeAsync(channel);
        }
        catch (HttpRequestException)
        {
            _dmSearchError = "Unable to create a direct message right now. Please try again.";
        }
        finally
        {
            _isCreatingDm = false;
        }
    }

    /// <summary>Toggles visibility of the group-member add picker.</summary>
    protected void ToggleAddPeoplePicker()
    {
        if (OtherUser is null)
        {
            return;
        }

        _isAddPeoplePickerVisible = !_isAddPeoplePickerVisible;
        _addPeopleError = null;
    }

    /// <summary>Adds a selected user to the active DM channel to create a group DM.</summary>
    protected async Task AddGroupMemberAsync(MemberViewModel user)
    {
        if (_isAddingMember)
        {
            return;
        }

        if (ActiveChannelId is null || ActiveChannelId == Guid.Empty)
        {
            _addPeopleError = "Open a direct message channel before adding members.";
            return;
        }

        _isAddingMember = true;
        _addPeopleError = null;

        try
        {
            var added = await AddMemberToDmAsync(ActiveChannelId.Value, user.UserId);
            if (!added)
            {
                _addPeopleError = "Unable to add that member right now. Please try again.";
                return;
            }

            _addedMemberIds.Add(user.UserId);
            _effectiveMemberCount = Math.Max(_effectiveMemberCount, 2) + 1;
            _addPeopleSearchQuery = string.Empty;
            _isAddPeoplePickerVisible = false;

            await OnGroupMemberAdded.InvokeAsync(user);
        }
        catch (HttpRequestException)
        {
            _addPeopleError = "Unable to add that member right now. Please try again.";
        }
        finally
        {
            _isAddingMember = false;
        }
    }

    /// <summary>Gets or creates a DM channel using the API client.</summary>
    protected virtual async Task<ChannelViewModel?> GetOrCreateDmChannelAsync(Guid otherUserId)
    {
        if (ChatApiClient is null)
        {
            return null;
        }

        var channel = await ChatApiClient.GetOrCreateDmAsync(otherUserId);
        if (channel is null)
        {
            return null;
        }

        return new ChannelViewModel
        {
            Id = channel.Id,
            Name = channel.Name,
            Type = channel.Type,
            Topic = channel.Topic,
            LastActivityAt = channel.LastActivityAt,
            MemberCount = channel.MemberCount,
            PresenceStatus = channel.Type is "DirectMessage" or "Group" ? "Offline" : string.Empty,
            UnreadCount = 0,
            MentionCount = 0,
            IsActive = false
        };
    }

    /// <summary>Adds a member to an existing DM channel via API.</summary>
    protected virtual async Task<bool> AddMemberToDmAsync(Guid channelId, Guid userId)
    {
        if (ChatApiClient is null)
        {
            return false;
        }

        return await ChatApiClient.AddMemberAsync(channelId, new AddChannelMemberDto { UserId = userId });
    }

    /// <summary>Gets initials from a display name.</summary>
    protected static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant()
        };
    }
}
