# Android Notes Tab — Implementation Plan

> **Branch:** `feature/android-notes-tab`
> **Target:** Add a full Notes tab to the DotNetCloud Android MAUI app, matching the Blazor Notes module functionality adapted for mobile.
> **Status:** Planning — ready for implementation

---

## Architecture Overview

The Notes tab follows the exact same MVVM + REST pattern as the existing Calendar, Files, Music, and Chat tabs:

```
NotesPage.xaml(.cs)              ← MAUI ContentPage (the tab)
    ↓ binds to
NotesViewModel.cs                ← CommunityToolkit.Mvvm ObservableObject
    ↓ calls
INotesRestClient                 ← interface (per-call credential pattern)
    ↓ implemented by
HttpNotesRestClient.cs           ← HttpClient + envelope unwrapping
    ↓ talks to
Server: /api/v1/notes/*          ← existing REST API (fully implemented server-side)
```

**Shared DTOs** from `DotNetCloud.Core/DTOs/NoteDtos.cs` are reused directly — no local DTO duplication.

### Navigation

```
TabBar
  NotesPage (tab) ──GoToAsync──→ NoteEditPage (create new, no NoteId)
                   ──GoToAsync──→ NoteEditPage (edit existing, NoteId passed via query)
```

A single `NoteEditPage` handles both create and edit modes (simpler than Calendar's `EventDetailPage` + `EventEditPage` split because notes don't have complex date/time/recurrence pickers).

---

## Reference Files (study these before implementing)

