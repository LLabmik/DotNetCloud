using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the chat page layout orchestrator.
/// Manages channel selection, message loading, message sending, reactions,
/// member management, search, typing indicators, and announcements.
/// </summary>
public partial class ChatPageLayout : ComponentBase
{
    private const int MessagePageSize = 50;
    private const int SearchPageSize = 25;

    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private IMessageService MessageService { get; set; } = default!;
    [Inject] private IReactionService ReactionService { get; set; } = default!;
    [Inject] private IChannelMemberService MemberService { get; set; } = default!;
    [Inject] private ITypingIndicatorService TypingService { get; set; } = default!;
    [Inject] private IAnnouncementService AnnouncementService { get; set; } = default!;
    [Inject] private IChannelInviteService InviteService { get; set; } = default!;
    [Inject] private IUserDirectory UserDirectory { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    // Channel state
    private List<ChannelViewModel> _channels = [];
    private ChannelViewModel? _selectedChannel;
    private bool _isLoadingChannels;
    private string? _channelErrorMessage;

    // Message state
    private List<MessageViewModel> _messages = [];
    private bool _isLoadingMessages;
    private string? _messageErrorMessage;
    private bool _hasMoreMessages;
    private int _currentMessagePage = 1;
    private MessageViewModel? _replyToMessage;
    private MessageViewModel? _editingMessage;

    // Member state
    private List<MemberViewModel> _members = [];
    private List<MemberViewModel> _memberSuggestions = [];
    private bool _showMemberPanel;

    // Search state
    private bool _isSearchOpen;
    private string _searchQuery = string.Empty;
    private List<MessageViewModel> _searchResults = [];
    private bool _isSearching;
    private bool _hasMoreSearchResults;
    private int _currentSearchPage = 1;

    // Settings dialog state
    private bool _showSettingsDialog;

    // Announcement state
    private List<AnnouncementViewModel> _announcements = [];
    private AnnouncementViewModel? _activeAnnouncement;
    private bool _showAnnouncementEditor;
    private bool _isEditingAnnouncement;
#pragma warning disable CS0414 // Assigned but read only in LoadAnnouncementsAsync guard
    private bool _isLoadingAnnouncements;
#pragma warning restore CS0414
    private string? _announcementErrorMessage;
    private readonly HashSet<Guid> _dismissedAnnouncementIds = [];

    // Typing state
    private List<TypingUserViewModel> _typingUsers = [];

    // Invite state
    private bool _showInviteDialog;
    private string _inviteUsername = string.Empty;
    private string _inviteMessage = string.Empty;
    private string? _inviteErrorMessage;
    private string? _inviteSuccessMessage;
    private bool _isInviting;

    // User state
    private Guid _currentUserId;
    private string _currentUserRole = "Member";
    private bool _currentUserIsAdminOrOwner;
    private readonly Dictionary<Guid, string> _displayNameCache = [];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var caller = await GetCallerContextAsync();
        _currentUserId = caller.UserId;
        await LoadChannelsAsync();
        await LoadAnnouncementsAsync();
    }

    // ── Channel Operations ──────────────────────────────────────────

