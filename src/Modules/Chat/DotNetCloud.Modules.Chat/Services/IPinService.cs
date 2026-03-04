using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for pinning and unpinning messages in channels.
/// </summary>
public interface IPinService
{
    /// <summary>Pins a message in a channel.</summary>
    Task PinMessageAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Unpins a message from a channel.</summary>
    Task UnpinMessageAsync(Guid channelId, Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets all pinned messages in a channel.</summary>
    Task<IReadOnlyList<MessageDto>> GetPinnedMessagesAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);
}
