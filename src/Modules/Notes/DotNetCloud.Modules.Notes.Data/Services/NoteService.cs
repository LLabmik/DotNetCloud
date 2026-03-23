using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="INoteService"/>.
/// </summary>
public sealed class NoteService : INoteService
{
    private readonly NotesDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<NoteService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteService"/> class.
    /// </summary>
    public NoteService(NotesDbContext db, IEventBus eventBus, ILogger<NoteService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NoteDto> CreateNoteAsync(CreateNoteDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Validate folder exists and belongs to user if specified
        if (dto.FolderId.HasValue)
        {
            var folderExists = await _db.NoteFolders
                .AnyAsync(f => f.Id == dto.FolderId.Value && f.OwnerId == caller.UserId, cancellationToken);

            if (!folderExists)
            {
                throw new Core.Errors.ValidationException(
                    Core.Errors.ErrorCodes.NoteFolderNotFound, "Note folder not found.");
            }
        }

        var note = new Note
        {
            OwnerId = caller.UserId,
            CreatedByUserId = caller.UserId,
            UpdatedByUserId = caller.UserId,
            FolderId = dto.FolderId,
            Title = dto.Title,
            Content = dto.Content,
            Format = dto.Format
        };

        // Add tags
        foreach (var tag in dto.Tags)
        {
            note.Tags.Add(new NoteTag { Tag = tag });
        }

        // Add links
        foreach (var link in dto.Links)
        {
            note.Links.Add(new NoteLink
            {
                LinkType = link.LinkType,
                TargetId = link.TargetId,
                DisplayLabel = link.DisplayLabel
            });
        }

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note {NoteId} '{Title}' created by user {UserId}",
            note.Id, note.Title, caller.UserId);

        await _eventBus.PublishAsync(new NoteCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = note.Id,
            Title = note.Title,
            OwnerId = caller.UserId,
            FolderId = note.FolderId
        }, caller, cancellationToken);

