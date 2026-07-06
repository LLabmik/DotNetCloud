using Android.Util;
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
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels";
        Log.Info("DotNetCloud", $"GetChannelsAsync CALLING {url}");
        Log.Info("DotNetCloud", $"GetChannelsAsync Auth header: Bearer {accessToken[..Math.Min(20, accessToken.Length)]}... (len={accessToken.Length})");
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            Log.Info("DotNetCloud", $"GetChannelsAsync RESPONSE: Status={(int)response.StatusCode}, Content-Length={response.Content.Headers.ContentLength?.ToString() ?? "null"}, Headers={string.Join("; ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                Log.Error("DotNetCloud", $"GetChannelsAsync HTTP {(int)response.StatusCode} from {url}. Body='{body}' (len={body.Length})");
                response.EnsureSuccessStatusCode(); // throw after logging body
            }
            var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<ChannelSummaryDto>>>(JsonOpts, ct).ConfigureAwait(false);
            Log.Info("DotNetCloud", $"GetChannelsAsync SUCCEEDED from {url}");
            return (envelope?.Data ?? []).Select(ToChannelSummary).ToList();
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"GetChannelsAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid? beforeId = null, int pageSize = 50,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        if (beforeId.HasValue)
            _logger.LogDebug("GetMessagesAsync currently ignores beforeId; server API uses page/pageSize pagination.");

        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages?page=1&pageSize={pageSize}";
        Log.Info("DotNetCloud", $"GetMessagesAsync CALLING {url}");

        try
        {
            var envelope = await _http.GetFromJsonAsync<PagedEnvelope<ChatMessageDto>>(url, JsonOpts, ct).ConfigureAwait(false);
            var msgs = (envelope?.Data ?? []).Select(ToChatMessage).ToList();
            foreach (var m in msgs.Take(5))
                Log.Info("DotNetCloud", $"GetMessagesAsync msg: senderUserId={m.SenderUserId}, senderName='{m.SenderName}', content='{m.Content[..Math.Min(20, m.Content.Length)]}'");
            Log.Info("DotNetCloud", $"GetMessagesAsync SUCCEEDED from {url} ({msgs.Count} messages)");
            return msgs;
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"GetMessagesAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendMessageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages";
        using var response = await _http.PostAsJsonAsync(url, new { Content = content }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ChatMessageDto>>(JsonOpts, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Empty response from send message.");
        return envelope.Data is null
            ? throw new InvalidOperationException("Send message response did not include data.")
            : ToChatMessage(envelope.Data);
    }

    /// <inheritdoc />
    public async Task<ChatMessage> SendMessageWithAttachmentsAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, string content,
        IReadOnlyList<ChatAttachment>? attachments,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages";

        var body = new
        {
            Content = content,
            Attachments = attachments?.Select(a => new
            {
                fileName = a.FileName,
                mimeType = a.MimeType,
                fileSize = a.FileSize,
                thumbnailUrl = a.ThumbnailUrl
            }).ToList()
        };

        using var response = await _http.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<ChatMessageDto>>(JsonOpts, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Empty response from send message with attachments.");
        return envelope.Data is null
            ? throw new InvalidOperationException("Send message response did not include data.")
            : ToChatMessage(envelope.Data);
    }

    /// <inheritdoc />
    public async Task<ChatImageUploadResult> UploadImageAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/upload-image";

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct).ConfigureAwait(false);
        var data = ms.ToArray();

        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Headers.Add("X-File-Name", fileName);

        using var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<UploadImageResponseDto>>(JsonOpts, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Empty response from upload image.");
        return envelope.Data is null
            ? throw new InvalidOperationException("Upload image response did not include data.")
            : new ChatImageUploadResult(envelope.Data.Url, envelope.Data.FileName, envelope.Data.MimeType, envelope.Data.FileSize);
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, Guid messageId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/read";
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
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/typing";
        using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
        // Typing indicator is best-effort; swallow non-success silently.
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelMemberSummary>> GetChannelMembersAsync(
        string serverBaseUrl, string accessToken,
        Guid channelId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/members";
        Log.Info("DotNetCloud", $"GetChannelMembersAsync CALLING {url}");
        try
        {
            var envelope = await _http.GetFromJsonAsync<Envelope<List<ChannelMemberDto>>>(url, JsonOpts, ct).ConfigureAwait(false);
            var members = (envelope?.Data ?? []).Select(ToMemberSummary).ToList();
            foreach (var m in members.Take(5))
                Log.Info("DotNetCloud", $"GetChannelMembersAsync member: userId={m.UserId}, displayName='{m.DisplayName}'");
            Log.Info("DotNetCloud", $"GetChannelMembersAsync SUCCEEDED ({members.Count} members)");
            return members;
        }
        catch (Exception ex)
        {
            Log.Error("DotNetCloud", $"GetChannelMembersAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
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
        var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/messages";
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
        new(d.Id, d.Name, d.UnreadCount, d.HasMention, d.LastMessagePreview,
            d.LastMessageAt ?? (d.LastActivityAt.HasValue ? new DateTimeOffset(d.LastActivityAt.Value, TimeSpan.Zero) : null));

    private static ChatMessage ToChatMessage(ChatMessageDto d) =>
        new(d.Id, d.ChannelId, d.SenderUserId,
            string.IsNullOrWhiteSpace(d.SenderName) ? string.Empty : d.SenderName,
            d.Content, d.SentAt, d.IsEdited,
            d.Attachments?.Select(a => new ChatAttachment(a.Id, a.FileName, a.MimeType, a.FileSize, a.ThumbnailUrl)).ToList());

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
        public string? Description { get; init; }
        public string? Type { get; init; }
        public int MemberCount { get; init; }
        public int UnreadCount { get; init; }
        public bool HasMention { get; init; }
        public string? LastMessagePreview { get; init; }
        public DateTimeOffset? LastMessageAt { get; init; }
        public DateTime? LastActivityAt { get; init; }
    }

    private sealed class ChatMessageDto
    {
        public Guid Id { get; init; }
        public Guid ChannelId { get; init; }
        public Guid SenderUserId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public DateTimeOffset SentAt { get; init; }
        public bool IsEdited { get; init; }
        public List<ChatMessageAttachmentDto>? Attachments { get; init; }
    }

    private sealed class ChatMessageAttachmentDto
    {
        public Guid Id { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string MimeType { get; init; } = string.Empty;
        public long FileSize { get; init; }
        public string? ThumbnailUrl { get; init; }
    }

    private sealed class UploadImageResponseDto
    {
        public string Url { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string MimeType { get; init; } = string.Empty;
        public long FileSize { get; init; }
    }

    private sealed class ChannelMemberDto
    {
        public Guid UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Role { get; init; } = "Member";
        public bool IsOnline { get; init; }
    }
}
