using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.UI.Android.Services;

/// <summary>
/// HTTP client for the DotNetCloud Chat REST API.
/// </summary>
public interface IChatApiClient
{
    /// <summary>Lists channels the user belongs to.</summary>
    Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets messages in a channel.</summary>
    Task<PagedMessageResult> GetMessagesAsync(Guid channelId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    /// <summary>Sends a message to a channel.</summary>
    Task<MessageDto> SendMessageAsync(Guid channelId, SendMessageDto dto, CancellationToken cancellationToken = default);

    /// <summary>Adds a reaction to a message.</summary>
    Task AddReactionAsync(Guid messageId, string emoji, CancellationToken cancellationToken = default);

    /// <summary>Removes a reaction from a message.</summary>
    Task RemoveReactionAsync(Guid messageId, string emoji, CancellationToken cancellationToken = default);

    /// <summary>Marks a channel as read.</summary>
    Task MarkAsReadAsync(Guid channelId, Guid messageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub chat API client for initial skeleton.
/// </summary>
internal sealed class ChatApiClient : IChatApiClient
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ChannelDto>>([]);
    }

    /// <inheritdoc />
    public Task<PagedMessageResult> GetMessagesAsync(Guid channelId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PagedMessageResult { Items = [], Page = page, PageSize = pageSize, TotalItems = 0 });
    }

    /// <inheritdoc />
    public Task<MessageDto> SendMessageAsync(Guid channelId, SendMessageDto dto, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MessageDto
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderUserId = Guid.Empty,
            Content = dto.Content,
            Type = "Text",
            SentAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public Task AddReactionAsync(Guid messageId, string emoji, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveReactionAsync(Guid messageId, string emoji, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task MarkAsReadAsync(Guid channelId, Guid messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
