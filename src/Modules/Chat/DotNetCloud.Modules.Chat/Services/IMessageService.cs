using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Service for sending, editing, deleting, and querying chat messages.
/// </summary>
public interface IMessageService
{
    /// <summary>Sends a new message to a channel.</summary>
    Task<MessageDto> SendMessageAsync(Guid channelId, SendMessageDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Edits an existing message.</summary>
    Task<MessageDto> EditMessageAsync(Guid messageId, EditMessageDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a message.</summary>
    Task DeleteMessageAsync(Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets paginated messages from a channel.</summary>
    Task<PagedMessageResult> GetMessagesAsync(Guid channelId, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches messages in a channel.</summary>
    Task<PagedMessageResult> SearchMessagesAsync(Guid channelId, string query, int page, int pageSize, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single message by ID.</summary>
    Task<MessageDto?> GetMessageAsync(Guid messageId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Attaches a file to an existing message.</summary>
    Task<MessageAttachmentDto> AddAttachmentAsync(Guid channelId, Guid messageId, CreateAttachmentDto dto, CallerContext caller, CancellationToken cancellationToken = default);
}

/// <summary>
/// Paginated result of messages.
/// </summary>
public sealed record PagedMessageResult
{
    /// <summary>The messages on the current page.</summary>
    public required IReadOnlyList<MessageDto> Items { get; init; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalItems { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
}
