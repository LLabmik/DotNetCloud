using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Modules.Bookmarks.Models;

namespace DotNetCloud.Modules.Bookmarks.Services;

/// <summary>
/// HTTP implementation of <see cref="IBookmarksApiClient"/>.
/// </summary>
public sealed class BookmarksApiClient : IBookmarksApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BookmarksApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Bookmarks ────────────────────────────────────────────

    public async Task<IReadOnlyList<BookmarkItem>> ListAsync(Guid? folderId = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"api/v1/bookmarks?skip={skip}&take={take}";
        if (folderId.HasValue) url += $"&folderId={folderId.Value}";
        return await ReadDataAsync<IReadOnlyList<BookmarkItem>>(url, ct) ?? [];
    }

    public Task<BookmarkItem?> GetAsync(Guid id, CancellationToken ct = default)
        => ReadDataAsync<BookmarkItem>($"api/v1/bookmarks/{id}", ct);

    public async Task<BookmarkItem?> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/bookmarks", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkItem>(response, ct);
    }

    public async Task<BookmarkItem?> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/bookmarks/{id}", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkItem>(response, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/bookmarks/{id}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<IReadOnlyList<BookmarkItem>> SearchAsync(string query, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(query);
        return await ReadDataAsync<IReadOnlyList<BookmarkItem>>($"api/v1/bookmarks/search?q={q}&skip={skip}&take={take}", ct) ?? [];
    }

    // ── Folders ──────────────────────────────────────────────

    public async Task<IReadOnlyList<BookmarkFolder>> ListFoldersAsync(Guid? parentId = null, CancellationToken ct = default)
    {
        var url = "api/v1/bookmarks/folders";
        if (parentId.HasValue) url += $"?parentId={parentId.Value}";
        return await ReadDataAsync<IReadOnlyList<BookmarkFolder>>(url, ct) ?? [];
    }

    public Task<BookmarkFolder?> GetFolderAsync(Guid id, CancellationToken ct = default)
        => ReadDataAsync<BookmarkFolder>($"api/v1/bookmarks/folders/{id}", ct);

    public async Task<BookmarkFolder?> CreateFolderAsync(CreateBookmarkFolderRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/bookmarks/folders", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkFolder>(response, ct);
    }

    public async Task<BookmarkFolder?> UpdateFolderAsync(Guid id, UpdateBookmarkFolderRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/bookmarks/folders/{id}", request, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkFolder>(response, ct);
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/bookmarks/folders/{id}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Import / Export ──────────────────────────────────────

    public async Task<BookmarkImportResult?> ImportAsync(Stream fileStream, string fileName, Guid? folderId = null, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", fileName);

        var url = "api/v1/bookmarks/import";
        if (folderId.HasValue) url += $"?folderId={folderId.Value}";

        var response = await _httpClient.PostAsync(url, content, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkImportResult>(response, ct);
    }

    public async Task<byte[]> ExportAsync(Guid? folderId = null, CancellationToken ct = default)
    {
        var url = "api/v1/bookmarks/export";
        if (folderId.HasValue) url += $"?folderId={folderId.Value}";
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // ── Previews ─────────────────────────────────────────────

    public async Task<BookmarkPreview?> FetchPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/bookmarks/{bookmarkId}/preview/fetch", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<BookmarkPreview>(response, ct);
    }

    public Task<BookmarkPreview?> GetPreviewAsync(Guid bookmarkId, CancellationToken ct = default)
        => ReadDataAsync<BookmarkPreview>($"api/v1/bookmarks/{bookmarkId}/preview", ct);

    // ── Helpers ──────────────────────────────────────────────

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<T>(response, ct);
    }

    private static async Task<T?> ReadDataFromResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
            return default;

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
            dataElement = nestedData;

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        string message;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "error", out var error) &&
                TryGetPropertyIgnoreCase(error, "message", out var msg))
                message = msg.GetString() ?? $"Request failed ({(int)response.StatusCode}).";
            else
                message = $"Request failed ({(int)response.StatusCode}).";
        }
        catch
        {
            message = $"Request failed ({(int)response.StatusCode}).";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
