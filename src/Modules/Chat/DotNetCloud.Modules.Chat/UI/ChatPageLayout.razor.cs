using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the chat page layout orchestrator.
/// Manages channel selection, message loading, and message sending.
/// </summary>
public partial class ChatPageLayout : ComponentBase
{
    private const int MessagePageSize = 50;

    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private IMessageService MessageService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private List<ChannelViewModel> _channels = [];
    private ChannelViewModel? _selectedChannel;
    private List<MessageViewModel> _messages = [];
    private List<TypingUserViewModel> _typingUsers = [];
    private List<MemberViewModel> _memberSuggestions = [];
    private MessageViewModel? _replyToMessage;
    private Guid _currentUserId;

    private bool _isLoadingChannels;
    private bool _isLoadingMessages;
    private string? _channelErrorMessage;
    private string? _messageErrorMessage;
    private bool _hasMoreMessages;
    private int _currentMessagePage = 1;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var caller = await GetCallerContextAsync();
        _currentUserId = caller.UserId;
        await LoadChannelsAsync();
    }

    private async Task LoadChannelsAsync()
    {
        try
        {
            _isLoadingChannels = true;
            _channelErrorMessage = null;

            var caller = await GetCallerContextAsync();
            var channels = await ChannelService.ListChannelsAsync(caller);
            _channels = channels.Select(ToChannelViewModel).ToList();
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

    /// <summary>Handles channel selection from the sidebar.</summary>
    protected async Task HandleChannelSelected(ChannelViewModel channel)
    {
        _selectedChannel = channel;
        _replyToMessage = null;
        _messages = [];
        _currentMessagePage = 1;
        await LoadMessagesAsync(channel.Id);
    }

    private async Task LoadMessagesAsync(Guid channelId)
    {
        try
        {
            _isLoadingMessages = true;
            _messageErrorMessage = null;

            var caller = await GetCallerContextAsync();
            var result = await MessageService.GetMessagesAsync(channelId, _currentMessagePage, MessagePageSize, caller);
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
        if (_selectedChannel is null || !_hasMoreMessages)
        {
            return;
        }

        try
        {
            _currentMessagePage++;
            var caller = await GetCallerContextAsync();
            var result = await MessageService.GetMessagesAsync(_selectedChannel.Id, _currentMessagePage, MessagePageSize, caller);
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
        if (_selectedChannel is null)
        {
            return;
        }

        try
        {
            var caller = await GetCallerContextAsync();
            var sent = await MessageService.SendMessageAsync(_selectedChannel.Id, new SendMessageDto
            {
                Content = args.Content,
                ReplyToMessageId = args.ReplyToMessageId
            }, caller);

            _messages.Add(ToMessageViewModel(sent));
            _replyToMessage = null;
        }
        catch (Exception ex)
        {
            _messageErrorMessage = ex.Message;
        }
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

    /// <summary>Handles toggling a reaction on a message.</summary>
    protected Task HandleReactionToggle((Guid MessageId, string Emoji) args)
    {
        // Reaction toggling will be wired to the API when the reaction endpoint is available.
        return Task.CompletedTask;
    }

    /// <summary>Handles channel reorder from drag-and-drop.</summary>
    protected Task HandleChannelReordered(IReadOnlyList<Guid> newOrder)
    {
        // Pinned order is persisted client-side for now.
        return Task.CompletedTask;
    }

    /// <summary>Handles cancel reply action.</summary>
    protected Task HandleCancelReply()
    {
        _replyToMessage = null;
        return Task.CompletedTask;
    }

    /// <summary>Handles typing indicator action.</summary>
    protected Task HandleTyping()
    {
        // Typing indicators will be wired via SignalR when available.
        return Task.CompletedTask;
    }

    /// <summary>Handles attach button click.</summary>
    protected Task HandleAttach()
    {
        // File attachment will be wired to the Files module when available.
        return Task.CompletedTask;
    }

    /// <summary>Handles edit channel action.</summary>
    protected Task HandleEditChannel(ChannelViewModel channel)
    {
        // Channel settings dialog will be wired here.
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
            }
        }
        catch (Exception ex)
        {
            _channelErrorMessage = ex.Message;
        }
    }

    /// <summary>Handles leave channel action.</summary>
    protected Task HandleLeaveChannel(ChannelViewModel channel)
    {
        // Leave channel will be wired to the membership API when available.
        return Task.CompletedTask;
    }

    /// <summary>Handles toggle member list action.</summary>
    protected Task HandleToggleMemberList()
    {
        // Member list panel toggle will be added here.
        return Task.CompletedTask;
    }

    /// <summary>Handles search action.</summary>
    protected Task HandleSearch()
    {
        // Message search will be wired here.
        return Task.CompletedTask;
    }

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

    private MessageViewModel ToMessageViewModel(MessageDto dto)
    {
        return new MessageViewModel
        {
            Id = dto.Id,
            SenderUserId = dto.SenderUserId,
            SenderName = string.Empty, // Display name resolution is not yet available
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
}