| File                                                                              | What to learn                                |
| --------------------------------------------------------------------------------- | -------------------------------------------- |
| `src/Clients/DotNetCloud.Client.Android/Calendar/ICalendarRestClient.cs`          | REST interface pattern                       |
| `src/Clients/DotNetCloud.Client.Android/Calendar/HttpCalendarRestClient.cs`       | HTTP implementation with envelope unwrapping |
| `src/Clients/DotNetCloud.Client.Android/ViewModels/CalendarViewModel.cs`          | ViewModel pattern with `GetCredentialsAsync` |
| `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml`                  | Tab page XAML layout                         |
| `src/Clients/DotNetCloud.Client.Android/Views/CalendarPage.xaml.cs`               | Tab page code-behind pattern                 |
| `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`                            | Tab registration in Shell                    |
| `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs`                         | Route registration                           |
| `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`                           | DI registration for clients, VMs, pages      |
| `src/Core/DotNetCloud.Core/DTOs/NoteDtos.cs`                                      | All Note DTOs (reuse, don't duplicate)       |
| `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Controllers/NotesController.cs` | Server REST API reference                    |

---

## Server REST API (already implemented — just need to call it)

**Base URL:** `{serverBaseUrl}/api/v1/notes`

**Response envelope:** All responses use `{ "success": bool, "data": ... }` on success, or `{ "success": false, "error": { "code": "...", "message": "..." } }` on error.

### Note CRUD

| Method   | URL                                   | Request Body    | Response `data`     |
| -------- | ------------------------------------- | --------------- | ------------------- |
| `GET`    | `/api/v1/notes?folderId=&skip=&take=` | —               | `NoteDto[]`         |
| `GET`    | `/api/v1/notes/{noteId}`              | —               | `NoteDto`           |
| `POST`   | `/api/v1/notes`                       | `CreateNoteDto` | `NoteDto`           |
| `PUT`    | `/api/v1/notes/{noteId}`              | `UpdateNoteDto` | `NoteDto`           |
| `DELETE` | `/api/v1/notes/{noteId}`              | —               | `{ deleted: true }` |

### Search

| Method | URL                                   | Request Body | Response `data` |
| ------ | ------------------------------------- | ------------ | --------------- |
| `GET`  | `/api/v1/notes/search?q=&skip=&take=` | —            | `NoteDto[]`     |

### Markdown Rendering

| Method | URL                              | Request Body                 | Response `data`                                    |
| ------ | -------------------------------- | ---------------------------- | -------------------------------------------------- |
| `GET`  | `/api/v1/notes/{noteId}/preview` | —                            | `{ noteId, title, renderedHtml, format, version }` |
| `POST` | `/api/v1/notes/render`           | `{ content: "markdown..." }` | `{ html: "..." }`                                  |

### Folders

| Method   | URL                                | Request Body          | Response `data`     |
| -------- | ---------------------------------- | --------------------- | ------------------- |
| `GET`    | `/api/v1/notes/folders?parentId=`  | —                     | `NoteFolderDto[]`   |
| `GET`    | `/api/v1/notes/folders/{folderId}` | —                     | `NoteFolderDto`     |
| `POST`   | `/api/v1/notes/folders`            | `CreateNoteFolderDto` | `NoteFolderDto`     |
| `PUT`    | `/api/v1/notes/folders/{folderId}` | `UpdateNoteFolderDto` | `NoteFolderDto`     |
| `DELETE` | `/api/v1/notes/folders/{folderId}` | —                     | `{ deleted: true }` |

---

## v1 Scope

### ✅ Included

- List notes with folder filter, search, and pull-to-refresh
- View note detail (rendered markdown via `/preview` endpoint)
- Create note (title + markdown content + optional folder)
- Edit note (partial update with optimistic concurrency via `ExpectedVersion`)
- Delete note (soft delete with confirmation dialog)
- Pin/unpin toggle and favorite/unfavorite toggle
- Folder management: list, create, rename, delete folders
- Markdown live preview while editing (via `/api/v1/notes/render`)
- **Markdown formatting toolbar** — inserts syntax at cursor position
- **Smart continuation on Enter** — auto-inserts list/blockquote prefixes on new lines

### ❌ Deferred to v2

- Version history (list + restore versions)
- Note sharing (list shares, share, revoke)
- Note links (cross-module links to files, events, contacts)
- Tag editing UI (tags are displayed but not editable in v1)
- Multi-select / batch operations

---

## Files to Create (10 new files)

| #   | File                                                                     | Purpose                 |
| --- | ------------------------------------------------------------------------ | ----------------------- |
| 1   | `src/Clients/DotNetCloud.Client.Android/Notes/INotesRestClient.cs`       | REST interface          |
| 2   | `src/Clients/DotNetCloud.Client.Android/Notes/HttpNotesRestClient.cs`    | HTTP implementation     |
| 3   | `src/Clients/DotNetCloud.Client.Android/ViewModels/NotesViewModel.cs`    | Main tab ViewModel      |
| 4   | `src/Clients/DotNetCloud.Client.Android/ViewModels/NoteEditViewModel.cs` | Create/edit ViewModel   |
| 5   | `src/Clients/DotNetCloud.Client.Android/Views/NotesPage.xaml`            | Main tab UI             |
| 6   | `src/Clients/DotNetCloud.Client.Android/Views/NotesPage.xaml.cs`         | Main tab code-behind    |
| 7   | `src/Clients/DotNetCloud.Client.Android/Views/NoteEditPage.xaml`         | Create/edit form UI     |
| 8   | `src/Clients/DotNetCloud.Client.Android/Views/NoteEditPage.xaml.cs`      | Create/edit code-behind |
| 9   | `src/Clients/DotNetCloud.Client.Android/Resources/Images/notes_icon.svg` | Tab bar icon            |
| 10  | `tests/DotNetCloud.Client.Android.Tests/Notes/`                          | Unit tests              |

## Files to Modify (3 existing files)

| #   | File                                                      | Change                                              |
| --- | --------------------------------------------------------- | --------------------------------------------------- |
| 11  | `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`    | Add Notes `<ShellContent>` inside `<TabBar>`        |
| 12  | `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs` | Register `NoteEdit` route                           |
| 13  | `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`   | DI registrations for REST client, ViewModels, Pages |

---

## Step 1: REST Client Interface

**File:** `src/Clients/DotNetCloud.Client.Android/Notes/INotesRestClient.cs`

**Namespace:** `DotNetCloud.Client.Android.Notes`

Follow the exact same pattern as `ICalendarRestClient` — every method takes `(string serverBaseUrl, string accessToken, ...)` as the first two parameters.

```csharp
namespace DotNetCloud.Client.Android.Notes;

using DotNetCloud.Core.DTOs;

/// <summary>REST client for the Notes module API.</summary>
public interface INotesRestClient
{
    // ── Notes ──────────────────────────────────────────────────────

    /// <summary>Lists notes for the authenticated user with optional folder filter.</summary>
    Task<IReadOnlyList<NoteDto>> ListNotesAsync(
        string serverBaseUrl, string accessToken,
        Guid? folderId = null, int skip = 0, int take = 50,
        CancellationToken ct = default);

    /// <summary>Gets a note by ID.</summary>
    Task<NoteDto> GetNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default);

    /// <summary>Creates a new note.</summary>
    Task<NoteDto> CreateNoteAsync(
        string serverBaseUrl, string accessToken,
        CreateNoteDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing note (partial update with optimistic concurrency).</summary>
    Task<NoteDto> UpdateNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, UpdateNoteDto dto, CancellationToken ct = default);

    /// <summary>Soft-deletes a note.</summary>
    Task DeleteNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default);

    // ── Search ─────────────────────────────────────────────────────

    /// <summary>Searches notes by title, content, and tags.</summary>
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync(
        string serverBaseUrl, string accessToken,
        string query, int skip = 0, int take = 50,
        CancellationToken ct = default);

    // ── Markdown Rendering ─────────────────────────────────────────

    /// <summary>Gets the rendered HTML preview of a saved note.</summary>
    Task<NotePreviewResponse> GetNotePreviewAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default);

    /// <summary>Renders raw markdown to HTML (live preview, no save).</summary>
    Task<string> RenderMarkdownAsync(
        string serverBaseUrl, string accessToken,
        string content, CancellationToken ct = default);

    // ── Folders ────────────────────────────────────────────────────

    /// <summary>Lists folders for the authenticated user.</summary>
    Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(
        string serverBaseUrl, string accessToken,
        Guid? parentId = null, CancellationToken ct = default);

    /// <summary>Gets a folder by ID.</summary>
    Task<NoteFolderDto> GetFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, CancellationToken ct = default);

    /// <summary>Creates a new folder.</summary>
    Task<NoteFolderDto> CreateFolderAsync(
        string serverBaseUrl, string accessToken,
        CreateNoteFolderDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing folder.</summary>
    Task<NoteFolderDto> UpdateFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, UpdateNoteFolderDto dto, CancellationToken ct = default);

    /// <summary>Soft-deletes a folder (notes become un-filed).</summary>
    Task DeleteFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, CancellationToken ct = default);
}

/// <summary>
/// Response DTO for the note preview endpoint.
/// Defined here because the server returns this shape but it's not in the shared DTOs file.
/// </summary>
public sealed class NotePreviewResponse
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RenderedHtml { get; set; } = string.Empty;
    public NoteContentFormat Format { get; set; }
    public int Version { get; set; }
}
```

---

## Step 2: REST Client Implementation

**File:** `src/Clients/DotNetCloud.Client.Android/Notes/HttpNotesRestClient.cs`

**Namespace:** `DotNetCloud.Client.Android.Notes`

**IMPORTANT:** Copy the implementation pattern EXACTLY from `HttpCalendarRestClient.cs`. The key patterns are:

- `SetAuth(accessToken)` sets `_http.DefaultRequestHeaders.Authorization` before each call
- `GetEnvelopeDataAsync<T>(url)` does GET + envelope unwrap
- `ReadEnvelopeDataAsync<T>(response)` parses `{ "success": true, "data": ... }`
- `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true` and `JsonStringEnumConverter`
- URL construction: `$"{serverBaseUrl.TrimEnd('/')}/api/v1/notes/..."`

```csharp
namespace DotNetCloud.Client.Android.Notes;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Core.DTOs;

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

    public HttpNotesRestClient(HttpClient http)
    {
        _http = http;
    }

    // ── Notes ──────────────────────────────────────────────────────

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

    public async Task<NoteDto> GetNoteAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NoteDto>(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Note {noteId} not found.");
    }

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

    public async Task<NotePreviewResponse> GetNotePreviewAsync(
        string serverBaseUrl, string accessToken,
        Guid noteId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NotePreviewResponse>(
            $"{Url(serverBaseUrl)}/api/v1/notes/{noteId}/preview", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Preview for note {noteId} not found.");
    }

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

    public async Task<NoteFolderDto> GetFolderAsync(
        string serverBaseUrl, string accessToken,
        Guid folderId, CancellationToken ct = default)
    {
        SetAuth(accessToken);
        return await GetEnvelopeDataAsync<NoteFolderDto>(
            $"{Url(serverBaseUrl)}/api/v1/notes/folders/{folderId}", ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Folder {folderId} not found.");
    }

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
```

---

## Step 3: Main Tab ViewModel

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/NotesViewModel.cs`

**Namespace:** `DotNetCloud.Client.Android.ViewModels`

**Pattern:** Copy the structure from `CalendarViewModel.cs`. Key patterns:

- `ObservableObject` base class from CommunityToolkit.Mvvm
- Constructor DI for REST client + server store + token store + logger
- `GetCredentialsAsync(ct)` helper returning `(string ServerUrl, string Token)`
- `[RelayCommand]` on async methods for commands
- `[ObservableProperty]` for bound properties
- `IsActive` internal bool for preventing errors after navigating away

```csharp
namespace DotNetCloud.Client.Android.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

/// <summary>Main ViewModel for the Notes tab.</summary>
public sealed partial class NotesViewModel : ObservableObject
{
    private readonly INotesRestClient _notesApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<NotesViewModel> _logger;

    public NotesViewModel(
        INotesRestClient notesApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<NotesViewModel> logger)
    {
        _notesApi = notesApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── View State ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private Guid? _selectedFolderId;

    [ObservableProperty]
    private NoteDto? _selectedNote;

    [ObservableProperty]
    private string _previewHtml = string.Empty;

    [ObservableProperty]
    private bool _isPreviewVisible;

    /// <summary>Whether the page is currently visible.</summary>
    internal bool IsActive { get; set; }

    // ── Data Collections ───────────────────────────────────────────

    /// <summary>All notes for the current filter.</summary>
    public ObservableCollection<NoteDto> Notes { get; } = [];

    /// <summary>User's note folders.</summary>
    public ObservableCollection<NoteFolderDto> Folders { get; } = [];

    // ── Computed ───────────────────────────────────────────────────

    /// <summary>Currently selected folder name for display, or "All Notes".</summary>
    public string SelectedFolderName =>
        Folders.FirstOrDefault(f => f.Id == SelectedFolderId)?.Name ?? "All Notes";

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadNotesAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);

            IReadOnlyList<NoteDto> notes;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                notes = await _notesApi.SearchNotesAsync(serverUrl, token, SearchQuery, ct: ct);
            }
            else
            {
                notes = await _notesApi.ListNotesAsync(serverUrl, token, SelectedFolderId, ct: ct);
            }

            Notes.Clear();
            foreach (var n in notes)
                Notes.Add(n);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notes.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var folders = await _notesApi.ListFoldersAsync(serverUrl, token, ct: ct);

            Folders.Clear();
            foreach (var f in folders)
                Folders.Add(f);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load folders.");
        }
    }

    /// <summary>Navigates to NoteEditPage to create a new note.</summary>
    [RelayCommand]
    private async Task CreateNoteAsync()
    {
        await Shell.Current.GoToAsync("NoteEdit");
    }

    /// <summary>Navigates to NoteEditPage to edit an existing note.</summary>
    [RelayCommand]
    private async Task EditNoteAsync(NoteDto note)
    {
        await Shell.Current.GoToAsync($"NoteEdit?NoteId={note.Id}");
    }

    /// <summary>Soft-deletes a note after confirmation.</summary>
    [RelayCommand]
    private async Task DeleteNoteAsync(NoteDto note)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Note", $"Delete \"{note.Title}\"?", "Delete", "Cancel");

        if (!confirm) return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.DeleteNoteAsync(serverUrl, token, note.Id);
            Notes.Remove(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete note.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    [RelayCommand]
    private async Task TogglePinAsync(NoteDto note)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var updated = await _notesApi.UpdateNoteAsync(serverUrl, token, note.Id,
                new UpdateNoteDto { IsPinned = !note.IsPinned, ExpectedVersion = note.Version },
                CancellationToken.None);

            var index = Notes.IndexOf(note);
            if (index >= 0)
                Notes[index] = updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pin.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(NoteDto note)
    {
        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var updated = await _notesApi.UpdateNoteAsync(serverUrl, token, note.Id,
                new UpdateNoteDto { IsFavorite = !note.IsFavorite, ExpectedVersion = note.Version },
                CancellationToken.None);

            var index = Notes.IndexOf(note);
            if (index >= 0)
                Notes[index] = updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favorite.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Selects a note and loads its rendered HTML preview.</summary>
    [RelayCommand]
    private async Task SelectNoteAsync(NoteDto? note)
    {
        if (note is null) return;
        SelectedNote = note;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            var preview = await _notesApi.GetNotePreviewAsync(serverUrl, token, note.Id);
            PreviewHtml = preview.RenderedHtml;
            IsPreviewVisible = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load note preview.");
            PreviewHtml = $"<p style='color:red'>Failed to load preview: {ex.Message}</p>";
            IsPreviewVisible = true;
        }
    }

    /// <summary>Closes the preview panel and returns to the note list.</summary>
    [RelayCommand]
    private void ClosePreview()
    {
        SelectedNote = null;
        PreviewHtml = string.Empty;
        IsPreviewVisible = false;
    }

    /// <summary>Filters notes by the given folder (null = all notes).</summary>
    [RelayCommand]
    private async Task SelectFolderAsync(Guid? folderId)
    {
        SelectedFolderId = folderId;
        SearchQuery = string.Empty;
        await LoadNotesCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(SelectedFolderName));
    }

    /// <summary>Executes search (triggers on SearchBar.SearchCommand).</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        // When searching, clear folder filter
        SelectedFolderId = null;
        await LoadNotesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        string? name = await Shell.Current.DisplayPromptAsync("New Folder", "Folder name:");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.CreateFolderAsync(serverUrl, token,
                new CreateNoteFolderDto { Name = name.Trim() });
            await LoadFoldersCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    [RelayCommand]
    private async Task RenameFolderAsync(NoteFolderDto folder)
    {
        string? name = await Shell.Current.DisplayPromptAsync(
            "Rename Folder", "New name:", initialValue: folder.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.UpdateFolderAsync(serverUrl, token, folder.Id,
                new UpdateNoteFolderDto { Name = name.Trim() });
            await LoadFoldersCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    [RelayCommand]
    private async Task DeleteFolderAsync(NoteFolderDto folder)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Folder",
            $"Delete \"{folder.Name}\"? Notes in this folder will become un-filed.",
            "Delete", "Cancel");

        if (!confirm) return;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(CancellationToken.None);
            await _notesApi.DeleteFolderAsync(serverUrl, token, folder.Id);
            Folders.Remove(folder);

            if (SelectedFolderId == folder.Id)
                await SelectFolderCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder.");
            if (IsActive)
                ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
    }

    /// <summary>Refreshes both notes and folders (for pull-to-refresh).</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        await LoadFoldersCommand.ExecuteAsync(ct);
        await LoadNotesCommand.ExecuteAsync(ct);
    }

    // ── Private Helpers ────────────────────────────────────────────

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }
}
```

---

## Step 4: Create/Edit ViewModel

**File:** `src/Clients/DotNetCloud.Client.Android/ViewModels/NoteEditViewModel.cs`

**Namespace:** `DotNetCloud.Client.Android.ViewModels`

Handles both create and edit modes. Uses `[QueryProperty]` to receive the optional `NoteId` from Shell navigation.

```csharp
namespace DotNetCloud.Client.Android.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

