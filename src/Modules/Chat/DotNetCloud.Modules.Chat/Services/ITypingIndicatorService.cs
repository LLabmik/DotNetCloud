using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for tracking and querying typing indicators in chat channels.
/// Uses in-memory storage with time-based expiration.
/// </summary>
public interface ITypingIndicatorService
{
    /// <summary>Notifies that a user is typing in a channel. Automatically expires.</summary>
    Task NotifyTypingAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the list of users currently typing in a channel.</summary>
    Task<IReadOnlyList<TypingIndicatorDto>> GetTypingUsersAsync(Guid channelId, CancellationToken cancellationToken = default);
}
