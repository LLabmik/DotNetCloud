using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Chat;

/// <summary>
/// <see cref="IChatRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;IChatRestClient, HttpChatRestClient&gt;()</c>
/// so it inherits the typed client lifetime.
/// </summary>
internal sealed class HttpChatRestClient : IChatRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpChatRestClient> _logger;

    /// <summary>Initializes a new <see cref="HttpChatRestClient"/>.</summary>
    public HttpChatRestClient(HttpClient http, ILogger<HttpChatRestClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelSummary>> GetChannelsAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels?userId={userId}";
        var envelope = await _http.GetFromJsonAsync<Envelope<List<ChannelSummaryDto>>>(url, JsonOpts, ct).ConfigureAwait(false);
        return (envelope?.Data ?? []).Select(ToChannelSummary).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid? beforeId = null, int pageSize = 50,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        if (beforeId.HasValue)
            _logger.LogDebug("GetMessagesAsync currently ignores beforeId; server API uses page/pageSize pagination.");

        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages?userId={userId}&page=1&pageSize={pageSize}";

        var envelope = await _http.GetFromJsonAsync<PagedEnvelope<ChatMessageDto>>(url, JsonOpts, ct).ConfigureAwait(false);
        return (envelope?.Data ?? []).Select(ToChatMessage).ToList();
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages?userId={userId}";
        using var response = await _http.PostAsJsonAsync(url, new { Content = content }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ChatMessageDto>>(JsonOpts, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Empty response from send message.");
        return envelope.Data is null
            ? throw new InvalidOperationException("Send message response did not include data.")
            : ToChatMessage(envelope.Data);
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/read?userId={userId}";
        using var response = await _http.PostAsJsonAsync(url, new { messageId }, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("MarkRead returned {StatusCode} for channel {ChannelId}.", response.StatusCode, channelId);
    }

    /// <inheritdoc />
    public async Task NotifyTypingAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/typing?userId={userId}";
        using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
        // Typing indicator is best-effort; swallow non-success silently.
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelMemberSummary>> GetChannelMembersAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/members?userId={userId}";
        var envelope = await _http.GetFromJsonAsync<Envelope<List<ChannelMemberDto>>>(url, JsonOpts, ct).ConfigureAwait(false);
        return (envelope?.Data ?? []).Select(ToMemberSummary).ToList();
    }

    /// <inheritdoc />
    public async Task LeaveChannelAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/members/{userId}";
        using var response = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendFileMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid fileId, string fileName,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var userId = AccessTokenUserIdExtractor.ExtractUserId(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages?userId={userId}";
        var body = new { Content = fileName, FileId = fileId };
        using var response = await _http.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ChatMessageDto>>(JsonOpts, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Empty response from send file message.");
        return envelope.Data is null
            ? throw new InvalidOperationException("Send file message response did not include data.")
            : ToChatMessage(envelope.Data);
    }

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    // ── DTO mappings ────────────────────────────────────────────────

    private static ChannelSummary ToChannelSummary(ChannelSummaryDto d) =>
        new(d.Id, d.Name, d.UnreadCount, d.HasMention, d.LastMessagePreview, d.LastMessageAt);

    private static ChatMessage ToChatMessage(ChatMessageDto d) =>
        new(d.Id, d.ChannelId, d.SenderName, d.Content, d.SentAt, d.IsEdited);

    private static ChannelMemberSummary ToMemberSummary(ChannelMemberDto d) =>
        new(d.UserId, d.DisplayName, d.Role, d.IsOnline);

    private sealed class Envelope<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
    }

    private sealed class PagedEnvelope<T>
    {
        public bool Success { get; init; }
        public List<T>? Data { get; init; }
    }

    private sealed class ChannelSummaryDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int UnreadCount { get; init; }
        public bool HasMention { get; init; }
        public string? LastMessagePreview { get; init; }
        public DateTimeOffset? LastMessageAt { get; init; }
    }

    private sealed class ChatMessageDto
    {
        public Guid Id { get; init; }
        public Guid ChannelId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public DateTimeOffset SentAt { get; init; }
        public bool IsEdited { get; init; }
    }

    private sealed class ChannelMemberDto
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Role { get; init; } = "Member";
        public bool IsOnline { get; init; }
    }
}