/// <summary>ViewModel for creating and editing a note.</summary>
[QueryProperty(nameof(NoteId), "NoteId")]
public sealed partial class NoteEditViewModel : ObservableObject
{
    private readonly INotesRestClient _notesApi;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<NoteEditViewModel> _logger;

    private int _currentVersion;    // For optimistic concurrency on update

    public NoteEditViewModel(
        INotesRestClient notesApi,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<NoteEditViewModel> logger)
    {
        _notesApi = notesApi;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    // ── Query Properties ───────────────────────────────────────────

    /// <summary>Note ID for edit mode. Null/empty = create mode.</summary>
    private string? _noteId;
    public string? NoteId
    {
        get => _noteId;
        set
        {
            _noteId = value;
            IsEditing = !string.IsNullOrEmpty(value);
        }
    }

    // ── View State ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _previewHtml = string.Empty;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private Guid? _selectedFolderId;

    // ── Commands ───────────────────────────────────────────────────

    /// <summary>Loads the note for editing (called on page appearing in edit mode).</summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (!IsEditing || string.IsNullOrEmpty(NoteId)) return;
        if (!Guid.TryParse(NoteId, out var noteId)) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            var note = await _notesApi.GetNoteAsync(serverUrl, token, noteId, ct);
            Title = note.Title;
            Content = note.Content;
            SelectedFolderId = note.FolderId;
            _currentVersion = note.Version;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load note for editing.");
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Saves the note (creates or updates depending on mode).</summary>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required.";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);

            if (IsEditing && Guid.TryParse(NoteId, out var noteId))
            {
                await _notesApi.UpdateNoteAsync(serverUrl, token, noteId,
                    new UpdateNoteDto
                    {
                        Title = Title,
                        Content = Content,
                        FolderId = SelectedFolderId,
                        ExpectedVersion = _currentVersion
                    }, ct);
            }
            else
            {
                await _notesApi.CreateNoteAsync(serverUrl, token,
                    new CreateNoteDto
                    {
                        Title = Title,
                        Content = Content,
                        FolderId = SelectedFolderId,
                        Format = NoteContentFormat.Markdown
                    }, ct);
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save note.");
            ErrorMessage = ApiExceptionHelper.GetUserFriendlyMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Renders the current content as markdown for live preview.</summary>
    [RelayCommand]
    private async Task RenderPreviewAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Content))
        {
            PreviewHtml = "<p style='color:#94A3B8'><em>Nothing to preview.</em></p>";
            IsPreviewVisible = true;
            return;
        }

