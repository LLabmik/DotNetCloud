using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Notes;

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
    /// <summary>Note unique identifier.</summary>
    public Guid NoteId { get; set; }

    /// <summary>Note title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Rendered HTML content.</summary>
    public string RenderedHtml { get; set; } = string.Empty;

    /// <summary>Content format.</summary>
    public NoteContentFormat Format { get; set; }

    /// <summary>Version number at time of rendering.</summary>
    public int Version { get; set; }
}
