using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Ai;

/// <summary>
/// <see cref="IAiRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;IAiRestClient, HttpAiRestClient&gt;()</c>.
/// Uses the same auth pattern as the other REST clients — sets the Bearer token
/// on DefaultRequestHeaders. The AI module is excluded from the response envelope,
/// so responses are parsed defensively (root array vs. an object with a "data" property).
/// </summary>
internal sealed class HttpAiRestClient : IAiRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<HttpAiRestClient> _logger;

    /// <summary>Initializes a new <see cref="HttpAiRestClient"/>.</summary>
    public HttpAiRestClient(HttpClient http, ILogger<HttpAiRestClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string BaseUrl(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, string accessToken, CancellationToken ct)
    {
        SetAuth(accessToken);
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("AI: {(int)StatusCode} for {Url}: {Body}", (int)response.StatusCode, url, errorBody);
        }
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T?> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("data", out var dataProp))
        {
            return dataProp.Deserialize<T>(JsonOpts);
        }

        return doc.RootElement.Deserialize<T>(JsonOpts);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/models";
        var data = await GetEnvelopeDataAsync<List<AiModelDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiConversationDto>> ListConversationsAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations";
        var data = await GetEnvelopeDataAsync<List<AiConversationDto>>(url, accessToken, ct).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<AiConversationDto?> GetConversationAsync(
        string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}";
        try
        {
            return await GetEnvelopeDataAsync<AiConversationDto>(url, accessToken, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<AiConversationDto?> CreateConversationAsync(
        string serverBaseUrl, string accessToken, string? title, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations";
        var json = JsonSerializer.Serialize(new { title }, JsonOpts);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<AiConversationDto>(response, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConversationAsync(
        string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}", ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RenameConversationAsync(
        string serverBaseUrl, string accessToken, Guid conversationId, string newTitle, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}/title";
        var json = JsonSerializer.Serialize(new { title = newTitle }, JsonOpts);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> GetOllamaHealthAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/health/ollama";
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<AiSettingsDto?> GetSettingsAsync(
        string serverBaseUrl, string accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/settings";
        return await GetEnvelopeDataAsync<AiSettingsDto>(url, accessToken, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AiStreamChunk> SendMessageStreamingAsync(
        string serverBaseUrl, string accessToken, Guid conversationId, string message,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}/messages/stream";
        var json = JsonSerializer.Serialize(new { message }, JsonOpts);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                break;
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]")
                yield break;
            var chunk = JsonSerializer.Deserialize<AiStreamChunk>(payload, JsonOpts);
            if (chunk is not null)
                yield return chunk;
        }
    }
}