        try
        {
            var (serverUrl, token) = await GetCredentialsAsync(ct);
            PreviewHtml = await _notesApi.RenderMarkdownAsync(serverUrl, token, Content, ct);
            IsPreviewVisible = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render markdown preview.");
            PreviewHtml = $"<p style='color:red'>Preview failed.</p>";
            IsPreviewVisible = true;
        }
    }

    /// <summary>Toggles between edit and preview mode.</summary>
    [RelayCommand]
    private void TogglePreview()
    {
        if (IsPreviewVisible)
        {
            IsPreviewVisible = false;
        }
        else
        {
            RenderPreviewCommand.Execute(null);
        }
    }

    /// <summary>Navigates back without saving.</summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    // ── Private Helpers ────────────────────────────────────────────

    private async Task<(string ServerUrl, string Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive()
            ?? throw new InvalidOperationException("No active server connection.");
        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct)
            ?? throw new InvalidOperationException("No access token available.");
        return (connection.ServerBaseUrl, token);
    }
}
```

---

## Step 5: Main Tab Page XAML

**File:** `src/Clients/DotNetCloud.Client.Android/Views/NotesPage.xaml`

Follow the exact same visual pattern as `CalendarPage.xaml`:

- Dark background `#0F172A`
- `Shell.TitleView` with logo + title
- `x:DataType` on the page for compiled bindings

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:DotNetCloud.Client.Android.ViewModels"
             xmlns:core="clr-namespace:DotNetCloud.Core.DTOs;assembly=DotNetCloud.Core"
             x:Class="DotNetCloud.Client.Android.Views.NotesPage"
             x:DataType="vm:NotesViewModel"
             Title="Notes"
             BackgroundColor="#0F172A"
             Shell.NavBarIsVisible="True">

    <Shell.TitleView>
        <HorizontalStackLayout Spacing="8" VerticalOptions="Center">
            <Image Source="logo.png"
                   HeightRequest="36" WidthRequest="36"
                   VerticalOptions="Center"/>
            <Label Text="Notes"
                   FontSize="20" FontAttributes="Bold"
                   TextColor="#F1F5F9" VerticalOptions="Center"/>
        </HorizontalStackLayout>
    </Shell.TitleView>

    <Grid RowDefinitions="Auto,Auto,*"
          BackgroundColor="#0F172A">

        <!-- Row 0: Search bar -->
        <SearchBar Grid.Row="0"
                   Placeholder="Search notes..."
                   Text="{Binding SearchQuery}"
                   SearchCommand="{Binding SearchCommand}"
                   BackgroundColor="#1E293B"
                   TextColor="#F1F5F9"
                   PlaceholderColor="#64748B"
                   CancelButtonColor="#0EA5E9"
                   Margin="8,8,8,0"/>

        <!-- Row 1: Folder chips row -->
        <ScrollView Grid.Row="1"
                    Orientation="Horizontal"
                    HorizontalScrollBarVisibility="Never"
                    Margin="8,4,8,0">
            <HorizontalStackLayout Spacing="6"
                                   BindableLayout.ItemsSource="{Binding Folders}">
                <!-- "All Notes" chip (always first) -->
                <Border StrokeShape="RoundRectangle 16"
                        BackgroundColor="{Binding SelectedFolderId, Converter={StaticResource FolderChipColorConverter}}"
                        Stroke="#334155"
                        Padding="12,4"
                        HeightRequest="32">
                    <Border.GestureRecognizers>
                        <TapGestureRecognizer Command="{Binding SelectFolderCommand}"
                                              CommandParameter="{x:Null}"/>
                    </Border.GestureRecognizers>
                    <Label Text="All Notes"
                           FontSize="13" TextColor="#F1F5F9"
                           VerticalOptions="Center"/>
                </Border>

                <!-- Folder chips -->
                <Border StrokeShape="RoundRectangle 16"
                        BackgroundColor="#1E293B"
                        Stroke="#334155"
                        Padding="12,4"
                        HeightRequest="32">
                    <Border.GestureRecognizers>
                        <TapGestureRecognizer
                            Command="{Binding Source={RelativeSource AncestorType={x:Type ContentPage}}, Path=BindingContext.SelectFolderCommand}"
                            CommandParameter="{Binding Id}"/>
                    </Border.GestureRecognizers>
                    <Label Text="{Binding Name}"
                           FontSize="13" TextColor="#F1F5F9"
                           VerticalOptions="Center"/>
                </Border>
            </HorizontalStackLayout>
        </ScrollView>

        <!-- Row 2: Notes list (or preview panel) -->

        <!-- A: Preview panel (visible when a note is selected) -->
        <Grid Grid.Row="2" IsVisible="{Binding IsPreviewVisible}">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- Preview header -->
            <HorizontalStackLayout Grid.Row="0" Spacing="8" Padding="12,8">
                <Button Text="&lt; Back"
                        BackgroundColor="Transparent"
                        TextColor="#0EA5E9" FontSize="14"
                        Command="{Binding ClosePreviewCommand}"/>
                <Label Text="{Binding SelectedNote.Title}"
                       FontSize="18" FontAttributes="Bold"
                       TextColor="#F1F5F9"
                       VerticalOptions="Center"/>
            </HorizontalStackLayout>

            <!-- Rendered HTML -->
            <WebView Grid.Row="1"
                     HtmlSource="{Binding PreviewHtml}"
                     BackgroundColor="#0F172A"
                     Margin="12,0"/>

            <!-- Preview footer actions -->
            <HorizontalStackLayout Grid.Row="2" Spacing="12" Padding="12,8"
                                   HorizontalOptions="Center">
                <Button Text="Edit"
                        BackgroundColor="#1E293B" TextColor="#0EA5E9"
                        FontSize="14" Padding="16,8"
                        CornerRadius="8" BorderColor="#334155" BorderWidth="1"
                        Command="{Binding EditNoteCommand}"
                        CommandParameter="{Binding SelectedNote}"/>
                <Button Text="Delete"
                        BackgroundColor="#1E293B" TextColor="#EF4444"
                        FontSize="14" Padding="16,8"
                        CornerRadius="8" BorderColor="#334155" BorderWidth="1"
                        Command="{Binding DeleteNoteCommand}"
                        CommandParameter="{Binding SelectedNote}"/>
            </HorizontalStackLayout>
        </Grid>

        <!-- B: Notes list (visible when no note is selected) -->
        <Grid Grid.Row="2" IsVisible="{Binding IsPreviewVisible, Converter={StaticResource InvertBoolConverter}}">
            <RefreshView IsRefreshing="{Binding IsLoading}"
                         Command="{Binding RefreshCommand}"
                         RefreshColor="#0EA5E9">
                <CollectionView ItemsSource="{Binding Notes}"
                                SelectionMode="Single"
                                SelectionChanged="OnNoteSelected">

                    <CollectionView.EmptyView>
                        <VerticalStackLayout VerticalOptions="Center"
                                             HorizontalOptions="Center"
                                             Padding="40">
                            <Label Text="📝"
                                   FontSize="48"
                                   HorizontalOptions="Center"/>
                            <Label Text="No notes yet"
                                   FontSize="18" FontAttributes="Bold"
                                   TextColor="#94A3B8"
                                   HorizontalOptions="Center"
                                   Margin="0,12,0,4"/>
                            <Label Text="Tap + to create your first note"
                                   FontSize="14"
                                   TextColor="#64748B"
                                   HorizontalOptions="Center"/>
                        </VerticalStackLayout>
                    </CollectionView.EmptyView>

                    <CollectionView.ItemTemplate>
                        <DataTemplate x:DataType="core:NoteDto">
                            <!-- Each note item: title, preview, pin/favorite icons, date -->
                            <Border Margin="8,4"
                                    Padding="12,10"
                                    BackgroundColor="#1E293B"
                                    StrokeShape="RoundRectangle 8"
                                    Stroke="#334155">
                                <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto">
                                    <!-- Title row -->
                                    <HorizontalStackLayout Grid.Row="0" Grid.Column="0" Spacing="6">
                                        <!-- Pin indicator -->
                                        <Label Text="📌" FontSize="12"
                                               IsVisible="{Binding IsPinned}"
                                               VerticalOptions="Center"/>
                                        <!-- Favorite indicator -->
                                        <Label Text="⭐" FontSize="12"
                                               IsVisible="{Binding IsFavorite}"
                                               VerticalOptions="Center"/>
                                        <!-- Title -->
                                        <Label Text="{Binding Title}"
                                               FontSize="15" FontAttributes="Bold"
                                               TextColor="#F1F5F9"
                                               LineBreakMode="TailTruncation"
                                               MaxLines="1"
                                               VerticalOptions="Center"/>
                                    </HorizontalStackLayout>

                                    <!-- Updated date -->
                                    <Label Grid.Row="0" Grid.Column="1"
                                           Text="{Binding UpdatedAt, StringFormat='{0:MMM dd}'}"
                                           FontSize="11"
                                           TextColor="#64748B"
                                           VerticalOptions="Center"/>

                                    <!-- Content preview -->
                                    <Label Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2"
                                           Text="{Binding Content}"
                                           FontSize="13"
                                           TextColor="#94A3B8"
                                           LineBreakMode="TailTruncation"
                                           MaxLines="2"
                                           Margin="0,4,0,0"/>

                                    <!-- Tags + action buttons -->
                                    <HorizontalStackLayout Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
                                                           Spacing="8" Margin="0,6,0,0">
                                        <!-- Pin toggle -->
                                        <HorizontalStackLayout Spacing="2"
                                            GestureRecognizers="{TapGestureRecognizer Command={Binding Source={RelativeSource AncestorType={x:Type ContentPage}}, Path=BindingContext.TogglePinCommand}, CommandParameter={Binding .}}">
                                            <Label Text="📌" FontSize="12"
                                                   TextColor="{Binding IsPinned, Converter={StaticResource BoolToColorConverter}, ConverterParameter=#0EA5E9|#475569}"/>
                                        </HorizontalStackLayout>
                                        <!-- Favorite toggle -->
                                        <HorizontalStackLayout Spacing="2"
                                            GestureRecognizers="{TapGestureRecognizer Command={Binding Source={RelativeSource AncestorType={x:Type ContentPage}}, Path=BindingContext.ToggleFavoriteCommand}, CommandParameter={Binding .}}">
                                            <Label Text="⭐" FontSize="12"
                                                   TextColor="{Binding IsFavorite, Converter={StaticResource BoolToColorConverter}, ConverterParameter=#F59E0B|#475569}"/>
                                        </HorizontalStackLayout>
                                        <!-- Delete -->
                                        <HorizontalStackLayout Spacing="2" HorizontalOptions="EndAndExpand"
                                            GestureRecognizers="{TapGestureRecognizer Command={Binding Source={RelativeSource AncestorType={x:Type ContentPage}}, Path=BindingContext.DeleteNoteCommand}, CommandParameter={Binding .}}">
                                            <Label Text="🗑️" FontSize="12"/>
                                        </HorizontalStackLayout>
                                    </HorizontalStackLayout>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>
            </RefreshView>

            <!-- FAB: Create new note -->
            <Button Text="+"
                    FontSize="24"
                    BackgroundColor="#0EA5E9"
                    TextColor="#FFFFFF"
                    CornerRadius="28"
                    WidthRequest="56" HeightRequest="56"
                    HorizontalOptions="End" VerticalOptions="End"
                    Margin="0,0,16,16"
                    Shadow="{Shadow Color='#000000', Opacity=0.3, Radius=8, Offset='2,2'}"
                    Command="{Binding CreateNoteCommand}"/>
        </Grid>
    </Grid>
