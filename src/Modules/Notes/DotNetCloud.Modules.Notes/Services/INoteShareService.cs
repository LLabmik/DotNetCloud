using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Models;

namespace DotNetCloud.Modules.Notes.Services;

/// <summary>
/// Note sharing operations.
/// </summary>
public interface INoteShareService
{
    /// <summary>Shares a note with a user.</summary>
    Task<NoteShareDto> ShareNoteAsync(Guid noteId, Guid targetUserId, NoteSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists shares for a note.</summary>
    Task<IReadOnlyList<NoteShareDto>> ListSharesAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a note share.</summary>
    Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default);
}
