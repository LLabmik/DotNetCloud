using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNetCloud.Core.DTOs.Chat;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Chat module.
/// All methods communicate with the process-isolated Chat module host over gRPC.
/// </summary>
public interface IChatApiClient
{
    // ── Channel Operations ──────────────────────────────────────────────

    /// <summary>Creates a new channel.</summary>
    Task<ChatChannelDto?> CreateChannelAsync(
        string name,
        string description,
        string type,
        string topic,
        Guid userId,
        IReadOnlyList<Guid> memberIds,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a channel by ID.</summary>
    Task<ChatChannelDto?> GetChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Lists channels the user belongs to.</summary>
    Task<IReadOnlyList<ChatChannelDto>> ListChannelsAsync(Guid userId, CancellationToken cancellationToken = default);

    // ── Message Operations ──────────────────────────────────────────────

    /// <summary>Sends a message to a channel.</summary>
    Task<ChatMessageDto?> SendMessageAsync(
        Guid channelId,
        Guid userId,
        string content,
        Guid? replyToMessageId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets paginated messages from a channel.</summary>
    Task<(IReadOnlyList<ChatMessageDto> Messages, int TotalCount)> GetMessagesAsync(
        Guid channelId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Edits an existing message.</summary>
    Task<ChatMessageDto?> EditMessageAsync(
        Guid messageId,
        Guid userId,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a message (soft-delete).</summary>
    Task<bool> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    // ── Reaction Operations ─────────────────────────────────────────────

    /// <summary>Adds a reaction to a message.</summary>
    Task<bool> AddReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default);

    /// <summary>Removes a reaction from a message.</summary>
    Task<bool> RemoveReactionAsync(Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default);

    // ── Typing Indicators ───────────────────────────────────────────────

    /// <summary>Notifies that a user is typing in a channel.</summary>
    Task<bool> NotifyTypingAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);

    // ── Channel Member Operations ───────────────────────────────────────

    /// <summary>Checks whether a user is a member of a channel.</summary>
    Task<bool> IsChannelMemberAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a channel as read up to a specific message.</summary>
    Task<bool> MarkChannelAsReadAsync(Guid channelId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets unread message counts for a user.</summary>
    Task<IReadOnlyList<ChatUnreadCountDto>> GetUnreadCountsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lists members of a channel.</summary>
    Task<IReadOnlyList<ChatChannelMemberDto>> ListChannelMembersAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);

    // ── Video Call Signaling Operations ─────────────────────────────────

    /// <summary>Sends an SDP offer to a target participant.</summary>
    Task<bool> SendCallOfferAsync(Guid callId, Guid targetUserId, string sdpOffer, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sends an SDP answer to a target participant.</summary>
    Task<bool> SendCallAnswerAsync(Guid callId, Guid targetUserId, string sdpAnswer, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sends an ICE candidate to a target participant.</summary>
    Task<bool> SendIceCandidateAsync(Guid callId, Guid targetUserId, string iceCandidate, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sends a media state change notification.</summary>
    Task<bool> SendMediaStateChangeAsync(Guid callId, string mediaType, bool enabled, Guid userId, CancellationToken cancellationToken = default);

    // ── Video Call Lifecycle Operations ─────────────────────────────────

    /// <summary>Invites a user to join an active call.</summary>
    Task<bool> InviteToCallAsync(Guid callId, Guid targetUserId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Transfers the host role of an active call to another participant.</summary>
    Task<bool> TransferCallHostAsync(Guid callId, Guid newHostUserId, Guid userId, CancellationToken cancellationToken = default);

    // ── Push Notification Operations ───────────────────────────────────

    /// <summary>Sends a push notification to a user's registered devices via the Chat module.</summary>
    Task SendPushNotificationAsync(
        Guid userId,
        string title,
        string body,
        string category,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