</ContentPage>
```

---

## Step 6: Main Tab Page Code-Behind

**File:** `src/Clients/DotNetCloud.Client.Android/Views/NotesPage.xaml.cs`

Follow the exact pattern from `CalendarPage.xaml.cs`.

```csharp
namespace DotNetCloud.Client.Android.Views;

using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;

/// <summary>Main Notes tab page with note list, search, folders, and preview.</summary>
public partial class NotesPage : ContentPage
{
    private readonly NotesViewModel _vm;

    public NotesPage(NotesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsActive = true;
        _vm.ErrorMessage = null;
        if (_vm.Folders.Count == 0 && _vm.LoadFoldersCommand.CanExecute(null))
        {
            _vm.LoadFoldersCommand.Execute(null);
        }
        if (_vm.Notes.Count == 0 && _vm.LoadNotesCommand.CanExecute(null))
        {
            _vm.LoadNotesCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsActive = false;
        _vm.ErrorMessage = null;
    }

    private void OnNoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is NoteDto note)
        {
            _vm.SelectNoteCommand.Execute(note);
        }
        // Clear selection to allow re-selecting the same item
        if (sender is CollectionView cv)
            cv.SelectedItem = null;
    }
}
```

---

## Step 7: Create/Edit Page XAML

**File:** `src/Clients/DotNetCloud.Client.Android/Views/NoteEditPage.xaml`

This page has the markdown formatting toolbar, a large Editor, and a preview toggle.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:DotNetCloud.Client.Android.ViewModels"
             x:Class="DotNetCloud.Client.Android.Views.NoteEditPage"
             x:DataType="vm:NoteEditViewModel"
             Title="{Binding IsEditing, Converter={StaticResource BoolToStringConverter}, ConverterParameter='Edit Note|New Note'}"
             BackgroundColor="#0F172A">

    <Shell.TitleView>
        <HorizontalStackLayout Spacing="8" VerticalOptions="Center">
            <Image Source="logo.png"
                   HeightRequest="36" WidthRequest="36"
                   VerticalOptions="Center"/>
            <Label Text="{Binding IsEditing, Converter={StaticResource BoolToStringConverter}, ConverterParameter='Edit Note|New Note'}"
                   FontSize="18" FontAttributes="Bold"
                   TextColor="#F1F5F9" VerticalOptions="Center"/>
        </HorizontalStackLayout>
    </Shell.TitleView>

    <!-- Toolbar: Cancel (left), Save (right) -->
    <Shell.ToolbarItems>
        <ToolbarItem Text="Cancel"
                     Command="{Binding CancelCommand}"
                     Order="Primary"/>
        <ToolbarItem Text="Save"
                     Command="{Binding SaveCommand}"
                     Order="Primary"/>
    </Shell.ToolbarItems>

    <Grid RowDefinitions="Auto,Auto,*,Auto"
          BackgroundColor="#0F172A">

        <!-- Row 0: Title entry -->
        <Entry Grid.Row="0"
               Placeholder="Note title"
               Text="{Binding Title}"
               BackgroundColor="#1E293B"
               TextColor="#F1F5F9"
               PlaceholderColor="#64748B"
               FontSize="18"
               Margin="12,12,12,0"/>

        <!-- Row 1: Markdown formatting toolbar -->
        <HorizontalStackLayout Grid.Row="1"
                                Spacing="4"
                                Padding="8,4"
                                BackgroundColor="#1E293B"
                                Margin="12,4,12,0">
            <Button Text="B" FontAttributes="Bold"
                    Clicked="OnBoldClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="I" FontAttributes="Italic"
                    Clicked="OnItalicClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="H"
                    Clicked="OnHeadingClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" FontAttributes="Bold" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="•"
                    Clicked="OnBulletListClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="16" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="1."
                    Clicked="OnNumberedListClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="🔗"
                    Clicked="OnLinkClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="&lt;&gt;"
                    Clicked="OnCodeClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="12" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>
            <Button Text="❝"
                    Clicked="OnBlockquoteClicked"
                    BackgroundColor="Transparent" TextColor="#F1F5F9"
                    FontSize="14" WidthRequest="36" HeightRequest="36"
                    Padding="0" BorderWidth="1" BorderColor="#334155"
                    CornerRadius="4"/>

            <!-- Spacer -->
            <BoxView WidthRequest="1" HeightRequest="24"
                     Color="#334155" VerticalOptions="Center"/>

            <!-- Preview toggle button -->
            <Button Text="{Binding IsPreviewVisible, Converter={StaticResource BoolToStringConverter}, ConverterParameter='Edit|Preview'}"
                    Clicked="OnTogglePreviewClicked"
                    BackgroundColor="#0EA5E9" TextColor="#FFFFFF"
                    FontSize="12" HeightRequest="32"
                    Padding="12,0" CornerRadius="16"
                    VerticalOptions="Center"/>
        </HorizontalStackLayout>

        <!-- Row 2: Editor / Preview WebView -->
        <Grid Grid.Row="2" Margin="12,8,12,0">

            <!-- Edit mode: Multi-line Editor -->
            <Editor x:Name="ContentEditor"
                    Placeholder="Start writing... (Markdown supported)"
                    Text="{Binding Content}"
                    BackgroundColor="#1E293B"
                    TextColor="#F1F5F9"
                    PlaceholderColor="#64748B"
                    FontSize="15"
                    IsVisible="{Binding IsPreviewVisible, Converter={StaticResource InvertBoolConverter}}"
                    AutoSize="TextChanges"
                    TextChanged="OnContentTextChanged"/>

            <!-- Preview mode: Rendered HTML -->
            <WebView x:Name="PreviewWebView"
                     IsVisible="{Binding IsPreviewVisible}"
                     BackgroundColor="#0F172A"/>
        </Grid>

        <!-- Row 3: Folder picker -->
        <HorizontalStackLayout Grid.Row="3" Spacing="8" Padding="12,8,12,12">
            <Label Text="Folder:"
                   TextColor="#94A3B8"
                   FontSize="14"
                   VerticalOptions="Center"/>
            <Picker x:Name="FolderPicker"
                    Title="None"
                    TextColor="#F1F5F9"
                    TitleColor="#64748B"
                    VerticalOptions="Center"
                    HorizontalOptions="FillAndExpand"/>
        </HorizontalStackLayout>
    </Grid>
</ContentPage>
```

