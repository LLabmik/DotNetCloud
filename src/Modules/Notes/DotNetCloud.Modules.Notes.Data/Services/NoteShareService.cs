using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="INoteShareService"/>.
/// </summary>
public sealed class NoteShareService : INoteShareService
{
    private readonly NotesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<NoteShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteShareService"/> class.
    /// </summary>
    public NoteShareService(
        NotesDbContext db,
        IEventBus eventBus,
        ILogger<NoteShareService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NoteShareDto> ShareNoteAsync(Guid noteId, Guid targetUserId, NoteSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Verify the note exists and the caller owns it
        var noteExists = await _db.Notes
            .AnyAsync(n => n.Id == noteId && n.OwnerId == caller.UserId, cancellationToken);

        if (!noteExists)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");
        }

        // Check if already shared
        var existingShare = await _db.NoteShares
            .FirstOrDefaultAsync(s => s.NoteId == noteId && s.SharedWithUserId == targetUserId, cancellationToken);

        if (existingShare is not null)
        {
            // Update permission
            existingShare.Permission = permission;
            existingShare.UpdatedByUserId = caller.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            return MapToDto(existingShare);
        }

        var share = new NoteShare
        {
            NoteId = noteId,
            SharedWithUserId = targetUserId,
            Permission = permission,
            CreatedByUserId = caller.UserId,
            UpdatedByUserId = caller.UserId
        };

        _db.NoteShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ResourceSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = caller.UserId,
            SharedWithUserId = targetUserId,
            SourceModuleId = "dotnetcloud.notes",
            EntityType = "Note",
            EntityId = noteId,
            EntityDisplayName = $"Note {noteId}",
            Permission = permission.ToString()
        }, caller, cancellationToken);

        _logger.LogInformation("Note {NoteId} shared with user {TargetUserId} ({Permission}) by user {UserId}",
            noteId, targetUserId, permission, caller.UserId);

        return MapToDto(share);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteShareDto>> ListSharesAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Verify the note exists and the caller has access
        var noteExists = await _db.Notes
            .AnyAsync(n => n.Id == noteId &&
                (n.OwnerId == caller.UserId || n.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        if (!noteExists)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");
        }

        var shares = await _db.NoteShares
            .AsNoTracking()
            .Where(s => s.NoteId == noteId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return shares.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var share = await _db.NoteShares
            .Include(s => s.Note)
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Share not found.");

        // Only the note owner can remove shares
        if (share.Note?.OwnerId != caller.UserId)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Share not found or access denied.");
        }

        _db.NoteShares.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note share {ShareId} removed by user {UserId}", shareId, caller.UserId);
    }

    private static NoteShareDto MapToDto(NoteShare s)
    {
        return new NoteShareDto
        {
            Id = s.Id,
            NoteId = s.NoteId,
            SharedWithUserId = s.SharedWithUserId,
            Permission = s.Permission,
            CreatedAt = s.CreatedAt
        };
    }
}
