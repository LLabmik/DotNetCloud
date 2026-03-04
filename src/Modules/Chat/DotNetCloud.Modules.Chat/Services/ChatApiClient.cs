using System.Net.Http.Json;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// HTTP client for the DotNetCloud Chat REST API.
/// Used by all chat clients (Blazor WebAssembly, desktop, Android MAUI).
/// </summary>
/// <remarks>
/// Register as a typed <see cref="HttpClient"/> in DI:
/// <code>services.AddHttpClient&lt;ChatApiClient&gt;(c =&gt; c.BaseAddress = ...);</code>
/// </remarks>
public sealed class ChatApiClient
{
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatApiClient"/> class.
    /// </summary>
    public ChatApiClient(HttpClient http)
    {
        _http = http;
    }

    // ── Channels ────────────────────────────────────────────────────

    /// <summary>Creates a new channel.</summary>
    public async Task<ChannelDto?> CreateChannelAsync(CreateChannelDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/chat/channels", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<ChannelDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>Lists channels the current user belongs to.</summary>
    public async Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<ChannelDto>>>("api/v1/chat/channels", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>Gets a channel by ID.</summary>
    public async Task<ChannelDto?> GetChannelAsync(Guid channelId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<ChannelDto>>($"api/v1/chat/channels/{channelId}", ct);
        return envelope?.Data;
    }

    /// <summary>Updates a channel.</summary>
    public async Task<ChannelDto?> UpdateChannelAsync(Guid channelId, UpdateChannelDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/chat/channels/{channelId}", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<ChannelDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>Deletes a channel (soft-delete).</summary>
    public async Task<bool> DeleteChannelAsync(Guid channelId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/chat/channels/{channelId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Archives a channel.</summary>
    public async Task<bool> ArchiveChannelAsync(Guid channelId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/chat/channels/{channelId}/archive", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Gets or creates a direct message channel with another user.</summary>
    public async Task<ChannelDto?> GetOrCreateDmAsync(Guid otherUserId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/chat/channels/dm/{otherUserId}", null, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<ChannelDto>>(ct);
        return envelope?.Data;
    }

    // ── Members ─────────────────────────────────────────────────────

    /// <summary>Adds a member to a channel.</summary>
    public async Task<bool> AddMemberAsync(Guid channelId, AddChannelMemberDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/chat/channels/{channelId}/members", dto, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Removes a member from a channel.</summary>
    public async Task<bool> RemoveMemberAsync(Guid channelId, Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/chat/channels/{channelId}/members/{userId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Lists members of a channel.</summary>
    public async Task<IReadOnlyList<ChannelMemberDto>> ListMembersAsync(Guid channelId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<ChannelMemberDto>>>(
            $"api/v1/chat/channels/{channelId}/members", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>Marks a channel as read up to a specific message.</summary>
    public async Task<bool> MarkAsReadAsync(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/chat/channels/{channelId}/read",
            new { messageId }, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Gets unread counts for all channels the current user belongs to.</summary>
    public async Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<UnreadCountDto>>>("api/v1/chat/unread", ct);
        return envelope?.Data ?? [];
    }

    // ── Messages ────────────────────────────────────────────────────

    /// <summary>Sends a message to a channel.</summary>
    public async Task<MessageDto?> SendMessageAsync(Guid channelId, SendMessageDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/chat/channels/{channelId}/messages", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<MessageDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>Gets paginated messages from a channel.</summary>
    public async Task<ChatPagedResult<MessageDto>?> GetMessagesAsync(
        Guid channelId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatPagedEnvelope<MessageDto>>(
            $"api/v1/chat/channels/{channelId}/messages?page={page}&pageSize={pageSize}", ct);
        return envelope is null ? null : new ChatPagedResult<MessageDto>
        {
            Items = envelope.Data ?? [],
            Page = envelope.Pagination?.Page ?? page,
            PageSize = envelope.Pagination?.PageSize ?? pageSize,
            TotalItems = envelope.Pagination?.TotalItems ?? 0,
            TotalPages = envelope.Pagination?.TotalPages ?? 0
        };
    }

    /// <summary>Gets a single message by ID.</summary>
    public async Task<MessageDto?> GetMessageAsync(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<MessageDto>>(
            $"api/v1/chat/channels/{channelId}/messages/{messageId}", ct);
        return envelope?.Data;
    }

    /// <summary>Edits a message.</summary>
    public async Task<MessageDto?> EditMessageAsync(Guid channelId, Guid messageId, EditMessageDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/chat/channels/{channelId}/messages/{messageId}", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<MessageDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>Deletes a message (soft-delete).</summary>
    public async Task<bool> DeleteMessageAsync(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/chat/channels/{channelId}/messages/{messageId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Searches messages in a channel.</summary>
    public async Task<ChatPagedResult<MessageDto>?> SearchMessagesAsync(
        Guid channelId, string query, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatPagedEnvelope<MessageDto>>(
            $"api/v1/chat/channels/{channelId}/messages/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}", ct);
        return envelope is null ? null : new ChatPagedResult<MessageDto>
        {
            Items = envelope.Data ?? [],
            Page = envelope.Pagination?.Page ?? page,
            PageSize = envelope.Pagination?.PageSize ?? pageSize,
            TotalItems = envelope.Pagination?.TotalItems ?? 0,
            TotalPages = envelope.Pagination?.TotalPages ?? 0
        };
    }

    // ── Attachments ─────────────────────────────────────────────────

    /// <summary>Attaches a file to an existing message.</summary>
    public async Task<MessageAttachmentDto?> AddAttachmentAsync(
        Guid channelId, Guid messageId, CreateAttachmentDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/chat/channels/{channelId}/messages/{messageId}/attachments", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ChatEnvelope<MessageAttachmentDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>Lists files shared in a channel (via message attachments).</summary>
    public async Task<IReadOnlyList<MessageAttachmentDto>> GetChannelFilesAsync(Guid channelId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<MessageAttachmentDto>>>(
            $"api/v1/chat/channels/{channelId}/files", ct);
        return envelope?.Data ?? [];
    }

    // ── Reactions ────────────────────────────────────────────────────

    /// <summary>Adds a reaction to a message.</summary>
    public async Task<bool> AddReactionAsync(Guid messageId, string emoji, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/chat/messages/{messageId}/reactions",
            new { emoji }, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Removes a reaction from a message.</summary>
    public async Task<bool> RemoveReactionAsync(Guid messageId, string emoji, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(
            $"api/v1/chat/messages/{messageId}/reactions/{Uri.EscapeDataString(emoji)}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Gets all reactions for a message.</summary>
    public async Task<IReadOnlyList<MessageReactionDto>> GetReactionsAsync(Guid messageId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<MessageReactionDto>>>(
            $"api/v1/chat/messages/{messageId}/reactions", ct);
        return envelope?.Data ?? [];
    }

    // ── Pins ────────────────────────────────────────────────────────

    /// <summary>Pins a message in a channel.</summary>
    public async Task<bool> PinMessageAsync(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/chat/channels/{channelId}/pins/{messageId}", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Unpins a message from a channel.</summary>
    public async Task<bool> UnpinMessageAsync(Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/chat/channels/{channelId}/pins/{messageId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Gets pinned messages in a channel.</summary>
    public async Task<IReadOnlyList<MessageDto>> GetPinnedMessagesAsync(Guid channelId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<MessageDto>>>(
            $"api/v1/chat/channels/{channelId}/pins", ct);
        return envelope?.Data ?? [];
    }

    // ── Typing ──────────────────────────────────────────────────────

    /// <summary>Notifies that the current user is typing in a channel.</summary>
    public async Task<bool> NotifyTypingAsync(Guid channelId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/chat/channels/{channelId}/typing", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Gets users currently typing in a channel.</summary>
    public async Task<IReadOnlyList<TypingIndicatorDto>> GetTypingUsersAsync(Guid channelId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ChatEnvelope<IReadOnlyList<TypingIndicatorDto>>>(
            $"api/v1/chat/channels/{channelId}/typing", ct);
        return envelope?.Data ?? [];
    }

    // ── Response envelope types ─────────────────────────────────────

    /// <summary>Standard chat API response envelope.</summary>
    internal sealed class ChatEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

    /// <summary>Paginated chat API response envelope.</summary>
    internal sealed class ChatPagedEnvelope<T>
    {
        public bool Success { get; set; }
        public IReadOnlyList<T>? Data { get; set; }
        public PaginationInfo? Pagination { get; set; }
    }

    /// <summary>Pagination metadata from the API response.</summary>
    internal sealed class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}

/// <summary>
/// Client-side paginated result for chat API responses.
/// </summary>
public sealed class ChatPagedResult<T>
{
    /// <summary>Items on the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>Number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalItems { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; init; }
}