---

## Step 8: Create/Edit Page Code-Behind

**File:** `src/Clients/DotNetCloud.Client.Android/Views/NoteEditPage.xaml.cs`

This file contains TWO important pieces:

1. **Markdown toolbar button handlers** — insert syntax at cursor
2. **Smart continuation on Enter** — auto-insert list/blockquote prefixes

```csharp
namespace DotNetCloud.Client.Android.Views;

using DotNetCloud.Client.Android.ViewModels;

/// <summary>Create/edit page for notes with markdown formatting toolbar.</summary>
public partial class NoteEditPage : ContentPage
{
    private readonly NoteEditViewModel _vm;

    public NoteEditPage(NoteEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }

    // ── Markdown Toolbar Button Handlers ────────────────────────────

    /// <summary>Inserts or wraps selection with **bold**.</summary>
    private void OnBoldClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("**", "**", "bold text");

    /// <summary>Inserts or wraps selection with *italic*.</summary>
    private void OnItalicClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("*", "*", "italic text");

    /// <summary>Prefixes the current line with ## heading.</summary>
    private void OnHeadingClicked(object? sender, EventArgs e)
        => PrefixLine("## ");

    /// <summary>Prefixes the current line with - bullet.</summary>
    private void OnBulletListClicked(object? sender, EventArgs e)
        => PrefixLine("- ");

    /// <summary>Prefixes the current line with 1. numbered list.</summary>
    private void OnNumberedListClicked(object? sender, EventArgs e)
        => PrefixLine("1. ");

    /// <summary>Inserts [text](url) template.</summary>
    private void OnLinkClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("[", "](url)", "link text");

    /// <summary>Inserts or wraps selection with `code`.</summary>
    private void OnCodeClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("`", "`", "code");

    /// <summary>Prefixes the current line with > blockquote.</summary>
    private void OnBlockquoteClicked(object? sender, EventArgs e)
        => PrefixLine("> ");

    /// <summary>Toggles between edit and preview modes.</summary>
    private void OnTogglePreviewClicked(object? sender, EventArgs e)
    {
        if (_vm.IsPreviewVisible)
        {
            _vm.IsPreviewVisible = false;
        }
        else
        {
            _vm.RenderPreviewCommand.Execute(null);
            // Update the WebView with the rendered HTML
            PreviewWebView.Source = new HtmlWebViewSource { Html = _vm.PreviewHtml };
        }
    }

    // ── Smart Continuation on Enter ─────────────────────────────────

    /// <summary>
    /// Handles the Editor's TextChanged event to detect Enter key and
    /// auto-continue list/blockquote prefixes on the new line.
    /// </summary>
    private void OnContentTextChanged(object? sender, TextChangedEventArgs e)
    {
        // We detect Enter by checking if a '\n' was added at the end
        if (e.NewTextValue is null || e.OldTextValue is null) return;
        if (e.NewTextValue.Length <= e.OldTextValue.Length) return;

        // Check if the new text ends with a newline and the old text didn't
        bool enterPressed = e.NewTextValue.EndsWith("\n") &&
                            !e.OldTextValue.EndsWith("\n") &&
                            e.NewTextValue.Length == e.OldTextValue.Length + 1;

        if (!enterPressed) return;

        // Get the line that was just completed (the line before the new \n)
        string newText = e.NewTextValue;
        int cursorPos = newText.Length; // cursor is at the end

        // Find start of current line (last \n before the final one)
        int lineStart = newText.LastIndexOf('\n', cursorPos - 2);
        if (lineStart < 0) lineStart = 0;
        else lineStart++; // skip the \n itself

        string previousLine = newText[lineStart..(cursorPos - 1)]; // exclude the new \n

        // Determine prefix to auto-insert
        string? prefix = GetContinuationPrefix(previousLine);
        if (prefix is null) return;

        // Remove empty prefix lines (e.g. user typed "- " then Enter)
        if (previousLine.TrimEnd() == prefix.TrimEnd())
        {
            // Replace: remove the empty prefix line and the newline
            string before = newText[..lineStart];
            string after = newText[(cursorPos)..];
            _vm.Content = before + after;
            return;
        }

        // Insert the continuation prefix
        _vm.Content = newText + prefix;

        // Set cursor position after the inserted prefix
        // (MAUI Editor doesn't expose SetCursorPosition directly, so we append)
        ContentEditor.CursorPosition = _vm.Content.Length;
    }

    /// <summary>
    /// Returns the continuation prefix for the given line, or null if no continuation.
    /// </summary>
    private static string? GetContinuationPrefix(string line)
    {
        // Trim trailing whitespace but preserve leading whitespace + prefix
        string trimmed = line.TrimEnd();

        // Blockquote: "> text" → "> "
        if (trimmed.StartsWith("> ") && trimmed.Length > 2)
            return "> ";

        // Unordered list: "- text", "* text", "+ text" → same prefix
        if ((trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            && trimmed.Length > 2)
        {
            return trimmed[..2]; // e.g., "- "
        }

        // Numbered list: "1. text", "2. text" → incremented number
        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\.\s");
        if (match.Success && trimmed.Length > match.Length)
        {
            if (int.TryParse(match.Groups[1].Value, out int num))
                return $"{num + 1}. ";
        }

        return null;
    }

    // ── Markdown Insertion Helpers ──────────────────────────────────

    /// <summary>
    /// Inserts markdown around the selection, or inserts placeholder text
    /// wrapped in the given prefix/suffix if nothing is selected.
    /// </summary>
    private void InsertMarkdownSyntax(string prefix, string suffix, string placeholder)
    {
        string content = _vm.Content;
        int cursorPos = ContentEditor.CursorPosition;
        int selectionLen = ContentEditor.SelectionLength;

        if (selectionLen > 0)
        {
            // Wrap selected text
            string selected = content.Substring(cursorPos, selectionLen);
            string replacement = prefix + selected + suffix;
            _vm.Content = content[..cursorPos] + replacement + content[(cursorPos + selectionLen)..];
        }
        else
        {
            // Insert placeholder
            string insertion = prefix + placeholder + suffix;
            _vm.Content = content[..cursorPos] + insertion + content[cursorPos..];
        }
    }

    /// <summary>
    /// Prefixes the current line with the given text.
    /// </summary>
    private void PrefixLine(string prefix)
    {
        string content = _vm.Content;
        int cursorPos = ContentEditor.CursorPosition;

        // Find start of current line
        int lineStart = content.LastIndexOf('\n', cursorPos > 0 ? cursorPos - 1 : 0);
        if (lineStart < 0) lineStart = 0;
        else lineStart++; // skip the \n

        string linePrefix = content[lineStart..cursorPos];
        // Don't double-prefix
        if (linePrefix.StartsWith(prefix))
            return;

        _vm.Content = content[..lineStart] + prefix + content[lineStart..];
    }
}
```