    private async Task LoadChannelsAsync()
    {
        try
        {
            _isLoadingChannels = true;
            _channelErrorMessage = null;

            var caller = await GetCallerContextAsync();
            var channels = await ChannelService.ListChannelsAsync(caller);
            _channels = channels.Select(ToChannelViewModel).ToList();

            // Load unread counts and apply to channel view models
            await LoadUnreadCountsAsync(caller);

            // Auto-select the first channel so the composer is immediately visible
            if (_selectedChannel is null && _channels.Count > 0)
            {
                await HandleChannelSelected(_channels[0]);
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
        finally
        {
            _isLoadingChannels = false;
        }
    }

    private async Task LoadUnreadCountsAsync(CallerContext caller)
    {
        try
        {
            var counts = await MemberService.GetUnreadCountsAsync(caller);
            foreach (var count in counts)
            {
                var channel = _channels.FirstOrDefault(c => c.Id == count.ChannelId);
                if (channel is not null)
                {
                    channel.UnreadCount = count.UnreadCount;
                    channel.MentionCount = count.MentionCount;
                }
            }
        }
        catch
        {
            // Non-critical: unread counts are a nice-to-have
        }
    }

    /// <summary>Handles channel selection from the sidebar.</summary>
    protected async Task HandleChannelSelected(ChannelViewModel channel)
    {
        _selectedChannel = channel;
        _replyToMessage = null;
        _editingMessage = null;
        _messages = [];
        _currentMessagePage = 1;
        _showMemberPanel = false;
        _isSearchOpen = false;
        _searchResults = [];

        await LoadMessagesAsync(channel.Id);
        await LoadMembersAsync(channel.Id);
        await MarkChannelAsReadAsync(channel.Id);
    }

    /// <summary>Handles creating a new channel.</summary>
    protected async Task HandleCreateChannel((string Name, string Type) args)
    {
        try
        {
            _channelErrorMessage = null;
            var caller = await GetCallerContextAsync();
            var created = await ChannelService.CreateChannelAsync(new CreateChannelDto
            {
                Name = args.Name.Trim(),
                Type = args.Type
            }, caller);

            _channels.Add(ToChannelViewModel(created));
            _channels = _channels
                .OrderByDescending(c => c.LastActivityAt ?? DateTime.MinValue)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Auto-select the newly created channel
            await HandleChannelSelected(_channels.First(c => c.Id == created.Id));
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles pin state changes from the header.</summary>
    protected Task HandlePinChanged((Guid ChannelId, bool IsPinned) args)
    {
        var channel = _channels.FirstOrDefault(c => c.Id == args.ChannelId);
        if (channel is not null)
        {
            channel.IsPinned = args.IsPinned;
        }
        return Task.CompletedTask;
    }

    /// <summary>Handles channel reorder from drag-and-drop.</summary>
    protected Task HandleChannelReordered(IReadOnlyList<Guid> newOrder)
    {
        // Pinned order is persisted client-side for now.
        return Task.CompletedTask;
    }

    /// <summary>Handles archive channel action.</summary>
    protected async Task HandleArchiveChannel(ChannelViewModel channel)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await ChannelService.ArchiveChannelAsync(channel.Id, caller);
            _channels.Remove(channel);
            if (_selectedChannel?.Id == channel.Id)
            {
                _selectedChannel = null;
                _messages = [];
                _members = [];
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles leave channel action.</summary>
    protected async Task HandleLeaveChannel(ChannelViewModel channel)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.RemoveMemberAsync(channel.Id, _currentUserId, caller);
            _channels.Remove(channel);
            if (_selectedChannel?.Id == channel.Id)
            {
                _selectedChannel = null;
                _messages = [];
                _members = [];
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Opens the channel settings dialog.</summary>
    protected async Task HandleEditChannel(ChannelViewModel channel)
    {
        _showSettingsDialog = true;
        await LoadMembersAsync(channel.Id);
    }

    /// <summary>Saves channel settings from the dialog.</summary>
    protected async Task HandleSaveChannelSettings((string Name, string? Topic, string? Description) args)
    {
        if (_selectedChannel is null) return;

        try
        {
            var caller = await GetCallerContextAsync();
            var updated = await ChannelService.UpdateChannelAsync(_selectedChannel.Id, new UpdateChannelDto
            {
                Name = args.Name,
                Topic = args.Topic,
                Description = args.Description
            }, caller);

            _selectedChannel.Name = updated.Name;
            _selectedChannel.Topic = updated.Topic;
            _showSettingsDialog = false;

            // Update in channels list too
            var ch = _channels.FirstOrDefault(c => c.Id == _selectedChannel.Id);
            if (ch is not null)
            {
                ch.Name = updated.Name;
                ch.Topic = updated.Topic;
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles notification preference change from settings dialog.</summary>
    protected async Task HandleNotificationPrefChanged(string pref)
    {
        if (_selectedChannel is null) return;

        try
        {
            var caller = await GetCallerContextAsync();
            if (Enum.TryParse<NotificationPreference>(pref, true, out var preference))
            {
                await MemberService.UpdateNotificationPreferenceAsync(_selectedChannel.Id, preference, caller);
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles deleting a channel from the settings dialog.</summary>
    protected async Task HandleDeleteChannel()
    {
        if (_selectedChannel is null) return;

        try
        {
            var caller = await GetCallerContextAsync();
            await ChannelService.DeleteChannelAsync(_selectedChannel.Id, caller);
            _channels.RemoveAll(c => c.Id == _selectedChannel.Id);
            _selectedChannel = null;
            _messages = [];
            _members = [];
            _showSettingsDialog = false;
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Closes the channel settings dialog.</summary>
    protected Task HandleCloseSettingsDialog()
    {
        _showSettingsDialog = false;
        return Task.CompletedTask;
    }

    // ── Message Operations ──────────────────────────────────────────

    private async Task LoadMessagesAsync(Guid channelId)
    {
        try
        {
            _isLoadingMessages = true;
            _messageErrorMessage = null;

            var caller = await GetCallerContextAsync();
            var result = await MessageService.GetMessagesAsync(channelId, _currentMessagePage, MessagePageSize, caller);
            await ResolveDisplayNamesAsync(result.Items);
            _messages = result.Items.Select(ToMessageViewModel).ToList();
            _hasMoreMessages = result.Page < result.TotalPages;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
        finally
        {
            _isLoadingMessages = false;
        }
    }

    /// <summary>Handles loading older messages.</summary>
    protected async Task HandleLoadMoreMessages()
    {
        if (_selectedChannel is null || !_hasMoreMessages) return;

        try
        {
            _currentMessagePage++;
            var caller = await GetCallerContextAsync();
            var result = await MessageService.GetMessagesAsync(_selectedChannel.Id, _currentMessagePage, MessagePageSize, caller);
            await ResolveDisplayNamesAsync(result.Items);
            var older = result.Items.Select(ToMessageViewModel).ToList();
            _messages.InsertRange(0, older);
            _hasMoreMessages = result.Page < result.TotalPages;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
            _currentMessagePage--;
        }
    }

    /// <summary>Handles sending a new message.</summary>
    protected async Task HandleSendMessage((string Content, Guid? ReplyToMessageId) args)
    {
        if (_selectedChannel is null) return;

        try
        {
            var caller = await GetCallerContextAsync();
            var sent = await MessageService.SendMessageAsync(_selectedChannel.Id, new SendMessageDto
            {
                Content = args.Content,
                ReplyToMessageId = args.ReplyToMessageId
            }, caller);

            await ResolveDisplayNamesAsync([sent]);
            _messages.Add(ToMessageViewModel(sent));
            _replyToMessage = null;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles editing an existing message.</summary>
    protected async Task HandleEditMessage((Guid MessageId, string Content) args)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var edited = await MessageService.EditMessageAsync(args.MessageId, new EditMessageDto
            {
                Content = args.Content
            }, caller);

            await ResolveDisplayNamesAsync([edited]);
            var idx = _messages.FindIndex(m => m.Id == args.MessageId);
            if (idx >= 0)
            {
                _messages[idx] = ToMessageViewModel(edited);
            }
            _editingMessage = null;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles deleting a message.</summary>
    protected async Task HandleDeleteMessage(Guid messageId)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await MessageService.DeleteMessageAsync(messageId, caller);
            _messages.RemoveAll(m => m.Id == messageId);
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles setting a message as the reply target.</summary>
    protected Task HandleReplyToMessage(MessageViewModel message)
    {
        _replyToMessage = message;
        return Task.CompletedTask;
    }

    /// <summary>Handles cancel reply action.</summary>
    protected Task HandleCancelReply()
    {
        _replyToMessage = null;
        return Task.CompletedTask;
    }

    /// <summary>Starts editing a message.</summary>
    protected Task HandleStartEditMessage(MessageViewModel message)
    {
        _editingMessage = message;
        return Task.CompletedTask;
    }

    /// <summary>Cancels message editing.</summary>
    protected Task HandleCancelEditMessage()
    {
        _editingMessage = null;
        return Task.CompletedTask;
    }

    // ── Reaction Operations ─────────────────────────────────────────

    /// <summary>Handles toggling a reaction on a message.</summary>
    protected async Task HandleReactionToggle((Guid MessageId, string Emoji) args)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var message = _messages.FirstOrDefault(m => m.Id == args.MessageId);
            var existingReaction = message?.Reactions.FirstOrDefault(r => r.Emoji == args.Emoji);

            if (existingReaction?.HasReacted == true)
            {
                await ReactionService.RemoveReactionAsync(args.MessageId, args.Emoji, caller);
            }
            else
            {
                await ReactionService.AddReactionAsync(args.MessageId, args.Emoji, caller);
            }

            // Refresh reactions for this message
            var reactions = await ReactionService.GetReactionsAsync(args.MessageId);
            if (message is not null)
            {
                message.Reactions = reactions.Select(r => new ReactionViewModel
                {
                    Emoji = r.Emoji,
                    Count = r.Count,
                    HasReacted = r.UserIds.Contains(_currentUserId)
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
    }

    // ── Member Operations ───────────────────────────────────────────

    private async Task LoadMembersAsync(Guid channelId)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var members = await MemberService.ListMembersAsync(channelId, caller);
            _members = members.Select(ToMemberViewModel).ToList();
            _memberSuggestions = _members;

            // Derive the current user's role in this channel
            var currentMember = members.FirstOrDefault(m => m.UserId == _currentUserId);
            _currentUserRole = currentMember?.Role ?? "Member";
            _currentUserIsAdminOrOwner = _currentUserRole is "Owner" or "Admin";
        }
        catch
        {
            // Non-critical: member list is a nice-to-have
            _members = [];
            _currentUserRole = "Member";
            _currentUserIsAdminOrOwner = false;
        }
    }

    /// <summary>Handles toggle member list action.</summary>
    protected async Task HandleToggleMemberList()
    {
        _showMemberPanel = !_showMemberPanel;
        if (_showMemberPanel && _selectedChannel is not null)
        {
            await LoadMembersAsync(_selectedChannel.Id);
        }
    }

    /// <summary>Closes the member panel.</summary>
    protected Task HandleCloseMemberPanel()
    {
        _showMemberPanel = false;
        return Task.CompletedTask;
    }

    // ── Invite Operations ───────────────────────────────────────────

    /// <summary>Opens the invite dialog.</summary>
    protected void HandleOpenInviteDialog()
    {
        _showInviteDialog = true;
        _inviteUsername = string.Empty;
        _inviteMessage = string.Empty;
        _inviteErrorMessage = null;
        _inviteSuccessMessage = null;
        _isInviting = false;
    }

    /// <summary>Closes the invite dialog.</summary>
    protected void HandleCloseInviteDialog()
    {
        _showInviteDialog = false;
    }

    /// <summary>Sends a channel invite to a user by username.</summary>
    protected async Task HandleSendInvite()
    {
        if (_selectedChannel is null || string.IsNullOrWhiteSpace(_inviteUsername)) return;

        _isInviting = true;
        _inviteErrorMessage = null;
        _inviteSuccessMessage = null;

        try
        {
            var userId = await UserDirectory.FindUserIdByUsernameAsync(_inviteUsername.Trim());
            if (userId is null)
            {
                _inviteErrorMessage = $"User \"{_inviteUsername}\" not found.";
                return;
            }

            var caller = await GetCallerContextAsync();
            var dto = new CreateChannelInviteDto
            {
                UserId = userId.Value,
                Message = string.IsNullOrWhiteSpace(_inviteMessage) ? null : _inviteMessage.Trim()
            };

            await InviteService.CreateInviteAsync(_selectedChannel.Id, dto, caller);
            _inviteSuccessMessage = $"Invite sent to {_inviteUsername}.";
            _inviteUsername = string.Empty;
            _inviteMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _inviteErrorMessage = ex.Message;
        }
        finally
        {
            _isInviting = false;
        }
    }

    /// <summary>Promotes a member to Admin role.</summary>
    protected async Task HandlePromoteMember(Guid userId)
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.UpdateMemberRoleAsync(_selectedChannel.Id, userId, ChannelMemberRole.Admin, caller);
            await LoadMembersAsync(_selectedChannel.Id);
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Demotes a member to Member role.</summary>
    protected async Task HandleDemoteMember(Guid userId)
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.UpdateMemberRoleAsync(_selectedChannel.Id, userId, ChannelMemberRole.Member, caller);
            await LoadMembersAsync(_selectedChannel.Id);
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Removes a member from the channel.</summary>
    protected async Task HandleRemoveMember(Guid userId)
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.RemoveMemberAsync(_selectedChannel.Id, userId, caller);
            await LoadMembersAsync(_selectedChannel.Id);
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Adds a member to the channel from the settings dialog.</summary>
    protected async Task HandleAddMember(Guid userId)
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.AddMemberAsync(_selectedChannel.Id, userId, caller);
            await LoadMembersAsync(_selectedChannel.Id);
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Changes a member's role from the settings dialog.</summary>
    protected async Task HandleChangeMemberRole((Guid UserId, string Role) args)
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            if (Enum.TryParse<ChannelMemberRole>(args.Role, true, out var role))
            {
                await MemberService.UpdateMemberRoleAsync(_selectedChannel.Id, args.UserId, role, caller);
                await LoadMembersAsync(_selectedChannel.Id);
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    private async Task MarkChannelAsReadAsync(Guid channelId)
    {
        try
        {
            if (_messages.Count > 0)
            {
                var caller = await GetCallerContextAsync();
                var lastMessage = _messages[^1];
                await MemberService.MarkAsReadAsync(channelId, lastMessage.Id, caller);

                var ch = _channels.FirstOrDefault(c => c.Id == channelId);
                if (ch is not null)
                {
                    ch.UnreadCount = 0;
                    ch.MentionCount = 0;
                }
            }
        }
        catch
        {
            // Non-critical
        }
    }

    // ── Search Operations ───────────────────────────────────────────

    /// <summary>Handles search action — toggles the search panel.</summary>
    protected Task HandleSearch()
    {
        _isSearchOpen = !_isSearchOpen;
        if (!_isSearchOpen)
        {
            _searchResults = [];
            _searchQuery = string.Empty;
        }
        return Task.CompletedTask;
    }

    /// <summary>Executes a message search.</summary>
    protected async Task HandleSearchSubmit(string query)
    {
        if (_selectedChannel is null || string.IsNullOrWhiteSpace(query)) return;

        try
        {
            _isSearching = true;
            _searchQuery = query;
            _currentSearchPage = 1;

            var caller = await GetCallerContextAsync();
            var result = await MessageService.SearchMessagesAsync(
                _selectedChannel.Id, query, _currentSearchPage, SearchPageSize, caller);
            await ResolveDisplayNamesAsync(result.Items);
            _searchResults = result.Items.Select(ToMessageViewModel).ToList();
            _hasMoreSearchResults = result.Page < result.TotalPages;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
        finally
        {
            _isSearching = false;
        }
    }

    /// <summary>Closes the search panel.</summary>
    protected Task HandleCloseSearch()
    {
        _isSearchOpen = false;
        _searchResults = [];
        _searchQuery = string.Empty;
        return Task.CompletedTask;
    }

    // ── Typing Indicators ───────────────────────────────────────────

    /// <summary>Handles typing indicator action.</summary>
    protected async Task HandleTyping()
    {
        if (_selectedChannel is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await TypingService.NotifyTypingAsync(_selectedChannel.Id, caller);
        }
        catch
        {
            // Non-critical
        }
    }

    // ── Attachment (placeholder) ────────────────────────────────────

    /// <summary>Handles attach button click — not yet wired to Files module.</summary>
    protected Task HandleAttach()
    {
        // File attachment requires Files module integration (file picker + upload).
        // This is a cross-module feature that will be wired in a future phase.
        return Task.CompletedTask;
    }

    // ── Announcement Operations ─────────────────────────────────────

    private async Task LoadAnnouncementsAsync()
    {
        try
        {
            _isLoadingAnnouncements = true;
            var caller = await GetCallerContextAsync();
            var announcements = await AnnouncementService.ListAsync(caller);
            _announcements = announcements.Select(ToAnnouncementViewModel).ToList();
            _activeAnnouncement = _announcements
                .Where(a => !_dismissedAnnouncementIds.Contains(a.Id))
                .Where(a => a.ExpiresAt is null || a.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(a => a.Priority == "Urgent")
                .ThenByDescending(a => a.Priority == "Important")
                .ThenByDescending(a => a.PublishedAt)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _announcementErrorMessage = ex.Message;
        }
        finally
        {
            _isLoadingAnnouncements = false;
        }
    }

    /// <summary>Handles acknowledging an announcement.</summary>
    protected async Task HandleAcknowledgeAnnouncement(Guid announcementId)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await AnnouncementService.AcknowledgeAsync(announcementId, caller);
            _dismissedAnnouncementIds.Add(announcementId);
            UpdateActiveAnnouncement();
        }
        catch (Exception ex)
        {
            _announcementErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles dismissing a banner.</summary>
    protected Task HandleDismissAnnouncement()
    {
        if (_activeAnnouncement is not null)
        {
            _dismissedAnnouncementIds.Add(_activeAnnouncement.Id);
        }
        UpdateActiveAnnouncement();
        return Task.CompletedTask;
    }

    /// <summary>Opens the announcement editor for a new announcement.</summary>
    protected Task HandleOpenAnnouncementEditor()
    {
        _showAnnouncementEditor = true;
        _isEditingAnnouncement = false;
        return Task.CompletedTask;
    }

    /// <summary>Saves an announcement from the editor.</summary>
    protected async Task HandleSaveAnnouncement(AnnouncementEditorResult result)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await AnnouncementService.CreateAsync(new CreateAnnouncementDto
            {
                Title = result.Title,
                Content = result.Content,
                Priority = result.Priority,
                ExpiresAt = result.ExpiresAt,
                RequiresAcknowledgement = result.RequiresAcknowledgement
            }, caller);

            _showAnnouncementEditor = false;
            await LoadAnnouncementsAsync();
        }
        catch (Exception ex)
        {
            _announcementErrorMessage = ex.Message;
        }
    }

    /// <summary>Closes the announcement editor.</summary>
    protected Task HandleCloseAnnouncementEditor()
    {
        _showAnnouncementEditor = false;
        return Task.CompletedTask;
    }

    private void UpdateActiveAnnouncement()
    {
        _activeAnnouncement = _announcements
            .Where(a => !_dismissedAnnouncementIds.Contains(a.Id))
            .Where(a => a.ExpiresAt is null || a.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(a => a.Priority == "Urgent")
            .ThenByDescending(a => a.Priority == "Important")
            .ThenByDescending(a => a.PublishedAt)
            .FirstOrDefault();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }

    private static ChannelViewModel ToChannelViewModel(ChannelDto dto)
    {
        return new ChannelViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Type = dto.Type,
            Topic = dto.Topic,
            LastActivityAt = dto.LastActivityAt,
            MemberCount = dto.MemberCount,
            PresenceStatus = dto.Type is "DirectMessage" or "Group" ? "Offline" : string.Empty,
            UnreadCount = 0,
            MentionCount = 0,
            IsActive = false
        };
    }

    private async Task ResolveDisplayNamesAsync(IReadOnlyList<MessageDto> messages)
    {
        var unknownIds = messages
            .Select(m => m.SenderUserId)
            .Where(id => !_displayNameCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (unknownIds.Count == 0) return;

        var names = await UserDirectory.GetDisplayNamesAsync(unknownIds);
        foreach (var (id, name) in names)
        {
            _displayNameCache[id] = name;
        }
    }

    private MessageViewModel ToMessageViewModel(MessageDto dto)
    {
        return new MessageViewModel
        {
            Id = dto.Id,
            SenderUserId = dto.SenderUserId,
            SenderName = _displayNameCache.GetValueOrDefault(dto.SenderUserId, dto.SenderUserId.ToString()[..8]),
            Content = dto.Content,
            Type = dto.Type,
            SentAt = dto.SentAt,
            IsEdited = dto.IsEdited,
            ReplyToMessageId = dto.ReplyToMessageId,
            Reactions = dto.Reactions.Select(r => new ReactionViewModel
            {
                Emoji = r.Emoji,
                Count = r.Count,
                HasReacted = r.UserIds.Contains(_currentUserId)
            }).ToList(),
            Attachments = dto.Attachments.Select(a => new AttachmentViewModel
            {
                Id = a.Id,
                FileName = a.FileName,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                ThumbnailUrl = a.ThumbnailUrl
            }).ToList()
        };
    }

    private MemberViewModel ToMemberViewModel(ChannelMemberDto dto)
    {
        return new MemberViewModel
        {
            UserId = dto.UserId,
            DisplayName = string.IsNullOrEmpty(dto.DisplayName) ? dto.UserId.ToString()[..8] : dto.DisplayName,
            Username = dto.Username,
            Role = dto.Role,
            Status = dto.UserId == _currentUserId ? "Online" : "Offline"
        };
    }

    private static AnnouncementViewModel ToAnnouncementViewModel(AnnouncementDto dto)
    {
        return new AnnouncementViewModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Content = dto.Content,
            Priority = dto.Priority,
            PublishedAt = dto.PublishedAt,
            ExpiresAt = dto.ExpiresAt,
            IsPinned = dto.IsPinned,
            RequiresAcknowledgement = dto.RequiresAcknowledgement,
            AcknowledgementCount = dto.AcknowledgementCount,
            AuthorName = dto.AuthorUserId.ToString()[..8]
        };
    }
}
