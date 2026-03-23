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

/// <summary>
/// Read-only DTO for a note share.
/// </summary>
public sealed record NoteShareDto
{
    /// <summary>Share ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Note ID.</summary>
    public required Guid NoteId { get; init; }

    /// <summary>User the note is shared with.</summary>
    public required Guid SharedWithUserId { get; init; }

    /// <summary>Permission level.</summary>
    public required NoteSharePermission Permission { get; init; }

    /// <summary>When the share was created.</summary>
    public required DateTime CreatedAt { get; init; }
}
