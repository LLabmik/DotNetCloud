using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/chat/channels";
        var result = await _http.GetFromJsonAsync<List<ChannelSummaryDto>>(url, JsonOpts, ct).ConfigureAwait(false);
        return (result ?? []).Select(ToChannelSummary).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid? beforeId = null, int pageSize = 50,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/chat/channels/{channelId}/messages?pageSize={pageSize}";
        if (beforeId.HasValue) url += $"&beforeId={beforeId.Value}";

        var result = await _http.GetFromJsonAsync<List<ChatMessageDto>>(url, JsonOpts, ct).ConfigureAwait(false);
        return (result ?? []).Select(ToChatMessage).ToList();
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/chat/channels/{channelId}/messages";
        using var response = await _http.PostAsJsonAsync(url, new { Content = content }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ChatMessageDto>(JsonOpts, ct).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Empty response from send message.");
        return ToChatMessage(dto);
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/chat/channels/{channelId}/read";
        using var response = await _http.PostAsJsonAsync(url, new { MessageId = messageId }, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("MarkRead returned {StatusCode} for channel {ChannelId}.", response.StatusCode, channelId);
    }

    /// <inheritdoc />
    public async Task NotifyTypingAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/chat/channels/{channelId}/typing";
        using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
        // Typing indicator is best-effort; swallow non-success silently.
    }

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    // ── DTO mappings ────────────────────────────────────────────────

    private static ChannelSummary ToChannelSummary(ChannelSummaryDto d) =>
        new(d.Id, d.Name, d.UnreadCount, d.HasMention, d.LastMessagePreview, d.LastMessageAt);

    private static ChatMessage ToChatMessage(ChatMessageDto d) =>
        new(d.Id, d.ChannelId, d.SenderName, d.Content, d.SentAt, d.IsEdited);

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
}