        return MapToDto(note);
    }

    /// <inheritdoc />
    public async Task<NoteDto?> GetNoteAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var note = await QueryNotes()
            .FirstOrDefaultAsync(n => n.Id == noteId &&
                (n.OwnerId == caller.UserId || n.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        return note is null ? null : MapToDto(note);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> ListNotesAsync(CallerContext caller, Guid? folderId = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = QueryNotes()
            .Where(n => n.OwnerId == caller.UserId || n.Shares.Any(s => s.SharedWithUserId == caller.UserId));

        if (folderId.HasValue)
        {
            query = query.Where(n => n.FolderId == folderId.Value);
        }

        var notes = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return notes.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<NoteDto> UpdateNoteAsync(Guid noteId, UpdateNoteDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var note = await _db.Notes
            .Include(n => n.Shares)
            .FirstOrDefaultAsync(n => n.Id == noteId && !n.IsDeleted &&
                (n.OwnerId == caller.UserId ||
                 n.Shares.Any(s => s.SharedWithUserId == caller.UserId && s.Permission == NoteSharePermission.ReadWrite)),
                cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");

        // Optimistic concurrency check
        if (dto.ExpectedVersion.HasValue && note.Version != dto.ExpectedVersion.Value)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.NoteVersionConflict,
                $"Version conflict: expected {dto.ExpectedVersion.Value}, actual {note.Version}.");
        }

        // Save a version snapshot before updating
        _db.NoteVersions.Add(new NoteVersion
        {
            NoteId = note.Id,
            VersionNumber = note.Version,
            Title = note.Title,
            Content = note.Content,
            EditedByUserId = caller.UserId
        });

        // Apply partial updates
        if (dto.FolderId.HasValue) note.FolderId = dto.FolderId;
        if (dto.Title is not null) note.Title = dto.Title;
        if (dto.Content is not null) note.Content = dto.Content;
        if (dto.Format.HasValue) note.Format = dto.Format.Value;
        if (dto.IsPinned.HasValue) note.IsPinned = dto.IsPinned.Value;
        if (dto.IsFavorite.HasValue) note.IsFavorite = dto.IsFavorite.Value;

        if (dto.Tags is not null)
        {
            // Load and remove existing tags separately to avoid tracking conflicts
            var existingTags = await _db.NoteTags
                .Where(t => t.NoteId == noteId)
                .ToListAsync(cancellationToken);
            _db.NoteTags.RemoveRange(existingTags);

            foreach (var tag in dto.Tags)
            {
                _db.NoteTags.Add(new NoteTag { NoteId = noteId, Tag = tag });
            }
        }

        if (dto.Links is not null)
        {
            var existingLinks = await _db.NoteLinks
                .Where(l => l.NoteId == noteId)
                .ToListAsync(cancellationToken);
            _db.NoteLinks.RemoveRange(existingLinks);

            foreach (var link in dto.Links)
            {
                _db.NoteLinks.Add(new NoteLink
                {
                    NoteId = noteId,
                    LinkType = link.LinkType,
                    TargetId = link.TargetId,
                    DisplayLabel = link.DisplayLabel
                });
            }
        }

        note.Version++;
        note.ETag = Guid.NewGuid().ToString("N");
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedByUserId = caller.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note {NoteId} updated to version {Version} by user {UserId}",
            noteId, note.Version, caller.UserId);

        await _eventBus.PublishAsync(new NoteUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = noteId,
            UpdatedByUserId = caller.UserId,
            NewVersion = note.Version
        }, caller, cancellationToken);

        // Reload with all navigations for the response DTO
        var updated = await QueryNotes()
            .FirstAsync(n => n.Id == noteId, cancellationToken);
        return MapToDto(updated);
    }

    /// <inheritdoc />
    public async Task DeleteNoteAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var note = await _db.Notes
            .FirstOrDefaultAsync(n => n.Id == noteId && !n.IsDeleted && n.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");

        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;
        note.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note {NoteId} soft-deleted by user {UserId}", noteId, caller.UserId);

        await _eventBus.PublishAsync(new NoteDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            NoteId = noteId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(CallerContext caller, string? query = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var q = QueryNotes()
            .Where(n => n.OwnerId == caller.UserId ||
                        n.Shares.Any(s => s.SharedWithUserId == caller.UserId));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(n =>
                n.Title.Contains(term) ||
                n.Content.Contains(term) ||
                n.Tags.Any(t => t.Tag.Contains(term)));
        }

        var notes = await q
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return notes.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteVersionDto>> GetVersionHistoryAsync(Guid noteId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Verify access
        var noteExists = await _db.Notes
            .AnyAsync(n => n.Id == noteId &&
                (n.OwnerId == caller.UserId || n.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        if (!noteExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");
        }

        var versions = await _db.NoteVersions
            .AsNoTracking()
            .Where(v => v.NoteId == noteId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return versions.Select(v => new NoteVersionDto
        {
            Id = v.Id,
            NoteId = v.NoteId,
            VersionNumber = v.VersionNumber,
            Title = v.Title,
            Content = v.Content,
            EditedByUserId = v.EditedByUserId,
            CreatedAt = v.CreatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<NoteDto> RestoreVersionAsync(Guid noteId, Guid versionId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var note = await _db.Notes
            .Include(n => n.Tags)
            .Include(n => n.Links)
            .FirstOrDefaultAsync(n => n.Id == noteId && !n.IsDeleted && n.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteNotFound, "Note not found or access denied.");

        var version = await _db.NoteVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.NoteId == noteId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteVersionNotFound, "Note version not found.");

        // Save current state as a version before restoring
        _db.NoteVersions.Add(new NoteVersion
        {
            NoteId = note.Id,
            VersionNumber = note.Version,
            Title = note.Title,
            Content = note.Content,
            EditedByUserId = caller.UserId
        });

        // Restore the old version's content
        note.Title = version.Title;
        note.Content = version.Content;
        note.Version++;
        note.ETag = Guid.NewGuid().ToString("N");
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note {NoteId} restored to version {VersionNumber} (now version {NewVersion}) by user {UserId}",
            noteId, version.VersionNumber, note.Version, caller.UserId);

        return MapToDto(note);
    }

    private IQueryable<Note> QueryNotes()
    {
        return _db.Notes
            .Include(n => n.Tags)
            .Include(n => n.Links)
            .Include(n => n.Shares)
            .AsNoTracking();
    }

    private static NoteDto MapToDto(Note n)
    {
        return new NoteDto
        {
            Id = n.Id,
            OwnerId = n.OwnerId,
            FolderId = n.FolderId,
            Title = n.Title,
            Content = n.Content,
            Format = n.Format,
            IsPinned = n.IsPinned,
            IsFavorite = n.IsFavorite,
            IsDeleted = n.IsDeleted,
            DeletedAt = n.DeletedAt,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
            Version = n.Version,
            Tags = n.Tags.Select(t => t.Tag).ToList(),
            Links = n.Links.Select(l => new NoteLinkDto
            {
                LinkType = l.LinkType,
                TargetId = l.TargetId,
                DisplayLabel = l.DisplayLabel
            }).ToList(),
            ContentLength = n.Content.Length,
            ETag = n.ETag
        };
    }
}
