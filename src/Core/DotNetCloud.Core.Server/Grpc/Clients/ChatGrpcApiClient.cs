using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetCloud.Core.DTOs.Chat;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Chat.Host.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

/// <summary>
/// Options for the Chat gRPC client used by the Core Server.
/// </summary>
public sealed class ChatGrpcClientOptions
{
    /// <summary>The section name in appsettings.json.</summary>
    public const string SectionName = "ChatGrpc";
    /// <summary>The gRPC address of the Chat module.</summary>
    public string ChatModuleAddress { get; set; } = "http://localhost:5009";
    /// <summary>Timeout for gRPC calls. Default: 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// gRPC implementation of <see cref="IChatApiClient"/>.
/// </summary>
public sealed class ChatGrpcApiClient : IChatApiClient, IDisposable
{
    private readonly ChatGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<ChatGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<ChatService.ChatServiceClient> _client;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ChatGrpcApiClient"/> class.</summary>
    public ChatGrpcApiClient(
        IOptions<ChatGrpcClientOptions> options,
        ModuleEndpointProvider endpointProvider,
        ILogger<ChatGrpcApiClient> logger)
    {
        _options = options.Value;
        _endpointProvider = endpointProvider;
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<ChatService.ChatServiceClient>(() => new ChatService.ChatServiceClient(_channel.Value));
    }

    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.chat");
        _logger.LogInformation("ChatGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5)
            }
        });
    }

    private ChatService.ChatServiceClient Client => _client.Value;

    private CallOptions GetCallOptions(CancellationToken cancellationToken)
    {
        return new CallOptions(
            deadline: DateTime.UtcNow.Add(_options.Timeout),
            cancellationToken: cancellationToken);
    }

    // ── Channel Operations ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ChatChannelDto?> CreateChannelAsync(
        string name,
        string description,
        string type,
        string topic,
        Guid userId,
        IReadOnlyList<Guid> memberIds,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateChannelRequest
        {
            Name = name,
            Description = description ?? string.Empty,
            Type = type,
            Topic = topic ?? string.Empty,
            UserId = userId.ToString(),
            OrganizationId = organizationId?.ToString() ?? string.Empty,
        };
        request.MemberIds.AddRange(memberIds.Select(id => id.ToString()));

        var response = await Client.CreateChannelAsync(request, GetCallOptions(cancellationToken));
        return response.Success ? ToChatChannelDto(response.Channel) : null;
    }

    /// <inheritdoc />
    public async Task<ChatChannelDto?> GetChannelAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        var request = new GetChannelRequest { ChannelId = channelId.ToString() };
        var response = await Client.GetChannelAsync(request, GetCallOptions(cancellationToken));
        return response.Success ? ToChatChannelDto(response.Channel) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatChannelDto>> ListChannelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new ListChannelsRequest { UserId = userId.ToString() };
        var response = await Client.ListChannelsAsync(request, GetCallOptions(cancellationToken));
        return response.Channels.Select(ToChatChannelDto).Where(c => c is not null).Cast<ChatChannelDto>().ToList().AsReadOnly();
    }

    // ── Message Operations ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ChatMessageDto?> SendMessageAsync(
        Guid channelId,
        Guid userId,
        string content,
        Guid? replyToMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SendMessageRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            Content = content,
            ReplyToMessageId = replyToMessageId?.ToString() ?? string.Empty,
        };

        var response = await Client.SendMessageAsync(request, GetCallOptions(cancellationToken));
        return response.Success ? ToChatMessageDto(response.Message) : null;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ChatMessageDto> Messages, int TotalCount)> GetMessagesAsync(
        Guid channelId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var request = new GetMessagesRequest
        {
            ChannelId = channelId.ToString(),
            Page = page,
            PageSize = pageSize,
        };

        var response = await Client.GetMessagesAsync(request, GetCallOptions(cancellationToken));
        var messages = response.Messages.Select(ToChatMessageDto).ToList().AsReadOnly();
        return (messages, response.TotalCount);
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto?> EditMessageAsync(
        Guid messageId,
        Guid userId,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        var request = new EditMessageRequest
        {
            MessageId = messageId.ToString(),
            UserId = userId.ToString(),
            NewContent = newContent,
        };

        var response = await Client.EditMessageAsync(request, GetCallOptions(cancellationToken));
        return response.Success ? ToChatMessageDto(response.Message) : null;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new DeleteMessageRequest
        {
            MessageId = messageId.ToString(),
            UserId = userId.ToString(),
        };

        var response = await Client.DeleteMessageAsync(request, GetCallOptions(cancellationToken));
        return response.Success;
    }

    // ── Reaction Operations ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> AddReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        var request = new AddReactionRequest
        {
            MessageId = messageId.ToString(),
            UserId = userId.ToString(),
            Emoji = emoji,
        };

        var response = await Client.AddReactionAsync(request, GetCallOptions(cancellationToken));
        return response.Success;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        var request = new RemoveReactionRequest
        {
            MessageId = messageId.ToString(),
            UserId = userId.ToString(),
            Emoji = emoji,
        };

        var response = await Client.RemoveReactionAsync(request, GetCallOptions(cancellationToken));
        return response.Success;
    }

    // ── Typing Indicators ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> NotifyTypingAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new TypingRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
        };

        var response = await Client.NotifyTypingAsync(request, GetCallOptions(cancellationToken));
        return response.Success;
    }

    // ── Channel Member Operations ───────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> IsChannelMemberAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = await GetChannelAsync(channelId, cancellationToken);
            return channel is not null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkChannelAsReadAsync(Guid channelId, Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new MarkAsReadRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MessageId = messageId.ToString(),
        };

        try
        {
            var response = await Client.MarkAsReadAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "MarkAsRead RPC failed for channel {ChannelId} user {UserId}", channelId, userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatUnreadCountDto>> GetUnreadCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new GetUnreadCountsRequest
        {
            UserId = userId.ToString(),
        };

        try
        {
            var response = await Client.GetUnreadCountsAsync(request, GetCallOptions(cancellationToken));
            return response.Counts.Select(e => new ChatUnreadCountDto
            {
                ChannelId = Guid.TryParse(e.ChannelId, out var cid) ? cid : Guid.Empty,
                UnreadCount = e.UnreadCount,
                MentionCount = e.MentionCount,
                IsMuted = e.IsMuted,
                IsPinned = e.IsPinned,
            }).ToList().AsReadOnly();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "GetUnreadCounts RPC failed for user {UserId}", userId);
            return Array.Empty<ChatUnreadCountDto>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatChannelMemberDto>> ListChannelMembersAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new ListChannelMembersRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
        };

        try
        {
            var response = await Client.ListChannelMembersAsync(request, GetCallOptions(cancellationToken));
            return response.Members.Select(m => new ChatChannelMemberDto
            {
                UserId = Guid.TryParse(m.UserId, out var uid) ? uid : Guid.Empty,
                DisplayName = m.DisplayName,
                Username = string.IsNullOrEmpty(m.Username) ? null : m.Username,
                Role = m.Role,
                JoinedAt = DateTime.TryParse(m.JoinedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var joinedAt) ? joinedAt : DateTime.MinValue,
                IsMuted = m.IsMuted,
                NotificationPref = m.NotificationPref,
            }).ToList().AsReadOnly();
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "ListChannelMembers RPC failed for channel {ChannelId}", channelId);
            return Array.Empty<ChatChannelMemberDto>();
        }
    }

    // ── Video Call Signaling Operations ─────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> SendCallOfferAsync(Guid callId, Guid targetUserId, string sdpOffer, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new SendCallOfferRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            TargetUserId = targetUserId.ToString(),
            SdpOffer = sdpOffer,
        };

        try
        {
            var response = await Client.SendCallOfferAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "SendCallOffer RPC failed for call {CallId}", callId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendCallAnswerAsync(Guid callId, Guid targetUserId, string sdpAnswer, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new SendCallAnswerRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            TargetUserId = targetUserId.ToString(),
            SdpAnswer = sdpAnswer,
        };

        try
        {
            var response = await Client.SendCallAnswerAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "SendCallAnswer RPC failed for call {CallId}", callId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendIceCandidateAsync(Guid callId, Guid targetUserId, string iceCandidate, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new SendIceCandidateRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            TargetUserId = targetUserId.ToString(),
            IceCandidate = iceCandidate,
        };

        try
        {
            var response = await Client.SendIceCandidateAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "SendIceCandidate RPC failed for call {CallId}", callId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendMediaStateChangeAsync(Guid callId, string mediaType, bool enabled, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new SendMediaStateChangeRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            MediaType = mediaType,
            Enabled = enabled,
        };

        try
        {
            var response = await Client.SendMediaStateChangeAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "SendMediaStateChange RPC failed for call {CallId}", callId);
            return false;
        }
    }

    // ── Video Call Lifecycle Operations ─────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> InviteToCallAsync(Guid callId, Guid targetUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new InviteToCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            TargetUserId = targetUserId.ToString(),
        };

        try
        {
            var response = await Client.InviteToCallAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "InviteToCall RPC failed for call {CallId}", callId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TransferCallHostAsync(Guid callId, Guid newHostUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var request = new TransferCallHostRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            NewHostUserId = newHostUserId.ToString(),
        };

        try
        {
            var response = await Client.TransferCallHostAsync(request, GetCallOptions(cancellationToken));
            return response.Success;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "TransferCallHost RPC failed for call {CallId}", callId);
            return false;
        }
    }

    // ── Mapping Methods ─────────────────────────────────────────────────

    private static ChatChannelDto? ToChatChannelDto(ChannelMessage? msg)
    {
        if (msg is null)
            return null;

        return new ChatChannelDto
        {
            Id = Guid.TryParse(msg.Id, out var id) ? id : Guid.Empty,
            Name = msg.Name,
            Description = string.IsNullOrEmpty(msg.Description) ? null : msg.Description,
            Type = msg.Type,
            Topic = string.IsNullOrEmpty(msg.Topic) ? null : msg.Topic,
            AvatarUrl = string.IsNullOrEmpty(msg.AvatarUrl) ? null : msg.AvatarUrl,
            IsArchived = msg.IsArchived,
            MemberCount = msg.MemberCount,
            CreatedAt = DateTime.TryParse(msg.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var createdAt) ? createdAt : DateTime.MinValue,
            LastActivityAt = string.IsNullOrEmpty(msg.LastActivityAt)
                ? null
                : DateTime.TryParse(msg.LastActivityAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var lastActivity) ? lastActivity : null,
            CreatedByUserId = Guid.TryParse(msg.CreatedByUserId, out var createdBy) ? createdBy : Guid.Empty,
        };
    }

    private static ChatMessageDto ToChatMessageDto(ChatMessageMessage? msg)
    {
        if (msg is null)
            return new ChatMessageDto { Id = Guid.Empty, Content = string.Empty, Type = string.Empty };

        return new ChatMessageDto
        {
            Id = Guid.TryParse(msg.Id, out var id) ? id : Guid.Empty,
            ChannelId = Guid.TryParse(msg.ChannelId, out var cid) ? cid : Guid.Empty,
            SenderUserId = Guid.TryParse(msg.SenderUserId, out var uid) ? uid : Guid.Empty,
            Content = msg.Content,
            Type = msg.Type,
            SentAt = DateTime.TryParse(msg.SentAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var sentAt) ? sentAt : DateTime.MinValue,
            EditedAt = string.IsNullOrEmpty(msg.EditedAt)
                ? null
                : DateTime.TryParse(msg.EditedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var editedAt) ? editedAt : null,
            IsEdited = msg.IsEdited,
            ReplyToMessageId = string.IsNullOrEmpty(msg.ReplyToMessageId)
                ? null
                : Guid.TryParse(msg.ReplyToMessageId, out var replyId) ? replyId : null,
            Attachments = msg.Attachments?.Select(ToChatAttachmentDto).ToList().AsReadOnly() ?? [],
        };
    }

    private static ChatMessageAttachmentDto ToChatAttachmentDto(AttachmentMessage att)
    {
        return new ChatMessageAttachmentDto
        {
            Id = Guid.TryParse(att.Id, out var id) ? id : Guid.Empty,
            FileName = att.FileName,
            MimeType = att.MimeType,
            FileSize = att.FileSize,
            ThumbnailUrl = string.IsNullOrEmpty(att.ThumbnailUrl) ? null : att.ThumbnailUrl,
            FileNodeId = string.IsNullOrEmpty(att.FileNodeId)
                ? null
                : Guid.TryParse(att.FileNodeId, out var fid) ? fid : null,
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_channel.IsValueCreated)
            {
                try
                { _channel.Value.Dispose(); }
                catch { /* best effort */ }
            }
        }
    }
}