---

## Step 9: Add Tab to AppShell

**File:** `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`

Add a new `<ShellContent>` inside the `<TabBar>`. The Notes module is required (per `NOTES_REQUIRED_MODULE_PLAN.md`), so the tab is **always visible** — no `IsVisible="False"` or module probing needed.

Insert this block after the Calendar tab and before the Settings tab:

```xml
        <ShellContent
            Route="Notes"
            Title="Notes"
            Icon="notes_icon.png"
            ContentTemplate="{DataTemplate views:NotesPage}"/>
```

Full TabBar order after change: Chat → Files → Music (conditional) → Calendar → **Notes** → Settings

## Step 10: Register Detail Route

**File:** `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs`

Add the `NoteEdit` route registration in the constructor, following the existing pattern:

```csharp
Routing.RegisterRoute("NoteEdit", typeof(NoteEditPage));
```

Full constructor after change:

```csharp
public AppShell()
{
    InitializeComponent();
    _musicTab = MusicTab;

    Routing.RegisterRoute("MessageList", typeof(MessageListPage));
    Routing.RegisterRoute("ChannelDetails", typeof(ChannelDetailsPage));
    Routing.RegisterRoute("EventDetail", typeof(EventDetailPage));
    Routing.RegisterRoute("EventEdit", typeof(EventEditPage));
    Routing.RegisterRoute("ImageViewer", typeof(ImageViewerPage));
    Routing.RegisterRoute("NoteEdit", typeof(NoteEditPage));
}
```

