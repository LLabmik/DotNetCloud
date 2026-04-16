using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Host.Protos;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Host.Services;

/// <summary>
/// gRPC service implementation for the Chat module.
/// Exposes channel and message operations over gRPC for the core server to invoke.
/// </summary>
public sealed class ChatGrpcService : ChatService.ChatServiceBase
{
    private readonly ChatDbContext _db;
    private readonly IChannelService _channelService;
    private readonly IVideoCallService _videoCallService;
    private readonly ILogger<ChatGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatGrpcService"/> class.
    /// </summary>
    public ChatGrpcService(
        ChatDbContext db,
        IChannelService channelService,
        IVideoCallService videoCallService,
        ILogger<ChatGrpcService> logger)
    {
        _db = db;
        _channelService = channelService;
        _videoCallService = videoCallService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ChannelResponse> CreateChannel(
        CreateChannelRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new ChannelResponse { Success = false, ErrorMessage = "Invalid user ID format." };
        }

        Guid? organizationId = string.IsNullOrEmpty(request.OrganizationId)
            ? null
            : Guid.TryParse(request.OrganizationId, out var orgId) ? orgId : null;

        var memberIds = request.MemberIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();

        var dto = new CreateChannelDto
        {
            Name = request.Name,
            Description = string.IsNullOrEmpty(request.Description) ? null : request.Description,
            Type = request.Type,
            Topic = string.IsNullOrEmpty(request.Topic) ? null : request.Topic,
            OrganizationId = organizationId,
            MemberIds = memberIds
        };

        try
        {
            var result = await _channelService.CreateChannelAsync(
                dto, new CallerContext(userId, ["user"], CallerType.User), context.CancellationToken);

            return new ChannelResponse
            {
                Success = true,
                Channel = ToChannelMessage(result)
            };
        }
        catch (Exception ex) when (ex is ArgumentException or DotNetCloud.Core.Errors.ValidationException)
        {
            return new ChannelResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<ChannelResponse> GetChannel(
        GetChannelRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ChannelId, out var channelId))
        {
            return new ChannelResponse { Success = false, ErrorMessage = "Invalid channel ID format." };
        }

        var channel = await _db.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId, context.CancellationToken);

        if (channel is null)
        {
            return new ChannelResponse { Success = false, ErrorMessage = "Channel not found." };
        }

        var memberCount = await _db.ChannelMembers
            .CountAsync(m => m.ChannelId == channelId, context.CancellationToken);

