using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for managing chat channels (CRUD, archive, DM).
/// </summary>
public interface IChannelService
{
    /// <summary>Creates a new channel.</summary>
    Task<ChannelDto> CreateChannelAsync(CreateChannelDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a channel by ID.</summary>
    Task<ChannelDto?> GetChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists channels the caller belongs to.</summary>
    Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates a channel's metadata.</summary>
    Task<ChannelDto> UpdateChannelAsync(Guid channelId, UpdateChannelDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a channel.</summary>
    Task DeleteChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Archives a channel.</summary>
    Task ArchiveChannelAsync(Guid channelId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets or creates a direct message channel between two users.</summary>
    Task<ChannelDto> GetOrCreateDirectMessageAsync(Guid otherUserId, CallerContext caller, CancellationToken cancellationToken = default);
}
