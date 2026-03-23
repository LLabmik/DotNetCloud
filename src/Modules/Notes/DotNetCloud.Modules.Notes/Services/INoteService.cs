using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Notes.Services;

/// <summary>
/// Core note CRUD and search operations.
/// </summary>
public interface INoteService
{
    /// <summary>Creates a new note.</summary>
    Task<NoteDto> CreateNoteAsync(CreateNoteDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a note by ID.</summary>
    Task<NoteDto?> GetNoteAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists notes for the calling user with optional folder filter.</summary>
    Task<IReadOnlyList<NoteDto>> ListNotesAsync(CallerContext caller, Guid? folderId = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing note.</summary>
    Task<NoteDto> UpdateNoteAsync(Guid noteId, UpdateNoteDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a note.</summary>
    Task DeleteNoteAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Searches notes by title, content, and tags.</summary>
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync(CallerContext caller, string? query = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets version history for a note.</summary>
    Task<IReadOnlyList<NoteVersionDto>> GetVersionHistoryAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores a note to a specific version.</summary>
    Task<NoteDto> RestoreVersionAsync(Guid noteId, Guid versionId, CallerContext caller, CancellationToken cancellationToken = default);
}
