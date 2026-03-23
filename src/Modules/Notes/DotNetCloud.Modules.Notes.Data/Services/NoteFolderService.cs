using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="INoteFolderService"/>.
/// </summary>
public sealed class NoteFolderService : INoteFolderService
{
    private readonly NotesDbContext _db;
    private readonly ILogger<NoteFolderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteFolderService"/> class.
    /// </summary>
    public NoteFolderService(NotesDbContext db, ILogger<NoteFolderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto> CreateFolderAsync(CreateNoteFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Validate parent folder if specified
        if (dto.ParentId.HasValue)
        {
            var parentExists = await _db.NoteFolders
                .AnyAsync(f => f.Id == dto.ParentId.Value && f.OwnerId == caller.UserId, cancellationToken);

            if (!parentExists)
            {
                throw new Core.Errors.ValidationException(
                    Core.Errors.ErrorCodes.NoteFolderNotFound, "Parent folder not found.");
            }
        }

        // Check for duplicate folder name within same parent
        var duplicate = await _db.NoteFolders
            .AnyAsync(f => f.OwnerId == caller.UserId && f.ParentId == dto.ParentId && f.Name == dto.Name, cancellationToken);

        if (duplicate)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.NoteFolderAlreadyExists,
                $"A folder named '{dto.Name}' already exists in this location.");
        }

        var folder = new NoteFolder
        {
            OwnerId = caller.UserId,
            ParentId = dto.ParentId,
            Name = dto.Name,
            Color = dto.Color
        };

        _db.NoteFolders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note folder {FolderId} '{Name}' created by user {UserId}",
            folder.Id, folder.Name, caller.UserId);

        return await MapToDtoAsync(folder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto?> GetFolderAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var folder = await _db.NoteFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == caller.UserId, cancellationToken);

        return folder is null ? null : await MapToDtoAsync(folder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NoteFolderDto>> ListFoldersAsync(CallerContext caller, Guid? parentId = null, CancellationToken cancellationToken = default)
    {
        var folders = await _db.NoteFolders
            .AsNoTracking()
            .Where(f => f.OwnerId == caller.UserId && f.ParentId == parentId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var result = new List<NoteFolderDto>();
        foreach (var folder in folders)
        {
            result.Add(await MapToDtoAsync(folder, cancellationToken));
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<NoteFolderDto> UpdateFolderAsync(Guid folderId, UpdateNoteFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var folder = await _db.NoteFolders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteFolderNotFound, "Folder not found.");

        if (dto.Name is not null) folder.Name = dto.Name;
        if (dto.ParentId.HasValue) folder.ParentId = dto.ParentId;
        if (dto.Color is not null) folder.Color = dto.Color;
        if (dto.SortOrder.HasValue) folder.SortOrder = dto.SortOrder.Value;

        folder.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note folder {FolderId} updated by user {UserId}", folderId, caller.UserId);

        return await MapToDtoAsync(folder, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteFolderAsync(Guid folderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var folder = await _db.NoteFolders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.NoteFolderNotFound, "Folder not found.");

        folder.IsDeleted = true;
        folder.DeletedAt = DateTime.UtcNow;
        folder.UpdatedAt = DateTime.UtcNow;

        // Move notes in this folder to un-filed
        var notes = await _db.Notes.Where(n => n.FolderId == folderId).ToListAsync(cancellationToken);
        foreach (var note in notes)
        {
            note.FolderId = null;
            note.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Note folder {FolderId} soft-deleted by user {UserId}", folderId, caller.UserId);
    }

    private async Task<NoteFolderDto> MapToDtoAsync(NoteFolder f, CancellationToken cancellationToken)
    {
        var noteCount = await _db.Notes
            .CountAsync(n => n.FolderId == f.Id && !n.IsDeleted, cancellationToken);

        return new NoteFolderDto
        {
            Id = f.Id,
            OwnerId = f.OwnerId,
            ParentId = f.ParentId,
            Name = f.Name,
            Color = f.Color,
            SortOrder = f.SortOrder,
            NoteCount = noteCount,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