        return new ChannelResponse { Success = true, Channel = ToChannelMessage(channel, memberCount) };
    }

    /// <inheritdoc />
    public override async Task<ListChannelsResponse> ListChannels(
        ListChannelsRequest request, ServerCallContext context)
    {
        var response = new ListChannelsResponse();

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return response;
        }

        var channelIds = await _db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.ChannelId)
            .ToListAsync(context.CancellationToken);

        var channels = await _db.Channels
            .AsNoTracking()
            .Where(c => channelIds.Contains(c.Id))
            .OrderByDescending(c => c.LastActivityAt ?? c.CreatedAt)
            .ToListAsync(context.CancellationToken);

        foreach (var channel in channels)
        {
            var memberCount = await _db.ChannelMembers
                .CountAsync(m => m.ChannelId == channel.Id, context.CancellationToken);
            response.Channels.Add(ToChannelMessage(channel, memberCount));
        }

        return response;
    }

    /// <inheritdoc />
    public override async Task<MessageResponse> SendMessage(
        SendMessageRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new MessageResponse { Success = false, ErrorMessage = "Message content is required." };
        }

        if (!Guid.TryParse(request.ChannelId, out var channelId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new MessageResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        Guid? replyToId = string.IsNullOrEmpty(request.ReplyToMessageId)
            ? null
            : Guid.TryParse(request.ReplyToMessageId, out var rid) ? rid : null;

        var message = new Message
        {
            ChannelId = channelId,
            SenderUserId = userId,
            Content = request.Content,
            Type = replyToId.HasValue ? MessageType.Reply : MessageType.Text,
            ReplyToMessageId = replyToId
        };

        _db.Messages.Add(message);

        // Update channel last activity
        var channel = await _db.Channels.FindAsync([channelId], context.CancellationToken);
        if (channel is not null)
        {
            channel.LastActivityAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Message {MessageId} sent in channel {ChannelId} by user {UserId}",
            message.Id, channelId, userId);

        return new MessageResponse { Success = true, Message = ToChatMessageMessage(message) };
    }

    /// <inheritdoc />
    public override async Task<GetMessagesResponse> GetMessages(
        GetMessagesRequest request, ServerCallContext context)
    {
        var response = new GetMessagesResponse();

        if (!Guid.TryParse(request.ChannelId, out var channelId))
        {
            return response;
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.SentAt);

        response.TotalCount = await query.CountAsync(context.CancellationToken);

        var messages = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Attachments)
            .ToListAsync(context.CancellationToken);

        foreach (var msg in messages)
        {
            response.Messages.Add(ToChatMessageMessage(msg));
        }

        return response;
    }

    /// <inheritdoc />
    public override async Task<MessageResponse> EditMessage(
        EditMessageRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MessageId, out var messageId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new MessageResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var message = await _db.Messages.FindAsync([messageId], context.CancellationToken);
        if (message is null)
        {
            return new MessageResponse { Success = false, ErrorMessage = "Message not found." };
        }

        if (message.SenderUserId != userId)
        {
            return new MessageResponse { Success = false, ErrorMessage = "Only the sender can edit this message." };
        }

        message.Content = request.NewContent;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        return new MessageResponse { Success = true, Message = ToChatMessageMessage(message) };
    }

    /// <inheritdoc />
    public override async Task<DeleteMessageResponse> DeleteMessage(
        DeleteMessageRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MessageId, out var messageId))
        {
            return new DeleteMessageResponse { Success = false, ErrorMessage = "Invalid message ID format." };
        }

        var message = await _db.Messages.FindAsync([messageId], context.CancellationToken);
        if (message is null)
        {
            return new DeleteMessageResponse { Success = false, ErrorMessage = "Message not found." };
        }

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Message {MessageId} deleted", messageId);

        return new DeleteMessageResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<ReactionResponse> AddReaction(
        AddReactionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MessageId, out var messageId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new ReactionResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var exists = await _db.MessageReactions.AnyAsync(
            r => r.MessageId == messageId && r.UserId == userId && r.Emoji == request.Emoji,
            context.CancellationToken);

        if (exists)
        {
            return new ReactionResponse { Success = false, ErrorMessage = "Reaction already exists." };
        }

        _db.MessageReactions.Add(new MessageReaction
        {
            MessageId = messageId,
            UserId = userId,
            Emoji = request.Emoji
        });

        await _db.SaveChangesAsync(context.CancellationToken);

        return new ReactionResponse { Success = true };
    }

    /// <inheritdoc />
    public override async Task<ReactionResponse> RemoveReaction(
        RemoveReactionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MessageId, out var messageId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new ReactionResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var reaction = await _db.MessageReactions.FirstOrDefaultAsync(
            r => r.MessageId == messageId && r.UserId == userId && r.Emoji == request.Emoji,
            context.CancellationToken);

        if (reaction is null)
        {
            return new ReactionResponse { Success = false, ErrorMessage = "Reaction not found." };
        }

        _db.MessageReactions.Remove(reaction);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new ReactionResponse { Success = true };
    }

    /// <inheritdoc />
    public override Task<TypingResponse> NotifyTyping(
        TypingRequest request, ServerCallContext context)
    {
        // Typing indicators are handled in-memory; no persistence needed
        _logger.LogDebug("User {UserId} typing in channel {ChannelId}", request.UserId, request.ChannelId);
        return Task.FromResult(new TypingResponse { Success = true });
    }

    // ── Video Call gRPC Methods ─────────────────────────────────────

    /// <inheritdoc />
    public override async Task<VideoCallResponse> InitiateVideoCall(
        InitiateVideoCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ChannelId, out var channelId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var caller = new CallerContext(userId, ["user"], CallerType.User);
            var callDto = await _videoCallService.InitiateCallAsync(
                channelId,
                new StartCallRequest { MediaType = request.MediaType },
                caller,
                context.CancellationToken);

            return new VideoCallResponse { Success = true, Call = ToVideoCallMessage(callDto) };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new VideoCallResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<VideoCallResponse> JoinVideoCall(
        JoinVideoCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CallId, out var callId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var caller = new CallerContext(userId, ["user"], CallerType.User);
            var callDto = await _videoCallService.JoinCallAsync(
                callId,
                new JoinCallRequest { WithAudio = request.WithAudio, WithVideo = request.WithVideo },
                caller,
                context.CancellationToken);

            return new VideoCallResponse { Success = true, Call = ToVideoCallMessage(callDto) };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new VideoCallResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<VideoCallOperationResponse> LeaveVideoCall(
        LeaveVideoCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CallId, out var callId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var caller = new CallerContext(userId, ["user"], CallerType.User);
            await _videoCallService.LeaveCallAsync(callId, caller, context.CancellationToken);
            return new VideoCallOperationResponse { Success = true };
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<VideoCallOperationResponse> EndVideoCall(
        EndVideoCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CallId, out var callId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var caller = new CallerContext(userId, ["user"], CallerType.User);
            await _videoCallService.EndCallAsync(callId, caller, context.CancellationToken);
            return new VideoCallOperationResponse { Success = true };
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<VideoCallOperationResponse> RejectVideoCall(
        RejectVideoCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CallId, out var callId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        try
        {
            var caller = new CallerContext(userId, ["user"], CallerType.User);
            await _videoCallService.RejectCallAsync(callId, caller, context.CancellationToken);
            return new VideoCallOperationResponse { Success = true };
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return new VideoCallOperationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public override async Task<GetCallHistoryResponse> GetCallHistory(
        GetCallHistoryRequest request, ServerCallContext context)
    {
        var response = new GetCallHistoryResponse();

        if (!Guid.TryParse(request.ChannelId, out var channelId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return response;
        }

        var caller = new CallerContext(userId, ["user"], CallerType.User);
        var history = await _videoCallService.GetCallHistoryAsync(
            channelId, request.Skip, request.Take, caller, context.CancellationToken);

        foreach (var entry in history)
        {
            response.Calls.Add(ToCallHistoryMessage(entry));
        }

        return response;
    }

    /// <inheritdoc />
    public override async Task<VideoCallResponse> GetActiveCall(
        GetActiveCallRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ChannelId, out var channelId) ||
            !Guid.TryParse(request.UserId, out var userId))
        {
            return new VideoCallResponse { Success = false, ErrorMessage = "Invalid ID format." };
        }

        var caller = new CallerContext(userId, ["user"], CallerType.User);
        var call = await _videoCallService.GetActiveCallAsync(channelId, caller, context.CancellationToken);

        if (call is null)
        {
            return new VideoCallResponse { Success = true }; // No active call, but not an error
        }

        return new VideoCallResponse { Success = true, Call = ToVideoCallMessage(call) };
    }

    private static ChannelMessage ToChannelMessage(ChannelDto dto)
    {
        return new ChannelMessage
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            Type = dto.Type,
            Topic = dto.Topic ?? string.Empty,
            AvatarUrl = dto.AvatarUrl ?? string.Empty,
            IsArchived = dto.IsArchived,
            MemberCount = dto.MemberCount,
            CreatedAt = dto.CreatedAt.ToString("O"),
            LastActivityAt = dto.LastActivityAt?.ToString("O") ?? string.Empty,
            CreatedByUserId = dto.CreatedByUserId.ToString()
        };
    }

    private static ChannelMessage ToChannelMessage(Channel channel, int memberCount)
    {
        return new ChannelMessage
        {
            Id = channel.Id.ToString(),
            Name = channel.Name,
            Description = channel.Description ?? string.Empty,
            Type = channel.Type.ToString(),
            Topic = channel.Topic ?? string.Empty,
            AvatarUrl = channel.AvatarUrl ?? string.Empty,
            IsArchived = channel.IsArchived,
            MemberCount = memberCount,
            CreatedAt = channel.CreatedAt.ToString("O"),
            LastActivityAt = channel.LastActivityAt?.ToString("O") ?? string.Empty,
            CreatedByUserId = channel.CreatedByUserId.ToString()
        };
    }

    private static ChatMessageMessage ToChatMessageMessage(Message message)
    {
        var msg = new ChatMessageMessage
        {
            Id = message.Id.ToString(),
            ChannelId = message.ChannelId.ToString(),
            SenderUserId = message.SenderUserId.ToString(),
            Content = message.Content,
            Type = message.Type.ToString(),
            SentAt = message.SentAt.ToString("O"),
            EditedAt = message.EditedAt?.ToString("O") ?? string.Empty,
            IsEdited = message.IsEdited,
            ReplyToMessageId = message.ReplyToMessageId?.ToString() ?? string.Empty
        };

        foreach (var attachment in message.Attachments)
        {
            msg.Attachments.Add(new AttachmentMessage
            {
                Id = attachment.Id.ToString(),
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
                FileSize = attachment.FileSize,
                ThumbnailUrl = attachment.ThumbnailUrl ?? string.Empty,
                FileNodeId = attachment.FileNodeId?.ToString() ?? string.Empty
            });
        }

        return msg;
    }

    /// <inheritdoc />
    public override async Task GetSearchableDocuments(
        GetSearchableDocumentsRequest request,
        IServerStreamWriter<SearchableDocument> responseStream,
        ServerCallContext context)
    {
        var messages = await _db.Messages
            .Include(m => m.Channel)
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.Id)
            .ToListAsync(context.CancellationToken);

        foreach (var message in messages)
        {
            var doc = MapToSearchableDocument(message);
            await responseStream.WriteAsync(doc, context.CancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<SearchableDocumentResponse> GetSearchableDocument(
        GetSearchableDocumentRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.EntityId, out var entityId))
            return new SearchableDocumentResponse { Found = false };

        var message = await _db.Messages
            .Include(m => m.Channel)
            .FirstOrDefaultAsync(m => m.Id == entityId && !m.IsDeleted, context.CancellationToken);

        if (message is null)
            return new SearchableDocumentResponse { Found = false };

        return new SearchableDocumentResponse
        {
            Found = true,
            Document = MapToSearchableDocument(message)
        };
    }

    private static SearchableDocument MapToSearchableDocument(Message message)
    {
        var doc = new SearchableDocument
        {
            ModuleId = "chat",
            EntityId = message.Id.ToString(),
            EntityType = "Message",
            Title = message.Channel?.Name ?? "Direct Message",
            Content = message.Content,
            Summary = message.Content.Length > 200
                ? message.Content[..200] + "..."
                : message.Content,
            OwnerId = message.SenderUserId.ToString(),
            CreatedAt = message.SentAt.ToString("O"),
            UpdatedAt = (message.EditedAt ?? message.SentAt).ToString("O")
        };

        doc.Metadata["ChannelId"] = message.ChannelId.ToString();
        doc.Metadata["SenderId"] = message.SenderUserId.ToString();
        doc.Metadata["MessageType"] = message.Type.ToString();

        return doc;
    }

    private static VideoCallMessage ToVideoCallMessage(VideoCallDto dto)
    {
        var msg = new VideoCallMessage
        {
            Id = dto.Id.ToString(),
            ChannelId = dto.ChannelId.ToString(),
            InitiatorUserId = dto.InitiatorUserId.ToString(),
            State = dto.State,
            MediaType = dto.MediaType,
            IsGroupCall = dto.IsGroupCall,
            StartedAtUtc = dto.StartedAtUtc?.ToString("O") ?? string.Empty,
            EndedAtUtc = dto.EndedAtUtc?.ToString("O") ?? string.Empty,
            EndReason = dto.EndReason ?? string.Empty,
            MaxParticipants = dto.MaxParticipants,
            CreatedAtUtc = dto.CreatedAtUtc.ToString("O")
        };

        foreach (var participant in dto.Participants)
        {
            msg.Participants.Add(new CallParticipantMessage
            {
                Id = participant.Id.ToString(),
                UserId = participant.UserId.ToString(),
                Role = participant.Role,
                JoinedAtUtc = participant.JoinedAtUtc.ToString("O"),
                LeftAtUtc = participant.LeftAtUtc?.ToString("O") ?? string.Empty,
                HasAudio = participant.HasAudio,
                HasVideo = participant.HasVideo,
                HasScreenShare = participant.HasScreenShare
            });
        }

        return msg;
    }

    private static CallHistoryMessage ToCallHistoryMessage(CallHistoryDto dto)
    {
        return new CallHistoryMessage
        {
            Id = dto.Id.ToString(),
            ChannelId = dto.ChannelId.ToString(),
            InitiatorUserId = dto.InitiatorUserId.ToString(),
            State = dto.State,
            MediaType = dto.MediaType,
            EndReason = dto.EndReason ?? string.Empty,
            DurationSeconds = dto.DurationSeconds ?? 0,
            ParticipantCount = dto.ParticipantCount,
            CreatedAtUtc = dto.CreatedAtUtc.ToString("O")
        };
    }
}
