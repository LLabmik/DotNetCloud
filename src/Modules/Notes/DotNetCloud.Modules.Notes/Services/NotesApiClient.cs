using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Models;

namespace DotNetCloud.Modules.Notes.Services;

/// <summary>
/// HTTP implementation of <see cref="INotesApiClient"/>.
/// </summary>
public sealed class NotesApiClient : INotesApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<NoteDto>> ListNotesAsync(Guid? folderId = null, CancellationToken cancellationToken = default)
    {
        var url = "api/v1/notes";
        if (folderId.HasValue)
        {
            url += $"?folderId={folderId.Value}";
        }

        return await ReadDataAsync<IReadOnlyList<NoteDto>>(url, cancellationToken) ?? [];
    }

    public Task<NoteDto?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        return ReadDataAsync<NoteDto>($"api/v1/notes/{noteId}", cancellationToken);
    }

    public async Task<NoteDto?> CreateNoteAsync(CreateNoteDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/notes", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteDto>(response, cancellationToken);
    }

    public async Task<NoteDto?> UpdateNoteAsync(Guid noteId, UpdateNoteDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/notes/{noteId}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteDto>(response, cancellationToken);
    }

    public async Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/notes/{noteId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<NoteFolderDto>>("api/v1/notes/folders", cancellationToken) ?? [];
    }

    public async Task<string> RenderMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/notes/render", markdown, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rendered = await ReadDataAsync<string>(response, cancellationToken);
        return rendered ?? string.Empty;
    }

    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string? query, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/notes/search?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(query))
        {
            url += $"&q={Uri.EscapeDataString(query)}";
        }

        return await ReadDataAsync<IReadOnlyList<NoteDto>>(url, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<NoteShareDto>> ListSharesAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<NoteShareDto>>($"api/v1/notes/{noteId}/shares", cancellationToken) ?? [];
    }

    public async Task<NoteShareDto?> ShareNoteAsync(Guid noteId, Guid userId, NoteSharePermission permission = NoteSharePermission.ReadOnly, CancellationToken cancellationToken = default)
    {
        var body = new { UserId = userId, Permission = permission };
        var response = await _httpClient.PostAsJsonAsync($"api/v1/notes/{noteId}/shares", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteShareDto>(response, cancellationToken);
    }

    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/notes/shares/{shareId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<NoteVersionDto>> GetVersionHistoryAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        return await ReadDataAsync<IReadOnlyList<NoteVersionDto>>($"api/v1/notes/{noteId}/versions", cancellationToken) ?? [];
    }

    public async Task<NoteDto?> RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/notes/{noteId}/versions/{versionId}/restore", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteDto>(response, cancellationToken);
    }

    public async Task<NoteFolderDto?> CreateFolderAsync(CreateNoteFolderDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/notes/folders", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteFolderDto>(response, cancellationToken);
    }

    public async Task<NoteFolderDto?> UpdateFolderAsync(Guid folderId, UpdateNoteFolderDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/notes/folders/{folderId}", dto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<NoteFolderDto>(response, cancellationToken);
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/notes/folders/{folderId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadDataAsync<T>(response, cancellationToken);
    }

    private static async Task<T?> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return default;
        }

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
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