---

## Step 11: DI Registration in MauiProgram

**File:** `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`

Add three registrations:

1. **REST client** — same pattern as Calendar, Files, Music, Chat:

```csharp
// ── Notes ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient<INotesRestClient, HttpNotesRestClient>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

2. **ViewModels** — add with the other `AddTransient<...ViewModel>()` calls:

```csharp
builder.Services.AddTransient<NotesViewModel>();
builder.Services.AddTransient<NoteEditViewModel>();
```

3. **Pages** — add with the other `AddTransient<...Page>()` calls:

```csharp
builder.Services.AddTransient<NotesPage>();
builder.Services.AddTransient<NoteEditPage>();
```

Also add the new `using` at the top:

```csharp
using DotNetCloud.Client.Android.Notes;
```

---

## Step 12: Tab Icon SVG

**File:** `src/Clients/DotNetCloud.Client.Android/Resources/Images/notes_icon.svg`

Create a simple note/document outline icon matching the existing tab icon visual style. Reference existing icons: `chat_icon.svg`, `files_icon.svg`, `music_icon.svg`, `calendar_icon.svg`, `settings_icon.svg`.

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
  <polyline points="14 2 14 8 20 8"/>
  <line x1="16" y1="13" x2="8" y2="13"/>
  <line x1="16" y1="17" x2="8" y2="17"/>
  <polyline points="10 9 9 9 8 9"/>
</svg>
```

Note: XAML references `notes_icon.png` — MAUI automatically converts SVGs to PNGs during build.

---

## Step 13: Unit Tests

**Directory:** `tests/DotNetCloud.Client.Android.Tests/Notes/`

Create test files following the existing test project patterns. If no Android test project exists, add tests to the most relevant existing test project.

### Test: HttpNotesRestClient

Test the REST client with a mocked `HttpMessageHandler`. Cover:

- `ListNotesAsync` — returns empty list, returns notes, passes folderId query param, passes skip/take
- `GetNoteAsync` — returns note, throws on not found
- `CreateNoteAsync` — sends correct DTO, returns created note
- `UpdateNoteAsync` — sends correct DTO with ExpectedVersion, returns updated note
- `DeleteNoteAsync` — calls correct URL, succeeds
- `SearchNotesAsync` — URL-encodes query, returns results
- `GetNotePreviewAsync` — returns preview response with HTML
- `RenderMarkdownAsync` — sends content, returns HTML string
- `ListFoldersAsync` — returns folders, passes parentId
- `CreateFolderAsync` / `UpdateFolderAsync` / `DeleteFolderAsync` — basic CRUD
- Envelope unwrapping: handles `{ "success": true, "data": [...] }`, handles error responses

### Test: NotesViewModel

Test with mocked `INotesRestClient`:

- `LoadNotesCommand` populates `Notes` collection, handles errors, handles empty
- `LoadFoldersCommand` populates `Folders` collection
- `SearchCommand` clears folder filter and reloads
- `SelectFolderCommand` sets filter and reloads
- `TogglePinCommand` calls UpdateNoteAsync with inverted IsPinned
- `ToggleFavoriteCommand` calls UpdateNoteAsync with inverted IsFavorite
- `DeleteNoteCommand` shows confirmation, removes from collection on confirm
- `SelectNoteCommand` loads preview HTML and sets IsPreviewVisible
- `ClosePreviewCommand` clears state

### Test: NoteEditViewModel

Test with mocked `INotesRestClient`:

- `LoadCommand` in edit mode: loads note and populates fields
- `LoadCommand` in create mode: does nothing (no NoteId)
- `SaveCommand` in create mode: calls CreateNoteAsync, navigates back
- `SaveCommand` in edit mode: calls UpdateNoteAsync with ExpectedVersion, navigates back
- `SaveCommand` with empty title: shows error, does not save
- `RenderPreviewCommand`: calls RenderMarkdownAsync, sets PreviewHtml

---

## Implementation Order

For maximum efficiency, implement in this order:

1. **Step 1 + 2** (REST client interface + implementation) — no dependencies
2. **Step 12** (tab icon) — no dependencies
3. **Step 3 + 4** (both ViewModels) — depends on Step 1
4. **Step 5 + 6 + 7 + 8** (all four page files) — depends on Step 3 + 4
5. **Step 9 + 10 + 11** (Shell + DI integration) — depends on Step 5 + 6 + 7 + 8
6. **Step 13** (unit tests) — depends on Step 1 + 3 + 4

Steps 1, 2, and 12 can be done in parallel. Steps 3+4 can be done together. Steps 5-8 can be done together.

---

## Post-Implementation Verification

1. **Build:**

```powershell
dotnet build src\Clients\DotNetCloud.Client.Android -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
```

2. **Unit tests:**

```powershell
dotnet test
```

3. **Deploy and manual test:**

```powershell
adb install -r src\Clients\DotNetCloud.Client.Android\bin\Debug\net10.0-android\android-arm64\net.dotnetcloud.client-Signed.apk
```

4. **Manual test checklist:**
   - [ ] Notes tab appears in tab bar with correct icon
   - [ ] Notes list loads from server showing existing notes
   - [ ] Search bar filters notes by title/content
   - [ ] Folder chips filter notes (tap folder → only that folder's notes)
   - [ ] "All Notes" chip clears filter
   - [ ] Tap note → rendered markdown preview displays
   - [ ] Preview: Edit/Delete buttons work
   - [ ] Create new note (FAB "+"): fills title + content → saves → appears in list
   - [ ] Edit existing note: changes persist → version increments
   - [ ] Pin/unpin toggle updates in real time
   - [ ] Favorite/unfavorite toggle updates in real time
   - [ ] Delete note with confirmation → note removed from list
   - [ ] Create folder via prompt → appears in folder chips
   - [ ] Rename folder via long-press (or tap and hold)
   - [ ] Delete folder → confirmation → removed; notes un-filed
   - [ ] Markdown toolbar: Bold, Italic, Heading, Bullet, Numbered, Link, Code, Blockquote all insert correct syntax
   - [ ] Smart Enter: typing `- item` then Enter auto-inserts `- ` on next line
   - [ ] Smart Enter: typing `1. item` then Enter auto-inserts `2. `
   - [ ] Smart Enter: typing `> quote` then Enter auto-inserts `> `
   - [ ] Smart Enter: empty prefix line (just `- ` then Enter) ends the list
   - [ ] Preview toggle shows rendered markdown in edit page
   - [ ] Pull-to-refresh reloads notes and folders
   - [ ] Errors display user-friendly messages (not stack traces)
   - [ ] Loading indicator shows during network calls
