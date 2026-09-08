using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    public Task<NoteShareDto> ShareNoteAsync(Guid noteId, Guid targetUserId, NoteSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
        => ShareNoteAsync(noteId, targetUserId, null, permission, caller, cancellationToken);

    /// <inheritdoc />
    public async Task<NoteShareDto> ShareNoteAsync(Guid noteId, Guid? targetUserId, Guid? targetTeamId, NoteSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // User XOR team target.
        if (targetUserId is null && targetTeamId is null)
        {
            throw new ArgumentException("A note share requires a user or a team target.", nameof(targetTeamId));
        }

        if (targetUserId is not null && targetTeamId is not null)
        {
            throw new ArgumentException("A note share cannot target both a user and a team.", nameof(targetTeamId));
        }

        // Verify the note exists and the caller owns it
        var noteExists = await _db.Notes
            .AnyAsync(n => n.Id == noteId && n.OwnerId == caller.UserId, cancellationToken);

        if (!noteExists)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");
        }

        // Check if already shared with this target
        NoteShare? existingShare;
        if (targetUserId is { } userId)
        {
            existingShare = await _db.NoteShares
                .FirstOrDefaultAsync(s => s.NoteId == noteId && s.SharedWithUserId == userId, cancellationToken);
        }
        else
        {
            existingShare = await _db.NoteShares
                .FirstOrDefaultAsync(s => s.NoteId == noteId && s.SharedWithTeamId == targetTeamId, cancellationToken);
        }

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
            SharedWithUserId = targetUserId ?? Guid.Empty,
            SharedWithTeamId = targetTeamId,
            Permission = permission,
            CreatedByUserId = caller.UserId,
            UpdatedByUserId = caller.UserId
        };

        _db.NoteShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ResourceSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = caller.UserId,
            SharedWithUserId = targetUserId,
            SharedWithTeamId = targetTeamId,
            SourceModuleId = "dotnetcloud.notes",
            EntityType = "Note",
            EntityId = noteId,
            EntityDisplayName = $"Note {noteId}",
            Permission = permission.ToString()
        }, caller, cancellationToken);

        _logger.LogInformation(
            "Note {NoteId} shared with {TargetType} {TargetId} ({Permission}) by user {UserId}",
            noteId,
            targetTeamId is not null ? "team" : "user",
            targetTeamId ?? targetUserId,
            permission,
            caller.UserId);

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
            SharedWithTeamId = s.SharedWithTeamId,
            Permission = s.Permission,
            CreatedAt = s.CreatedAt
        };
    }
}
