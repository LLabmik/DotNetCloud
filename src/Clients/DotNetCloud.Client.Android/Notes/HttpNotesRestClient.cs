using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Notes;

/// <summary>
/// <see cref="INotesRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Registered via <c>AddHttpClient&lt;INotesRestClient, HttpNotesRestClient&gt;()</c>.
/// </summary>
internal sealed class HttpNotesRestClient : INotesRestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    /// <summary>Initializes a new <see cref="HttpNotesRestClient"/>.</summary>
    public HttpNotesRestClient(HttpClient http)
    {
        _http = http;
    }

    // ── Notes ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> ListNotesAsync(
        string serverBaseUrl, string accessToken,
        Guid? folderId = null, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var query = $"?skip={skip}&take={take}";
        if (folderId.HasValue)
            query += $"&folderId={folderId.Value}";

        var result = await GetEnvelopeDataAsync<List<NoteDto>>(
            $"{Url(serverBaseUrl)}/api/v1/notes{query}", ct).ConfigureAwait(false);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<NoteDto> GetNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NoteDto>(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Note {noteId} not found.");
    }

    /// <inheritdoc />
    public async Task<NoteDto> CreateNoteAsync(
        string serverBaseUrl, string accessToken,
        CreateNoteDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<NoteDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for note creation.");
    }

    /// <inheritdoc />
    public async Task<NoteDto> UpdateNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, UpdateNoteDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PutAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<NoteDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for note update.");
    }

    /// <inheritdoc />
    public async Task DeleteNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Search ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(
        string serverBaseUrl, string accessToken,
        string query, int skip = 0, int take = 50,
        CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var q = $"?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}";
        var result = await GetEnvelopeDataAsync<List<NoteDto>>(
            $"{Url(serverBaseUrl)}/api/v1/notes/search{q}", ct).ConfigureAwait(false);
        return result ?? [];
    }

    // ── Markdown Rendering ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<NotePreviewResponse> GetNotePreviewAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NotePreviewResponse>(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}/preview", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Preview for note {noteId} not found.");
    }

    /// <inheritdoc />
    public async Task<string> RenderMarkdownAsync(
        string serverBaseUrl, string accessToken,
        string content, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/render",
            new { content }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("html", out var html))
        {
            return html.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    // ── Folders ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(
        string serverBaseUrl, string accessToken,
        Guid? parentId = null, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        var url = $"{Url(serverBaseUrl)}/api/v1/notes/folders";
        if (parentId.HasValue)
            url += $"?parentId={parentId.Value}";

        var result = await GetEnvelopeDataAsync<List<NoteFolderDto>>(url, ct).ConfigureAwait(false);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto> GetFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NoteFolderDto>(
            $"{Url(serverBaseUrl)}/api/v1/notes/folders/{folderId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Folder {folderId} not found.");
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto> CreateFolderAsync(
        string serverBaseUrl, string accessToken,
        CreateNoteFolderDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PostAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/folders", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<NoteFolderDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for folder creation.");
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto> UpdateFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, UpdateNoteFolderDto dto, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.PutAsJsonAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/folders/{folderId}", dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<NoteFolderDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned null for folder update.");
    }

    /// <inheritdoc />
    public async Task DeleteFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        using var response = await _http.DeleteAsync(
            $"{Url(serverBaseUrl)}/api/v1/notes/folders/{folderId}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void SetAuth(string accessToken) =>
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    private static string Url(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');

    private async Task<T?> GetEnvelopeDataAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<T>(response, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the response body, unwrapping the server's standard envelope
    /// (<c>{"success":true,"data":...}</c>) if present.
    /// </summary>
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
}
