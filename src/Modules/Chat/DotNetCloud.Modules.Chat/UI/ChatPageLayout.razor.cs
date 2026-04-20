using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the chat page layout orchestrator.
/// Manages channel selection, message loading, message sending, reactions,
/// member management, search, typing indicators, and announcements.
/// </summary>
public partial class ChatPageLayout : ComponentBase, IAsyncDisposable
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
    [Inject] private IChatRealtimeService ChatRealtimeService { get; set; } = default!;
    [Inject] private IChatMessageNotifier ChatMessageNotifier { get; set; } = default!;
    [Inject] private IVideoCallService VideoCallService { get; set; } = default!;
    [Inject] private IWebRtcInteropService WebRtcInterop { get; set; } = default!;
    [Inject] private ICallSignalingService CallSignalingService { get; set; } = default!;
    [Inject] private IIceServerService IceServerService { get; set; } = default!;
    [Inject] private IPresenceTracker PresenceTracker { get; set; } = default!;
    [Inject] private IUserBlockService UserBlockService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private GlobalChatNotificationState GlobalNotificationState { get; set; } = default!;
    [Inject] private IChatImageStore ChatImageStore { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

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

    // Pending image attachments (uploaded, waiting to be sent with next message)
    private readonly List<PendingAttachment> _pendingAttachments = [];
    private readonly string _fileInputId = $"chat-file-input-{Guid.NewGuid():N}";
    private ElementReference _fileInputRef;

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

    // DM user search state
    private bool _showDmUserPicker;
    private bool _dmCreationInProgress;
    private ElementReference _dmSearchInputRef;
    private string _dmSearchTerm = string.Empty;
    private List<UserSearchResultViewModel> _dmSearchResults = [];
    private bool _isDmSearching;
    private string? _dmSearchError;
    private CancellationTokenSource? _dmSearchCts;

    // Channel add-people state
    private bool _showChannelAddPeoplePicker;
    private string _channelAddPeopleSearchTerm = string.Empty;
    private List<UserSearchResultViewModel> _channelAddPeopleResults = [];
    private bool _isChannelAddPeopleSearching;

    // Video call state
    private Guid? _currentCallId;
    private Guid? _incomingCallId;
    private bool _showVideoCallDialog;
    private bool _showCallHistoryPanel;
    private bool _hasActiveCall;
    private string _currentCallState = string.Empty;
    private string _currentUserDisplayName = string.Empty;
    private List<CallParticipantDto> _remoteParticipants = [];
    private bool _isCallMuted;
    private bool _isCallCameraOff;
    private bool _isCallScreenSharing;
    private int _callDurationSeconds;
    private string? _callConnectionQuality;
    private Guid? _incomingCallInitiatorId;
    private Guid? _incomingCallChannelId;
    private Guid? _currentCallHostUserId;
    private bool _showCallAddPeoplePicker;
    private string _callAddPeopleSearchTerm = string.Empty;
    private List<UserSearchResultViewModel> _callAddPeopleResults = [];
    private bool _isCallAddPeopleSearching;
    private List<CallHistoryDto> _callHistory = [];
#pragma warning disable CS0649 // Will be assigned when call history loading is implemented
    private bool _isLoadingCallHistory;
    private bool _isLoadingMoreCallHistory;
    private bool _hasMoreCallHistory;
#pragma warning restore CS0649
    private DotNetObjectReference<ChatPageLayout>? _dotNetRef;
    private Guid? _callPeerId;
    private bool _webRtcInitialized;
    private bool _initialNegotiationDone;
    private System.Timers.Timer? _callDurationTimer;
    private readonly SemaphoreSlim _signalingLock = new(1, 1);

    // User state
    private Guid _currentUserId;
    private string _currentUserRole = "Member";
    private bool _currentUserIsAdminOrOwner;
    private readonly Dictionary<Guid, string> _displayNameCache = [];
    private readonly Dictionary<Guid, string> _avatarUrlCache = [];
    private readonly Dictionary<Guid, Guid> _dmChannelToOtherUser = [];
    private HashSet<Guid> _blockedUserIds = [];  // Users blocked by current user
    private HashSet<Guid> _blockedByUserIds = []; // Users who have blocked current user

    private Guid? SelectedDirectPeerUserId
    {
        get
        {
            if (_selectedChannel is null || _selectedChannel.Type != "DirectMessage")
            {
                return null;
            }

            return _dmChannelToOtherUser.TryGetValue(_selectedChannel.Id, out var otherUserId)
                ? otherUserId
                : null;
        }
    }

    private bool IsSelectedDirectPeerBlocked
        => SelectedDirectPeerUserId is Guid userId && _blockedUserIds.Contains(userId);

    private bool IsBlockedBySelectedPeer
        => SelectedDirectPeerUserId is Guid userId && _blockedByUserIds.Contains(userId);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var caller = await GetCallerContextAsync();
        _currentUserId = caller.UserId;
        _dotNetRef = DotNetObjectReference.Create(this);

        // Resolve the current user's display name for the video call solo view
        var selfNames = await UserDirectory.GetDisplayNamesAsync([_currentUserId]);
        if (selfNames.TryGetValue(_currentUserId, out var selfName))
        {
            _currentUserDisplayName = selfName;
            _displayNameCache[_currentUserId] = selfName;
        }

        var selfAvatars = await UserDirectory.GetAvatarUrlsAsync([_currentUserId]);
        if (selfAvatars.TryGetValue(_currentUserId, out var selfAvatar))
        {
            _avatarUrlCache[_currentUserId] = selfAvatar;
        }

        ChatMessageNotifier.MessageReceived += OnRemoteMessageReceived;
        ChatMessageNotifier.MessageEdited += OnRemoteMessageEdited;
        ChatMessageNotifier.MessageDeleted += OnRemoteMessageDeleted;
        ChatMessageNotifier.CallAccepted += OnCallAccepted;
        ChatMessageNotifier.CallSignalReceived += OnCallSignalReceived;
        ChatMessageNotifier.CallEnded += OnCallEnded;
        ChatMessageNotifier.CallHostTransferred += OnCallHostTransferred;
        ChatMessageNotifier.UserPresenceChanged += OnUserPresenceChanged;
        ChatMessageNotifier.MediaStateChanged += OnMediaStateChanged;
        ChatMessageNotifier.CallParticipantLeft += OnCallParticipantLeft;
        ChatMessageNotifier.ChannelAdded += OnChannelAdded;
        ChatMessageNotifier.ChannelDeleted += OnChannelDeleted;
        ChatMessageNotifier.UserBlockStatusChanged += OnUserBlockStatusChanged;
        GlobalNotificationState.OnCallAccepted += OnGlobalCallAccepted;

        await LoadChannelsAsync();
        await LoadBlockedUsersAsync();
        await LoadAnnouncementsAsync();

        // Check if a call was accepted from the global notification overlay before navigation
        var pendingAccept = GlobalNotificationState.ConsumePendingAccept();
        if (pendingAccept is not null)
        {
            await HandlePendingCallAcceptAsync(pendingAccept);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        ChatMessageNotifier.MessageReceived -= OnRemoteMessageReceived;
        ChatMessageNotifier.MessageEdited -= OnRemoteMessageEdited;
        ChatMessageNotifier.MessageDeleted -= OnRemoteMessageDeleted;
        ChatMessageNotifier.CallAccepted -= OnCallAccepted;
        ChatMessageNotifier.CallSignalReceived -= OnCallSignalReceived;
        ChatMessageNotifier.CallEnded -= OnCallEnded;
        ChatMessageNotifier.CallHostTransferred -= OnCallHostTransferred;
        ChatMessageNotifier.UserPresenceChanged -= OnUserPresenceChanged;
        ChatMessageNotifier.MediaStateChanged -= OnMediaStateChanged;
        ChatMessageNotifier.CallParticipantLeft -= OnCallParticipantLeft;
        ChatMessageNotifier.ChannelAdded -= OnChannelAdded;
        ChatMessageNotifier.ChannelDeleted -= OnChannelDeleted;
        ChatMessageNotifier.UserBlockStatusChanged -= OnUserBlockStatusChanged;
        GlobalNotificationState.OnCallAccepted -= OnGlobalCallAccepted;

        _callDurationTimer?.Stop();
        _callDurationTimer?.Dispose();

        if (_webRtcInitialized)
        {
            try { await WebRtcInterop.HangupAsync(); } catch { /* best-effort cleanup */ }
        }

        _dotNetRef?.Dispose();
        _signalingLock.Dispose();
    }

    private void OnRemoteMessageReceived(Guid channelId, MessageDto message)
    {
        InvokeAsync(() =>
        {
            if (_selectedChannel is not null && _selectedChannel.Id == channelId)
            {
                if (_messages.Any(m => m.Id == message.Id))
                {
                    return;
                }

                _messages.Add(ToMessageViewModel(message));
            }
            else
            {
                var channel = _channels.FirstOrDefault(c => c.Id == channelId);
                if (channel is not null)
                {
                    channel.UnreadCount += 1;
                }
            }

            StateHasChanged();
        });
    }

    private void OnRemoteMessageEdited(Guid channelId, MessageDto message)
    {
        if (_selectedChannel is null || _selectedChannel.Id != channelId) return;

        InvokeAsync(() =>
        {
            var idx = _messages.FindIndex(m => m.Id == message.Id);
            if (idx >= 0)
            {
                _messages[idx] = ToMessageViewModel(message);
                StateHasChanged();
            }
        });
    }

    private void OnRemoteMessageDeleted(Guid channelId, Guid messageId)
    {
        if (_selectedChannel is null || _selectedChannel.Id != channelId) return;

        InvokeAsync(() =>
        {
            if (_messages.RemoveAll(m => m.Id == messageId) > 0)
            {
                StateHasChanged();
            }
        });
    }

    private void OnUserPresenceChanged(UserPresenceChangedNotification notification)
    {
        InvokeAsync(() =>
        {
            var changed = false;

            // Update member list
            var member = _members.FirstOrDefault(m => m.UserId == notification.UserId);
            if (member is not null)
            {
                member.Status = notification.IsOnline ? "Online" : "Offline";
                changed = true;
            }

            // Update DM channel presence dots
            foreach (var (channelId, otherUserId) in _dmChannelToOtherUser)
            {
                if (otherUserId == notification.UserId)
                {
                    var channel = _channels.FirstOrDefault(c => c.Id == channelId);
                    if (channel is not null)
                    {
                        channel.PresenceStatus = notification.IsOnline ? "Online" : "Offline";
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                StateHasChanged();
            }
        });
    }

    private void OnMediaStateChanged(MediaStateChangedNotification notification)
    {
        // Ignore our own media state changes — we already know our own state
        if (notification.UserId == _currentUserId) return;
        if (_currentCallId is null || notification.CallId != _currentCallId) return;

        InvokeAsync(() =>
        {
            var participant = _remoteParticipants.FirstOrDefault(p => p.UserId == notification.UserId);
            if (participant is null) return;

            switch (notification.MediaType)
            {
                case "Audio":
                    participant.HasAudio = notification.Enabled;
                    break;
                case "Video":
                    participant.HasVideo = notification.Enabled;
                    break;
                case "ScreenShare":
                    participant.HasScreenShare = notification.Enabled;
                    break;
            }

            StateHasChanged();
        });
    }

    // ── Channel Operations ──────────────────────────────────────────

    private async Task LoadChannelsAsync()
    {
        if (_isLoadingChannels) return;
        try
        {
            _isLoadingChannels = true;
            _channelErrorMessage = null;

            var caller = await GetCallerContextAsync();
            var channels = await ChannelService.ListChannelsAsync(caller);
            _channels = channels.Select(ToChannelViewModel).ToList();

            // Resolve display names for DM channels (replace raw "DM-{guid}-{guid}" with the other user's name)
            await ResolveDmChannelNamesAsync();

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
                    channel.IsMuted = count.IsMuted;
                    channel.IsPinned = count.IsPinned;
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
        await CheckIfBlockedByPeerAsync();
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

    /// <summary>Handles mute state changes from the header.</summary>
    protected async Task HandleMuteChanged((Guid ChannelId, bool IsMuted) args)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.SetMuteAsync(args.ChannelId, args.IsMuted, caller);
            var channel = _channels.FirstOrDefault(c => c.Id == args.ChannelId);
            if (channel is not null)
            {
                channel.IsMuted = args.IsMuted;
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Loads all users blocked by the current user.</summary>
    private async Task LoadBlockedUsersAsync()
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var blockedList = await UserBlockService.GetBlockedUsersAsync(caller);
            _blockedUserIds = new HashSet<Guid>(blockedList.Select(b => b.BlockedUserId));
        }
        catch
        {
            // Non-critical: blocking list is a nice-to-have
            _blockedUserIds = [];
        }
    }

    /// <summary>Checks if the selected DM peer has blocked the current user.</summary>
    private async Task CheckIfBlockedByPeerAsync()
    {
        if (SelectedDirectPeerUserId is not Guid peerId) return;

        try
        {
            var isBlocked = await UserBlockService.IsBlockedAsync(_currentUserId, peerId);
            if (isBlocked)
            {
                _blockedByUserIds.Add(peerId);
            }
            else
            {
                _blockedByUserIds.Remove(peerId);
            }
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Handles block/unblock toggle for a user.</summary>
    protected async Task HandleToggleBlockUser(Guid userId)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            if (_blockedUserIds.Contains(userId))
            {
                // Unblock
                await UserBlockService.UnblockUserAsync(userId, caller);
                _blockedUserIds.Remove(userId);
            }
            else
            {
                // Block
                await UserBlockService.BlockUserAsync(userId, caller);
                _blockedUserIds.Add(userId);
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    protected async Task HandleToggleDirectPeerBlock()
    {
        if (SelectedDirectPeerUserId is not Guid userId)
        {
            return;
        }

        await HandleToggleBlockUser(userId);
    }

    private void OnChannelAdded(Guid channelId)
    {
        // A new channel (e.g. a DM started by another user) has been added.
        // Blazor Server runs InvokeAsync callbacks INLINE (up to their first real await) when
        // called from the circuit thread. Guard against the three cases where we should skip:
        //   1. _dmCreationInProgress: this IS the creator's circuit — StartDmWithUserAsync is
        //      mid-flight and will handle everything itself.
        //   2. _channels.Any: the channel was already added (queued callback ran after the fact).
        //   3. _isLoadingChannels: another reload is already in progress.
        _ = InvokeAsync(async () =>
        {
            if (_dmCreationInProgress) return;
            if (_channels.Any(c => c.Id == channelId)) return;
            if (_isLoadingChannels) return;
            await LoadChannelsAsync();
            StateHasChanged();
        });
    }

    private void OnChannelDeleted(Guid channelId)
    {
        // A channel was deleted (e.g. the other DM participant left) — remove it from the sidebar
        // and navigate away if it was the active channel.
        _ = InvokeAsync(async () =>
        {
            _channels.RemoveAll(c => c.Id == channelId);
            _dmChannelToOtherUser.Remove(channelId);

            if (_selectedChannel?.Id == channelId)
            {
                _selectedChannel = null;

                // Navigate to another DM if available, otherwise fall back to the Public channel.
                var next = _channels.FirstOrDefault(c => c.Type is "DirectMessage" or "Group")
                        ?? _channels.FirstOrDefault(c => c.Type is "Public");

                if (next is not null)
                {
                    await HandleChannelSelected(next);
                }
            }

            StateHasChanged();
        });
    }

    private void OnUserBlockStatusChanged(UserBlockStatusChangedNotification notification)
    {
        if (notification.TargetUserId != _currentUserId) return;

        InvokeAsync(() =>
        {
            if (notification.IsBlocked)
            {
                _blockedByUserIds.Add(notification.BlockerUserId);
            }
            else
            {
                _blockedByUserIds.Remove(notification.BlockerUserId);
            }

            StateHasChanged();
        });
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
            _messages = result.Items.Select(ToMessageViewModel).Reverse().ToList();
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
            var older = result.Items.Select(ToMessageViewModel).Reverse().ToList();
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

            // Build inline attachments from pending uploads
            List<CreateAttachmentDto>? attachments = null;
            if (_pendingAttachments.Count > 0)
            {
                attachments = _pendingAttachments.Select(p => new CreateAttachmentDto
                {
                    FileName = p.FileName,
                    MimeType = p.MimeType,
                    FileSize = p.FileSize,
                    ThumbnailUrl = p.Url
                }).ToList();
            }

            var content = args.Content;
            if (string.IsNullOrWhiteSpace(content) && attachments is { Count: > 0 })
            {
                content = " "; // Ensure non-null for messages with only attachments
            }

            var sent = await MessageService.SendMessageAsync(_selectedChannel.Id, new SendMessageDto
            {
                Content = content,
                ReplyToMessageId = args.ReplyToMessageId,
                Attachments = attachments
            }, caller);

            _pendingAttachments.Clear();

            await ResolveDisplayNamesAsync([sent]);
            _messages.Add(ToMessageViewModel(sent));
            _replyToMessage = null;

            await ChatRealtimeService.BroadcastNewMessageAsync(_selectedChannel.Id, sent);
            ChatMessageNotifier.NotifyMessageReceived(_selectedChannel.Id, sent);

            var members = await MemberService.ListMembersAsync(_selectedChannel.Id, caller);
            var preview = BuildMessagePreview(sent.Content);
            var channelName = string.IsNullOrWhiteSpace(_selectedChannel.Name) ? "Channel" : _selectedChannel.Name;
            var senderName = _displayNameCache.GetValueOrDefault(sent.SenderUserId, sent.SenderUserId.ToString()[..8]);

            foreach (var member in members)
            {
                if (member.UserId == caller.UserId || member.IsMuted)
                {
                    continue;
                }

                await ChatRealtimeService.SendNewMessageToastAsync(
                    member.UserId,
                    _selectedChannel.Id,
                    channelName,
                    senderName,
                    preview);

                ChatMessageNotifier.NotifyNewMessageToast(new NewMessageToastNotification(
                    member.UserId,
                    _selectedChannel.Id,
                    channelName,
                    senderName,
                    preview));
            }
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

            if (_selectedChannel is not null)
            {
                await ChatRealtimeService.BroadcastMessageEditedAsync(_selectedChannel.Id, edited);
                ChatMessageNotifier.NotifyMessageEdited(_selectedChannel.Id, edited);
            }
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

            if (_selectedChannel is not null)
            {
                await ChatRealtimeService.BroadcastMessageDeletedAsync(_selectedChannel.Id, messageId);
                ChatMessageNotifier.NotifyMessageDeleted(_selectedChannel.Id, messageId);
            }
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

            // Resolve avatar URLs for members not yet cached
            var memberIdsNeedingAvatars = members
                .Select(m => m.UserId)
                .Where(id => !_displayNameCache.ContainsKey(id))
                .Distinct()
                .ToList();

            if (memberIdsNeedingAvatars.Count > 0)
            {
                var avatarUrls = await UserDirectory.GetAvatarUrlsAsync(memberIdsNeedingAvatars);
                foreach (var (id, url) in avatarUrls)
                {
                    _avatarUrlCache[id] = url;
                }
            }

            _members = members.Select(ToMemberViewModel).ToList();

            // Query actual presence status for all members
            var memberIds = _members.Select(m => m.UserId).ToList();
            var onlineStatus = await PresenceTracker.GetOnlineStatusAsync(memberIds);
            foreach (var member in _members)
            {
                if (onlineStatus.TryGetValue(member.UserId, out var isOnline) && isOnline)
                {
                    member.Status = "Online";
                }
            }

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

    // ── Image Upload & Attachment ──────────────────────────────────

    /// <summary>Handles attach button click — opens the file picker via JS interop.</summary>
    protected async Task HandleAttach()
    {
        try
        {
            var channelId = _selectedChannel?.Id;
            await JS.InvokeVoidAsync("chatImageUpload.triggerFileInput", _fileInputId, _dotNetRef, channelId);
        }
        catch (JSDisconnectedException)
        {
            // Browser disconnected; safe to ignore.
        }
    }

    /// <summary>Handles an image pasted into the composer.</summary>
    protected async Task HandlePasteImage(PastedImageData pastedImage)
    {
        if (_selectedChannel is null) return;

        // If the image was already uploaded via HTTP, just add metadata
        if (!string.IsNullOrEmpty(pastedImage.Url))
        {
            _pendingAttachments.Add(new PendingAttachment
            {
                Id = Guid.NewGuid(),
                FileName = pastedImage.FileName,
                MimeType = pastedImage.ContentType,
                FileSize = pastedImage.SizeBytes,
                Url = pastedImage.Url
            });

            await InvokeAsync(StateHasChanged);
            return;
        }

        // Fallback: image data sent via SignalR (small images only)
        if (pastedImage.Data.Length == 0) return;

        try
        {
            var result = await ChatImageStore.SaveAsync(
                pastedImage.FileName,
                pastedImage.ContentType,
                pastedImage.Data);

            _pendingAttachments.Add(new PendingAttachment
            {
                Id = Guid.NewGuid(),
                FileName = pastedImage.FileName,
                MimeType = result.ContentType,
                FileSize = result.FileSize,
                Url = result.Url
            });

            await InvokeAsync(StateHasChanged);
        }
        catch (ArgumentException)
        {
            // Invalid image — silently ignore
        }
    }

    /// <summary>Called from JS when a file is selected via the file input and uploaded via HTTP.</summary>
    [JSInvokable]
    public async Task HandleImageUploaded(string url, string fileName, string mimeType, long fileSize)
    {
        if (_selectedChannel is null) return;

        _pendingAttachments.Add(new PendingAttachment
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            MimeType = mimeType,
            FileSize = fileSize,
            Url = url
        });

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when a file is selected via the file input (legacy SignalR path).</summary>
    [JSInvokable]
    public async Task HandleFileSelected(string fileName, string contentType, string dataUrl, long sizeBytes)
    {
        if (_selectedChannel is null) return;

        byte[] data;
        try
        {
            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex < 0) return;
            data = Convert.FromBase64String(dataUrl[(commaIndex + 1)..]);
        }
        catch (FormatException)
        {
            return;
        }

        try
        {
            var result = await ChatImageStore.SaveAsync(fileName, contentType, data);

            _pendingAttachments.Add(new PendingAttachment
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                MimeType = result.ContentType,
                FileSize = result.FileSize,
                Url = result.Url
            });

            await InvokeAsync(StateHasChanged);
        }
        catch (ArgumentException)
        {
            // Invalid file type or size — silently ignore
        }
    }

    /// <summary>Removes a pending attachment before sending.</summary>
    protected void HandleRemovePendingAttachment(Guid attachmentId)
    {
        _pendingAttachments.RemoveAll(a => a.Id == attachmentId);
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

    /// <summary>
    /// Resolves the display name for DM channels by parsing the raw "DM-{guid1}-{guid2}" name,
    /// finding the other participant, and replacing the name with their display name.
    /// </summary>
    private async Task ResolveDmChannelNamesAsync()
    {
        var dmChannels = _channels.Where(c => c.Type == "DirectMessage").ToList();
        if (dmChannels.Count == 0) return;

        var dmToOtherUser = new Dictionary<Guid, Guid>();

        foreach (var dm in dmChannels)
        {
            // DM name format: "DM-{guid1}-{guid2}" — each GUID is 36 chars, total 76
            if (dm.Name.StartsWith("DM-", StringComparison.Ordinal) && dm.Name.Length == 76)
            {
                var guid1Str = dm.Name.Substring(3, 36);
                var guid2Str = dm.Name.Substring(40, 36);

                if (Guid.TryParse(guid1Str, out var guid1) && Guid.TryParse(guid2Str, out var guid2))
                {
                    dmToOtherUser[dm.Id] = guid1 == _currentUserId ? guid2 : guid1;
                }
            }
        }

        if (dmToOtherUser.Count == 0) return;

        // Batch-resolve unknown display names
        var unknownIds = dmToOtherUser.Values
            .Where(id => !_displayNameCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (unknownIds.Count > 0)
        {
            var names = await UserDirectory.GetDisplayNamesAsync(unknownIds);
            foreach (var (id, name) in names)
            {
                _displayNameCache[id] = name;
            }

            var avatarUrls = await UserDirectory.GetAvatarUrlsAsync(unknownIds);
            foreach (var (id, url) in avatarUrls)
            {
                _avatarUrlCache[id] = url;
            }
        }

        // Replace raw DM names with resolved display names
        foreach (var dm in dmChannels)
        {
            if (dmToOtherUser.TryGetValue(dm.Id, out var otherUserId))
            {
                dm.Name = _displayNameCache.GetValueOrDefault(otherUserId, otherUserId.ToString()[..8]);
                _dmChannelToOtherUser[dm.Id] = otherUserId;
            }
        }

        // Query presence for DM peers
        var peerIds = dmToOtherUser.Values.Distinct().ToList();
        if (peerIds.Count > 0)
        {
            var onlineStatus = await PresenceTracker.GetOnlineStatusAsync(peerIds);
            foreach (var dm in dmChannels)
            {
                if (dmToOtherUser.TryGetValue(dm.Id, out var peerId)
                    && onlineStatus.TryGetValue(peerId, out var isOnline)
                    && isOnline)
                {
                    dm.PresenceStatus = "Online";
                }
            }
        }
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

        var avatarUrls = await UserDirectory.GetAvatarUrlsAsync(unknownIds);
        foreach (var (id, url) in avatarUrls)
        {
            _avatarUrlCache[id] = url;
        }
    }

    // ── Video Call Operations ───────────────────────────────────────

    private void OnGlobalCallAccepted()
    {
        var pending = GlobalNotificationState.ConsumePendingAccept();
        if (pending is not null)
        {
            _ = InvokeAsync(async () => await HandlePendingCallAcceptAsync(pending));
        }
    }

    private async Task HandlePendingCallAcceptAsync(PendingCallAccept accept)
    {
        _incomingCallId = accept.CallId;
        _incomingCallInitiatorId = accept.InitiatorUserId;
        _incomingCallChannelId = accept.ChannelId;
        await AcceptCallAsync(accept.WithVideo);
    }

    private void OnCallHostTransferred(CallHostTransferredNotification notification)
    {
        if (_currentCallId is null || _currentCallId != notification.CallId)
        {
            return;
        }

        _currentCallHostUserId = notification.NewHostUserId;

        for (var i = 0; i < _remoteParticipants.Count; i++)
        {
            var participant = _remoteParticipants[i];
            _remoteParticipants[i] = new CallParticipantDto
            {
                Id = participant.Id,
                UserId = participant.UserId,
                DisplayName = participant.DisplayName,
                AvatarUrl = participant.AvatarUrl,
                Role = participant.UserId == notification.NewHostUserId ? "Host" : "Participant",
                JoinedAtUtc = participant.JoinedAtUtc,
                LeftAtUtc = participant.LeftAtUtc,
                HasAudio = participant.HasAudio,
                HasVideo = participant.HasVideo,
                HasScreenShare = participant.HasScreenShare
            };
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnCallAccepted(CallAcceptedNotification notification)
    {
        // Only the caller (who initiated this call) should react
        if (_currentCallId is null || _currentCallId != notification.CallId) return;

        _currentCallState = "Active";
        _hasActiveCall = true;
        _callPeerId = notification.AcceptedByUserId;

        var displayName = _displayNameCache.GetValueOrDefault(notification.AcceptedByUserId,
            notification.AcceptedByUserId.ToString()[..8]);
        var existingIndex = _remoteParticipants.FindIndex(p => p.UserId == notification.AcceptedByUserId);
        var acceptedParticipant = new CallParticipantDto
        {
            Id = existingIndex >= 0 ? _remoteParticipants[existingIndex].Id : Guid.NewGuid(),
            UserId = notification.AcceptedByUserId,
            DisplayName = displayName,
            AvatarUrl = _avatarUrlCache.GetValueOrDefault(notification.AcceptedByUserId),
            Role = "Participant",
            HasAudio = true,
            HasVideo = !_isCallCameraOff
        };

        if (existingIndex >= 0)
        {
            _remoteParticipants[existingIndex] = acceptedParticipant;
        }
        else
        {
            _remoteParticipants.Add(acceptedParticipant);
        }

        // Caller initiates the WebRTC offer once the callee has accepted
        _ = InvokeAsync(async () =>
        {
            // Resolve callee's display name if not cached
            if (!_displayNameCache.ContainsKey(notification.AcceptedByUserId))
            {
                var names = await UserDirectory.GetDisplayNamesAsync([notification.AcceptedByUserId]);
                if (names.TryGetValue(notification.AcceptedByUserId, out var resolvedName))
                {
                    _displayNameCache[notification.AcceptedByUserId] = resolvedName;
                    var idx = _remoteParticipants.FindIndex(p => p.UserId == notification.AcceptedByUserId);
                    if (idx >= 0)
                    {
                        var old = _remoteParticipants[idx];
                        _remoteParticipants[idx] = new CallParticipantDto
                        {
                            Id = old.Id,
                            UserId = old.UserId,
                            DisplayName = resolvedName,
                            AvatarUrl = old.AvatarUrl,
                            Role = old.Role,
                            HasAudio = old.HasAudio,
                            HasVideo = old.HasVideo,
                            HasScreenShare = old.HasScreenShare
                        };
                    }
                }
            }

            StateHasChanged();
            await StartWebRtcAsync();
            await _signalingLock.WaitAsync();
            try
            {
                var sdpOffer = await WebRtcInterop.CreateOfferAsync(notification.AcceptedByUserId.ToString());
                if (sdpOffer is not null)
                {
                    var caller = await GetCallerContextAsync();
                    await CallSignalingService.SendOfferAsync(
                        notification.CallId, notification.AcceptedByUserId, sdpOffer, caller);
                }
            }
            finally
            {
                _signalingLock.Release();
            }
        });
    }

    private void OnCallParticipantLeft(CallParticipantLeftNotification notification)
    {
        if (_currentCallId is null || _currentCallId != notification.CallId)
            return;

        // Don't process our own leave
        if (notification.UserId == _currentUserId)
            return;

        _ = InvokeAsync(async () =>
        {
            _remoteParticipants.RemoveAll(p => p.UserId == notification.UserId);

            // Clean up the WebRTC peer connection for the departed user
            if (_webRtcInitialized)
            {
                try
                {
                    await WebRtcInterop.ClosePeerConnectionAsync(notification.UserId.ToString());
                }
                catch (Exception)
                {
                    // Best-effort cleanup for departed peer
                }
            }

            // If no remote participants left, hang up our side too
            if (_remoteParticipants.Count == 0)
            {
                await HandleCallHangUp();
            }

            StateHasChanged();
        });
    }

    private void OnCallEnded(CallEndedNotification notification)
    {
        // If we're in the call that just ended, clean up the UI
        if (_currentCallId is not null && _currentCallId == notification.CallId)
        {
            _ = InvokeAsync(async () =>
            {
                await StopWebRtcAsync();

                // Show the end reason briefly before dismissing the dialog
                var endReason = notification.EndReason;
                _currentCallState = endReason switch
                {
                    "Rejected" => "Rejected",
                    "Cancelled" => "Cancelled",
                    "Missed" => "Missed",
                    _ => "Ended"
                };
                _hasActiveCall = false;
                _currentCallId = null;
                _currentCallHostUserId = null;
                _remoteParticipants = [];
                _showCallAddPeoplePicker = false;
                _callAddPeopleSearchTerm = string.Empty;
                _callAddPeopleResults = [];
                _callDurationSeconds = 0;
                _isCallMuted = false;
                _isCallCameraOff = false;
                _isCallScreenSharing = false;
                _callConnectionQuality = null;
                GlobalNotificationState.SetActiveCallId(null);

                StateHasChanged();

                // Keep the dialog visible briefly so the caller sees the reason
                await Task.Delay(2500);

                _showVideoCallDialog = false;

                // Refresh call history if panel is visible
                if (_showCallHistoryPanel && _selectedChannel is not null)
                {
                    await LoadCallHistoryAsync();
                }

                StateHasChanged();
            });
            return;
        }

        // Incoming call ended (e.g. caller cancelled) — global state handles dismissal
        if (_incomingCallId is not null && _incomingCallId == notification.CallId)
        {
            _ = InvokeAsync(() =>
            {
                _incomingCallId = null;
                _incomingCallInitiatorId = null;
                _incomingCallChannelId = null;
                StateHasChanged();
            });
        }
    }

    private void OnCallSignalReceived(CallSignalNotification notification)
    {
        if (_currentCallId is null || notification.CallId != _currentCallId) return;
        // Only process signals addressed to this user — ignore signals we sent ourselves
        if (notification.ToUserId != _currentUserId) return;
        var peerId = notification.FromUserId.ToString();

        _ = InvokeAsync(async () =>
        {
            await _signalingLock.WaitAsync();
            try
            {
                // Ensure WebRTC is initialized before processing signals
                if (!_webRtcInitialized)
                {
                    await StartWebRtcAsync();
                }

                if (!_webRtcInitialized) return; // Still failed, bail

                switch (notification.Type)
                {
                    case "offer":
                        var sdpAnswer = await WebRtcInterop.HandleOfferAsync(peerId, notification.Payload);
                        if (sdpAnswer is not null)
                        {
                            var caller = await GetCallerContextAsync();
                            await CallSignalingService.SendAnswerAsync(
                                notification.CallId, notification.FromUserId, sdpAnswer, caller);
                        }
                        break;
                    case "answer":
                        await WebRtcInterop.HandleAnswerAsync(peerId, notification.Payload);
                        break;
                    case "ice-candidate":
                        await WebRtcInterop.AddIceCandidateAsync(peerId, notification.Payload);
                        break;
                }
            }
            catch (Exception ex)
            {
                _messageErrorMessage = $"WebRTC signal error: {ex.Message}";
                StateHasChanged();
            }
            finally
            {
                _signalingLock.Release();
            }
        });
    }

    private async Task StartWebRtcAsync()
    {
        if (_webRtcInitialized || _dotNetRef is null || _currentCallId is null) return;

        try
        {
            var iceServers = IceServerService.GetIceServers();
            var config = new WebRtcCallConfig
            {
                CallId = _currentCallId.Value.ToString(),
                IceServers = iceServers,
                IceTransportPolicy = IceServerService.IceTransportPolicy
            };

            var initialized = await WebRtcInterop.InitializeCallAsync(_dotNetRef, config);
            if (!initialized) return;

            _webRtcInitialized = true;

            // StartLocalMediaAsync may fail if the device has no camera/mic — continue without local media
            try
            {
                var streamId = await WebRtcInterop.StartLocalMediaAsync();
                if (streamId is not null)
                {
                    // Choose the correct video element based on current layout state
                    var localVideoElementId = _remoteParticipants.Count > 0
                        ? "local-video-pip"
                        : "local-video-main";
                    await WebRtcInterop.AttachStreamToElementAsync(localVideoElementId, "local");
                }
                else
                {
                    // No local media — mark initial negotiation as done so that
                    // future addTrack (e.g. screen share) renegotiation isn't skipped.
                    _initialNegotiationDone = true;
                }
            }
            catch (Exception mediaEx)
            {
                _messageErrorMessage = $"No camera/mic available: {mediaEx.Message}";
                // No local media — mark initial negotiation as done (same reason as above)
                _initialNegotiationDone = true;
                // Continue — we can still receive remote media
            }

            StartCallDurationTimer();
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to start media: {ex.Message}";
            StateHasChanged();
        }
    }

    private void StartCallDurationTimer()
    {
        _callDurationTimer?.Dispose();
        _callDurationSeconds = 0;
        _callDurationTimer = new System.Timers.Timer(1000);
        _callDurationTimer.Elapsed += (_, _) =>
        {
            _callDurationSeconds++;
            _ = InvokeAsync(StateHasChanged);
        };
        _callDurationTimer.Start();
    }

    private async Task StopWebRtcAsync()
    {
        _callDurationTimer?.Stop();
        _callDurationTimer?.Dispose();
        _callDurationTimer = null;

        if (_webRtcInitialized)
        {
            try { await WebRtcInterop.HangupAsync(); } catch { /* best-effort */ }
            _webRtcInitialized = false;
        }

        _callPeerId = null;
        _initialNegotiationDone = false;
    }

    // ── JSInvokable Callbacks (called from video-call.js) ───────────

    /// <summary>Called by JS when an ICE candidate is gathered for a peer.</summary>
    [JSInvokable]
    public async Task OnIceCandidate(string peerId, string candidateJson)
    {
        if (_currentCallId is null) return;
        await _signalingLock.WaitAsync();
        try
        {
            var caller = await GetCallerContextAsync();
            if (Guid.TryParse(peerId, out var targetUserId))
            {
                await CallSignalingService.SendIceCandidateAsync(
                    _currentCallId.Value, targetUserId, candidateJson, caller);
            }
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"ICE candidate relay failed: {ex.Message}";
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _signalingLock.Release();
        }
    }

    /// <summary>Called by JS when a remote stream is received from a peer.</summary>
    [JSInvokable]
    public async Task OnRemoteStream(string peerId, string streamId, string trackKind)
    {
        // Update participant media flags based on actual tracks received
        if (Guid.TryParse(peerId, out var peerUserId))
        {
            var participant = _remoteParticipants.FirstOrDefault(p => p.UserId == peerUserId);
            if (participant is not null)
            {
                if (string.Equals(trackKind, "video", StringComparison.OrdinalIgnoreCase))
                    participant.HasVideo = true;
                else if (string.Equals(trackKind, "audio", StringComparison.OrdinalIgnoreCase))
                    participant.HasAudio = true;
            }
        }

        // Ensure the DOM is up-to-date before attaching (element may not exist yet)
        await InvokeAsync(StateHasChanged);

        // Small delay to let Blazor flush the render batch so remote-video-{peerId} exists in the DOM
        await Task.Delay(100);

        try
        {
            // For remote streams, pass the peerId as streamType — JS looks up remoteStreams by key
            await WebRtcInterop.AttachStreamToElementAsync($"remote-video-{peerId}", peerId);

            // When remote participants appear, the layout switches from solo to PIP.
            // Re-attach local stream to the PIP element (local-video-main is gone).
            try
            {
                await WebRtcInterop.AttachStreamToElementAsync("local-video-pip", "local");
            }
            catch
            {
                // No local stream or PIP element not rendered — non-fatal
            }
        }
        catch
        {
            // Element may not be rendered yet; will retry on next remote stream event
        }
    }

    /// <summary>Called by JS when a peer connection state changes.</summary>
    [JSInvokable]
    public async Task OnConnectionStateChanged(string peerId, string state)
    {
        if (state == "connected")
        {
            _currentCallState = "Active";
            _hasActiveCall = true;
        }
        else if (state is "disconnected" or "failed")
        {
            _currentCallState = "Reconnecting";
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS on WebRTC errors.</summary>
    [JSInvokable]
    public async Task OnWebRtcError(string context, string message)
    {
        // getUserMedia failures are non-fatal — the call continues without local media.
        // Don't overwrite _messageErrorMessage (which hides the chat message list).
        if (string.Equals(context, "getUserMedia", StringComparison.Ordinal))
        {
            Console.WriteLine($"[WebRTC] getUserMedia unavailable: {message}");
            return;
        }

        _messageErrorMessage = $"WebRTC error ({context}): {message}";
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS when screen sharing state changes.</summary>
    [JSInvokable]
    public async Task OnScreenShareStateChanged(bool isSharing)
    {
        _isCallScreenSharing = isSharing;
        await SendMediaStateChangeAsync("ScreenShare", isSharing);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS when re-negotiation is needed for a peer.</summary>
    [JSInvokable]
    public async Task OnNegotiationNeeded(string peerId)
    {
        if (_currentCallId is null) return;

        // Skip the initial negotiation triggered by addTrack — it's handled explicitly
        if (!_initialNegotiationDone)
        {
            _initialNegotiationDone = true;
            return;
        }

        await _signalingLock.WaitAsync();
        try
        {
            var sdpOffer = await WebRtcInterop.CreateOfferAsync(peerId);
            if (sdpOffer is not null && Guid.TryParse(peerId, out var targetUserId))
            {
                var caller = await GetCallerContextAsync();
                await CallSignalingService.SendOfferAsync(
                    _currentCallId.Value, targetUserId, sdpOffer, caller);
            }
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Re-negotiation failed: {ex.Message}";
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _signalingLock.Release();
        }
    }

    /// <summary>Called by JS when a peer disconnects.</summary>
    [JSInvokable]
    public async Task OnPeerDisconnected(string peerId, string state)
    {
        _remoteParticipants.RemoveAll(p => p.UserId.ToString() == peerId);
        if (_remoteParticipants.Count == 0)
        {
            await HandleCallHangUp();
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS with connection quality metrics.</summary>
    [JSInvokable]
    public async Task OnConnectionQualityUpdate(string peerId, string quality,
        double roundTripTime, double availableBandwidth)
    {
        _callConnectionQuality = quality;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS when a remote track ends.</summary>
    [JSInvokable]
    public async Task OnRemoteTrackEnded(string peerId, string kind)
    {
        var idx = _remoteParticipants.FindIndex(p => p.UserId.ToString() == peerId);
        if (idx >= 0)
        {
            var p = _remoteParticipants[idx];
            _remoteParticipants[idx] = new CallParticipantDto
            {
                Id = p.Id,
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                AvatarUrl = p.AvatarUrl,
                Role = p.Role,
                HasAudio = kind == "audio" ? false : p.HasAudio,
                HasVideo = kind == "video" ? false : p.HasVideo
            };
        }

        await InvokeAsync(StateHasChanged);
    }

    // ── DM User Search & Direct Call ────────────────────────────────

    /// <summary>
    /// Searches for users to start a direct message conversation with.
    /// Uses <see cref="IUserDirectory.SearchUsersAsync"/> for global user lookup.
    /// </summary>
    private async Task SearchUsersForDmAsync(string searchTerm)
    {
        _dmSearchCts?.Cancel();
        _dmSearchCts?.Dispose();
        _dmSearchCts = new CancellationTokenSource();
        var cts = _dmSearchCts;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _dmSearchResults = [];
            _dmSearchError = null;
            return;
        }

        _isDmSearching = true;
        _dmSearchError = null;
        try
        {
            // Derive active DM peers from _channels (stays in sync with deletions)
            var existingDmUserIds = _channels
                .Where(c => c.Type is "DirectMessage" && _dmChannelToOtherUser.ContainsKey(c.Id))
                .Select(c => _dmChannelToOtherUser[c.Id])
                .ToHashSet();

            var results = await UserDirectory.SearchUsersAsync(searchTerm, cancellationToken: cts.Token);
            var filtered = results
                .Where(r => r.Id != _currentUserId && !existingDmUserIds.Contains(r.Id))
                .ToList();
            var avatarUrls = await UserDirectory.GetAvatarUrlsAsync(filtered.Select(r => r.Id), cts.Token);

            if (cts.IsCancellationRequested) return;

            _dmSearchResults = filtered
                .Select(r => new UserSearchResultViewModel
                {
                    UserId = r.Id,
                    DisplayName = r.DisplayName,
                    Email = r.Email,
                    AvatarUrl = avatarUrls.GetValueOrDefault(r.Id)
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer search — no action needed
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                _dmSearchError = $"Search failed: {ex.Message}";
                _dmSearchResults = [];
            }
        }
        finally
        {
            if (!cts.IsCancellationRequested)
                _isDmSearching = false;
        }
    }

    /// <summary>
    /// Creates or navigates to a DM channel with the specified user.
    /// </summary>
    private async Task StartDmWithUserAsync(Guid targetUserId)
    {
        _dmCreationInProgress = true;
        try
        {
            var caller = await GetCallerContextAsync();
            var dm = await ChannelService.GetOrCreateDirectMessageAsync(targetUserId, caller);

            _showDmUserPicker = false;
            _dmSearchTerm = string.Empty;
            _dmSearchResults = [];

            // Find the channel in the already-loaded list or add it directly.
            // IMPORTANT: add to _channels BEFORE any awaits so that the OnChannelAdded callback
            // (queued when ChannelCreatedEvent fires inside GetOrCreateDirectMessageAsync) sees
            // the channel already present and returns early, preventing concurrent DbContext use.
            var targetChannel = _channels.FirstOrDefault(c => c.Id == dm.Id);
            if (targetChannel is null)
            {
                targetChannel = ToChannelViewModel(dm);
                _dmChannelToOtherUser[dm.Id] = targetUserId;
                _channels.Add(targetChannel);
                _channels = _channels
                    .OrderByDescending(c => c.LastActivityAt ?? DateTime.MinValue)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                targetChannel = _channels.First(c => c.Id == dm.Id);
            }

            // Resolve display name and avatar (async — safe now that channel is already in _channels)
            if (!_displayNameCache.TryGetValue(targetUserId, out var displayName))
            {
                var names = await UserDirectory.GetDisplayNamesAsync([targetUserId]);
                displayName = names.GetValueOrDefault(targetUserId, dm.Name);
                _displayNameCache[targetUserId] = displayName;
            }
            targetChannel.Name = displayName;

            if (!_avatarUrlCache.ContainsKey(targetUserId))
            {
                var urls = await UserDirectory.GetAvatarUrlsAsync([targetUserId]);
                if (urls.TryGetValue(targetUserId, out var url))
                    _avatarUrlCache[targetUserId] = url;
            }

            await HandleChannelSelected(targetChannel);
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to start DM: {ex.Message}";
        }
        finally
        {
            _dmCreationInProgress = false;
        }
    }

    /// <summary>
    /// Initiates a direct call to a user by user ID. Creates/reuses DM channel and starts call.
    /// </summary>
    private async Task CallUserDirectlyAsync(Guid targetUserId, string mediaType = "Audio")
    {
        if (_selectedChannel is not null && _selectedChannel.IsMuted)
        {
            _messageErrorMessage = "This channel is muted. Unmute it to start calls.";
            return;
        }

        if (_blockedByUserIds.Contains(targetUserId))
        {
            _messageErrorMessage = "You have been blocked by this user. You cannot place calls.";
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            var result = await VideoCallService.InitiateDirectCallAsync(
                targetUserId,
                new StartCallRequest { MediaType = mediaType },
                caller);

            _currentCallId = result.Id;
            _currentCallHostUserId = result.HostUserId;
            _showVideoCallDialog = true;
            _currentCallState = result.State;
            _isCallCameraOff = mediaType != "Video";
            _remoteParticipants = await BuildRemoteParticipantsAsync(result.Participants);

            // Navigate to the DM channel if not already selected
            var targetChannel = _channels.FirstOrDefault(c => c.Id == result.ChannelId);
            if (targetChannel is not null && (_selectedChannel is null || _selectedChannel.Id != result.ChannelId))
            {
                await HandleChannelSelected(targetChannel);
            }
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to initiate direct call: {ex.Message}";
        }
    }

    private Task HandleAudioCallUser(Guid userId)
    {
        return CallUserDirectlyAsync(userId, "Audio");
    }

    private Task HandleVideoCallUser(Guid userId)
    {
        return CallUserDirectlyAsync(userId, "Video");
    }

    private async Task HandleOpenDmUserPicker()
    {
        _showDmUserPicker = true;
        _dmSearchTerm = string.Empty;
        _dmSearchResults = [];
        await Task.Yield(); // allow the dialog to render before focusing
        await _dmSearchInputRef.FocusAsync();
    }

    private Task HandleOpenChannelAddPeoplePicker()
    {
        if (_selectedChannel is null || _selectedChannel.Type is not ("DirectMessage" or "Group"))
        {
            return Task.CompletedTask;
        }

        _showChannelAddPeoplePicker = true;
        _channelAddPeopleSearchTerm = string.Empty;
        _channelAddPeopleResults = [];
        return Task.CompletedTask;
    }

    private Task HandleCloseChannelAddPeoplePicker()
    {
        _showChannelAddPeoplePicker = false;
        _channelAddPeopleSearchTerm = string.Empty;
        _channelAddPeopleResults = [];
        return Task.CompletedTask;
    }

    private async Task SearchUsersForChannelAddAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || _selectedChannel is null)
        {
            _channelAddPeopleResults = [];
            return;
        }

        _isChannelAddPeopleSearching = true;
        try
        {
            var results = await UserDirectory.SearchUsersAsync(searchTerm);
            var existingMemberIds = _members.Select(m => m.UserId).ToHashSet();
            var filtered = results
                .Where(r => r.Id != _currentUserId && !existingMemberIds.Contains(r.Id))
                .ToList();
            var avatarUrls = await UserDirectory.GetAvatarUrlsAsync(filtered.Select(r => r.Id));

            _channelAddPeopleResults = filtered
                .Select(r => new UserSearchResultViewModel
                {
                    UserId = r.Id,
                    DisplayName = r.DisplayName,
                    Email = r.Email,
                    AvatarUrl = avatarUrls.GetValueOrDefault(r.Id)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to search users: {ex.Message}";
            _channelAddPeopleResults = [];
        }
        finally
        {
            _isChannelAddPeopleSearching = false;
        }
    }

    private async Task AddUserToCurrentChannelAsync(Guid userId)
    {
        if (_selectedChannel is null)
        {
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            await MemberService.AddMemberAsync(_selectedChannel.Id, userId, caller);

            await LoadChannelsAsync();
            var updatedChannel = _channels.FirstOrDefault(c => c.Id == _selectedChannel.Id);
            if (updatedChannel is not null)
            {
                await HandleChannelSelected(updatedChannel);
            }

            _showChannelAddPeoplePicker = false;
            _channelAddPeopleSearchTerm = string.Empty;
            _channelAddPeopleResults = [];
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to add user: {ex.Message}";
        }
    }

    private Task HandleOpenCallAddPeoplePicker()
    {
        if (_currentCallId is null || _currentCallHostUserId != _currentUserId)
        {
            return Task.CompletedTask;
        }

        _showCallAddPeoplePicker = true;
        _callAddPeopleSearchTerm = string.Empty;
        _callAddPeopleResults = [];
        return Task.CompletedTask;
    }

    private Task HandleCloseCallAddPeoplePicker()
    {
        _showCallAddPeoplePicker = false;
        _callAddPeopleSearchTerm = string.Empty;
        _callAddPeopleResults = [];
        return Task.CompletedTask;
    }

    private async Task SearchUsersForCallAddAsync(string searchTerm)
    {
        _callAddPeopleSearchTerm = searchTerm;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _callAddPeopleResults = [];
            return;
        }

        _isCallAddPeopleSearching = true;
        try
        {
            var results = await UserDirectory.SearchUsersAsync(searchTerm);
            var existingParticipantIds = _remoteParticipants.Select(p => p.UserId).Append(_currentUserId).ToHashSet();

            _callAddPeopleResults = results
                .Where(r => !existingParticipantIds.Contains(r.Id))
                .Select(r => new UserSearchResultViewModel
                {
                    UserId = r.Id,
                    DisplayName = r.DisplayName,
                    Email = r.Email
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to search users: {ex.Message}";
            _callAddPeopleResults = [];
        }
        finally
        {
            _isCallAddPeopleSearching = false;
        }
    }

    private async Task InviteUserToCurrentCallAsync(Guid userId)
    {
        if (_currentCallId is null)
        {
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            await VideoCallService.InviteToCallAsync(_currentCallId.Value, userId, caller);
            await SearchUsersForCallAddAsync(_callAddPeopleSearchTerm);
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to invite user to call: {ex.Message}";
        }
    }

    private async Task TransferHostAsync(Guid newHostUserId)
    {
        if (_currentCallId is null)
        {
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            await VideoCallService.TransferHostAsync(_currentCallId.Value, newHostUserId, caller);

            _currentCallHostUserId = newHostUserId;
            for (var i = 0; i < _remoteParticipants.Count; i++)
            {
                var participant = _remoteParticipants[i];
                _remoteParticipants[i] = new CallParticipantDto
                {
                    Id = participant.Id,
                    UserId = participant.UserId,
                    DisplayName = participant.DisplayName,
                    Role = participant.UserId == newHostUserId ? "Host" : "Participant",
                    JoinedAtUtc = participant.JoinedAtUtc,
                    LeftAtUtc = participant.LeftAtUtc,
                    HasAudio = participant.HasAudio,
                    HasVideo = participant.HasVideo,
                    HasScreenShare = participant.HasScreenShare
                };
            }
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to transfer host: {ex.Message}";
        }
    }

    private Task HandleCloseDmUserPicker()
    {
        _dmSearchCts?.Cancel();
        _dmSearchCts?.Dispose();
        _dmSearchCts = null;
        _showDmUserPicker = false;
        _dmSearchTerm = string.Empty;
        _dmSearchResults = [];
        _dmSearchError = null;
        _isDmSearching = false;
        return Task.CompletedTask;
    }

    // ── Call UI Event Handlers ──────────────────────────────────────

    private async Task HandleStartAudioCall()
    {
        await InitiateCallAsync("Audio", cameraOff: true);
    }

    private async Task HandleStartVideoCall()
    {
        await InitiateCallAsync("Video", cameraOff: false);
    }

    private async Task InitiateCallAsync(string mediaType, bool cameraOff)
    {
        if (_selectedChannel is null) return;
        if (_selectedChannel.IsMuted)
        {
            _messageErrorMessage = "This channel is muted. Unmute it to start calls.";
            return;
        }
        if (IsBlockedBySelectedPeer)
        {
            _messageErrorMessage = "You have been blocked by this user. You cannot place calls.";
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            var result = await VideoCallService.InitiateCallAsync(
                _selectedChannel.Id,
                new StartCallRequest { MediaType = mediaType },
                caller);

            _currentCallId = result.Id;
            _currentCallHostUserId = result.HostUserId;
            _showVideoCallDialog = true;
            _currentCallState = result.State;
            _isCallCameraOff = cameraOff;
            _remoteParticipants = await BuildRemoteParticipantsAsync(result.Participants);
            GlobalNotificationState.SetActiveCallId(_currentCallId);
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to start call: {ex.Message}";
        }
    }

    private Task HandleJoinActiveCall()
    {
        _showVideoCallDialog = true;
        if (string.IsNullOrWhiteSpace(_currentCallState) || _currentCallState == "Ringing")
        {
            _currentCallState = "Connecting";
        }
        return Task.CompletedTask;
    }

    private async Task HandleCallToggleMute(bool muted)
    {
        _isCallMuted = muted;
        if (_webRtcInitialized)
        {
            await WebRtcInterop.ToggleAudioAsync(!muted);
        }
        await SendMediaStateChangeAsync("Audio", !muted);
    }

    private async Task HandleCallToggleCamera(bool cameraOff)
    {
        _isCallCameraOff = cameraOff;
        if (_webRtcInitialized)
        {
            await WebRtcInterop.ToggleVideoAsync(!cameraOff);
        }
        await SendMediaStateChangeAsync("Video", !cameraOff);
    }

    private async Task HandleCallToggleScreenShare(bool sharing)
    {
        if (!_webRtcInitialized) return;
        if (sharing)
        {
            await WebRtcInterop.StartScreenShareAsync();
        }
        else
        {
            await WebRtcInterop.StopScreenShareAsync();
        }
        // _isCallScreenSharing is updated via OnScreenShareStateChanged JS callback
    }

    private async Task SendMediaStateChangeAsync(string mediaType, bool enabled)
    {
        if (_currentCallId is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            await CallSignalingService.SendMediaStateChangeAsync(
                _currentCallId.Value, mediaType, enabled, caller);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Call] Failed to send media state change: {ex.Message}");
        }
    }

    private async Task HandleCallHangUp()
    {
        await StopWebRtcAsync();

        if (_currentCallId is not null)
        {
            try
            {
                var caller = await GetCallerContextAsync();
                if (_currentCallHostUserId == _currentUserId)
                {
                    await VideoCallService.EndCallAsync(_currentCallId.Value, caller);
                }
                else
                {
                    await VideoCallService.LeaveCallAsync(_currentCallId.Value, caller);
                }
            }
            catch (Exception ex)
            {
                _messageErrorMessage = $"Failed to leave call: {ex.Message}";
            }
        }

        _showVideoCallDialog = false;
        _currentCallState = "Ended";
        _hasActiveCall = false;
        _currentCallId = null;
        _currentCallHostUserId = null;
        _remoteParticipants = [];
        _showCallAddPeoplePicker = false;
        _callAddPeopleSearchTerm = string.Empty;
        _callAddPeopleResults = [];
        _callDurationSeconds = 0;
        _isCallMuted = false;
        _isCallCameraOff = false;
        _isCallScreenSharing = false;
        _callConnectionQuality = null;
        GlobalNotificationState.SetActiveCallId(null);

        // Refresh call history so the ended call appears immediately
        if (_showCallHistoryPanel && _selectedChannel is not null)
        {
            await LoadCallHistoryAsync();
        }
    }

    private Task HandleCallMinimize()
    {
        _showVideoCallDialog = false;
        return Task.CompletedTask;
    }

    private Task HandleCallPipClick()
    {
        return Task.CompletedTask;
    }

    private async Task AcceptCallAsync(bool withVideo)
    {
        if (_incomingCallId is null) return;
        try
        {
            var caller = await GetCallerContextAsync();
            var result = await VideoCallService.JoinCallAsync(
                _incomingCallId.Value,
                new JoinCallRequest { WithAudio = true, WithVideo = withVideo },
                caller);

            _currentCallId = result.Id;
            _currentCallHostUserId = result.HostUserId;
            _showVideoCallDialog = true;
            _currentCallState = result.State;
            _isCallCameraOff = !withVideo;
            _hasActiveCall = true;
            GlobalNotificationState.SetActiveCallId(_currentCallId);

            _remoteParticipants = await BuildRemoteParticipantsAsync(result.Participants);
            if (_incomingCallInitiatorId is not null)
            {
                _callPeerId = _incomingCallInitiatorId;
            }

            if (_incomingCallChannelId is not null && (_selectedChannel is null || _selectedChannel.Id != _incomingCallChannelId.Value))
            {
                var targetChannel = _channels.FirstOrDefault(c => c.Id == _incomingCallChannelId.Value);
                if (targetChannel is not null)
                {
                    await HandleChannelSelected(targetChannel);
                }
            }

            _incomingCallId = null;
            _incomingCallInitiatorId = null;
            _incomingCallChannelId = null;

            // Callee starts WebRTC and waits for the caller's offer via OnCallSignalReceived
            await StartWebRtcAsync();
        }
        catch (Exception ex)
        {
            _incomingCallId = null;
            _incomingCallInitiatorId = null;
            _incomingCallChannelId = null;
            _messageErrorMessage = $"Failed to join call: {ex.Message}";
        }
    }

    private async Task HandleToggleCallHistory()
    {
        _showCallHistoryPanel = !_showCallHistoryPanel;
        if (_showCallHistoryPanel && _selectedChannel is not null)
        {
            await LoadCallHistoryAsync();
        }
    }

    private Task HandleCloseCallHistory()
    {
        _showCallHistoryPanel = false;
        return Task.CompletedTask;
    }

    private async Task HandleLoadMoreCallHistory()
    {
        if (_selectedChannel is null || _isLoadingMoreCallHistory || !_hasMoreCallHistory) return;

        _isLoadingMoreCallHistory = true;
        try
        {
            var caller = await GetCallerContextAsync();
            var more = await VideoCallService.GetCallHistoryAsync(
                _selectedChannel.Id, _callHistory.Count, 20, caller);
            _callHistory.AddRange(more);
            _hasMoreCallHistory = more.Count >= 20;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to load call history: {ex.Message}";
        }
        finally
        {
            _isLoadingMoreCallHistory = false;
        }
    }

    private async Task LoadCallHistoryAsync()
    {
        if (_selectedChannel is null) return;

        _isLoadingCallHistory = true;
        try
        {
            var caller = await GetCallerContextAsync();
            var history = await VideoCallService.GetCallHistoryAsync(
                _selectedChannel.Id, 0, 20, caller);
            _callHistory = [.. history];
            _hasMoreCallHistory = history.Count >= 20;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = $"Failed to load call history: {ex.Message}";
        }
        finally
        {
            _isLoadingCallHistory = false;
        }
    }

    private Task HandleCallBack(string mediaType)
    {
        if (mediaType == "Video")
        {
            return HandleStartVideoCall();
        }

        return HandleStartAudioCall();
    }

    private async Task<List<CallParticipantDto>> BuildRemoteParticipantsAsync(IReadOnlyList<CallParticipantDto> participants)
    {
        var remote = participants
            .Where(p => p.UserId != _currentUserId)
            .Select(p => new CallParticipantDto
            {
                Id = p.Id,
                UserId = p.UserId,
                DisplayName = p.DisplayName,
                Role = p.Role,
                JoinedAtUtc = p.JoinedAtUtc,
                LeftAtUtc = p.LeftAtUtc,
                HasAudio = p.HasAudio,
                HasVideo = p.HasVideo,
                HasScreenShare = p.HasScreenShare
            })
            .ToList();

        var missingNames = remote
            .Where(p => string.IsNullOrWhiteSpace(p.DisplayName))
            .Select(p => p.UserId)
            .Distinct()
            .ToList();

        if (missingNames.Count > 0)
        {
            var names = await UserDirectory.GetDisplayNamesAsync(missingNames);
            for (var i = 0; i < remote.Count; i++)
            {
                var participant = remote[i];
                if (!string.IsNullOrWhiteSpace(participant.DisplayName))
                {
                    continue;
                }

                if (names.TryGetValue(participant.UserId, out var resolvedName))
                {
                    _displayNameCache[participant.UserId] = resolvedName;
                    remote[i] = new CallParticipantDto
                    {
                        Id = participant.Id,
                        UserId = participant.UserId,
                        DisplayName = resolvedName,
                        Role = participant.Role,
                        JoinedAtUtc = participant.JoinedAtUtc,
                        LeftAtUtc = participant.LeftAtUtc,
                        HasAudio = participant.HasAudio,
                        HasVideo = participant.HasVideo,
                        HasScreenShare = participant.HasScreenShare
                    };
                }
            }
        }

        return remote;
    }

    private MessageViewModel ToMessageViewModel(MessageDto dto)
    {
        return new MessageViewModel
        {
            Id = dto.Id,
            SenderUserId = dto.SenderUserId,
            SenderName = _displayNameCache.GetValueOrDefault(dto.SenderUserId, dto.SenderUserId.ToString()[..8]),
            SenderAvatarUrl = _avatarUrlCache.GetValueOrDefault(dto.SenderUserId),
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
            AvatarUrl = _avatarUrlCache.GetValueOrDefault(dto.UserId),
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

    private static string BuildMessagePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "(no text)";
        }

        var preview = content.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return preview.Length <= 120 ? preview : $"{preview[..120]}...";
    }
}
