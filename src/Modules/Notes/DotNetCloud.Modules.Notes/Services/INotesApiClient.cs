using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Models;

namespace DotNetCloud.Modules.Notes.Services;

/// <summary>
/// HTTP API client for Notes REST endpoints.
/// </summary>
public interface INotesApiClient
{
    Task<IReadOnlyList<NoteDto>> ListNotesAsync(Guid? folderId = null, CancellationToken cancellationToken = default);
    Task<NoteDto?> GetNoteAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NoteDto?> CreateNoteAsync(CreateNoteDto dto, CancellationToken cancellationToken = default);
    Task<NoteDto?> UpdateNoteAsync(Guid noteId, UpdateNoteDto dto, CancellationToken cancellationToken = default);
    Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(CancellationToken cancellationToken = default);
    Task<string> RenderMarkdownAsync(string markdown, CancellationToken cancellationToken = default);

    // Search
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string? query, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    // Sharing
    Task<IReadOnlyList<NoteShareDto>> ListSharesAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NoteShareDto?> ShareNoteAsync(Guid noteId, Guid userId, NoteSharePermission permission = NoteSharePermission.ReadOnly, CancellationToken cancellationToken = default);
    Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default);

    // Version history
    Task<IReadOnlyList<NoteVersionDto>> GetVersionHistoryAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NoteDto?> RestoreVersionAsync(Guid noteId, Guid versionId, CancellationToken cancellationToken = default);

    // Folder CRUD
    Task<NoteFolderDto?> CreateFolderAsync(CreateNoteFolderDto dto, CancellationToken cancellationToken = default);
    Task<NoteFolderDto?> UpdateFolderAsync(Guid folderId, UpdateNoteFolderDto dto, CancellationToken cancellationToken = default);
    Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken = default);
}
