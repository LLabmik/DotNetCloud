using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for non-destructive photo editing.
/// Edit operations are stored as a JSON stack; originals are never modified.
/// </summary>
public sealed class PhotoEditService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PhotoEditService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoEditService"/> class.
    /// </summary>
    public PhotoEditService(PhotosDbContext db, IEventBus eventBus, ILogger<PhotoEditService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Applies a non-destructive edit operation to a photo.
    /// </summary>
    public async Task<PhotoEditRecord> ApplyEditAsync(Guid photoId, PhotoEditOperationDto operation, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        ValidateOperation(operation);

        var maxOrder = await _db.PhotoEditRecords
            .Where(e => e.PhotoId == photoId)
            .MaxAsync(e => (int?)e.StackOrder, cancellationToken) ?? 0;

        var record = new PhotoEditRecord
        {
            PhotoId = photoId,
            OperationType = operation.OperationType.ToString(),
            ParametersJson = JsonSerializer.Serialize(operation.Parameters),
            StackOrder = maxOrder + 1,
            EditedByUserId = caller.UserId
        };

        _db.PhotoEditRecords.Add(record);
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Edit {OperationType} applied to photo {PhotoId} by user {UserId}", operation.OperationType, photoId, caller.UserId);

        await _eventBus.PublishAsync(new PhotoEditedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PhotoId = photoId,
            EditedByUserId = caller.UserId,
            EditType = operation.OperationType.ToString()
        }, caller, cancellationToken);

        return record;
    }

    /// <summary>
    /// Gets the edit stack for a photo (ordered by stack position).
    /// </summary>
    public async Task<IReadOnlyList<PhotoEditOperationDto>> GetEditStackAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var records = await _db.PhotoEditRecords
            .Where(e => e.PhotoId == photoId)
            .OrderBy(e => e.StackOrder)
            .ToListAsync(cancellationToken);

        return records.Select(r => new PhotoEditOperationDto
        {
            OperationType = Enum.Parse<PhotoEditType>(r.OperationType),
            Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(r.ParametersJson)
                ?? new Dictionary<string, string>()
        }).ToList();
    }

    /// <summary>
    /// Reverts all edits on a photo (clears the edit stack).
    /// </summary>
    public async Task RevertAllAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        var records = await _db.PhotoEditRecords
            .Where(e => e.PhotoId == photoId)
            .ToListAsync(cancellationToken);

        _db.PhotoEditRecords.RemoveRange(records);
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All edits reverted for photo {PhotoId} by user {UserId}", photoId, caller.UserId);
    }

    /// <summary>
    /// Undoes the last edit operation on a photo.
    /// </summary>
    public async Task UndoLastEditAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.PhotoNotFound, "Photo not found.");

        var lastRecord = await _db.PhotoEditRecords
            .Where(e => e.PhotoId == photoId)
            .OrderByDescending(e => e.StackOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastRecord is null)
            return;

        _db.PhotoEditRecords.Remove(lastRecord);
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateOperation(PhotoEditOperationDto operation)
    {
        switch (operation.OperationType)
        {
            case PhotoEditType.Rotate:
                if (operation.Parameters.TryGetValue("degrees", out var degreesStr))
                {
                    if (!int.TryParse(degreesStr, out var degrees) || (degrees != 90 && degrees != 180 && degrees != 270))
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Rotation must be 90, 180, or 270 degrees.");
                }
                break;

            case PhotoEditType.Crop:
                var requiredParams = new[] { "x", "y", "width", "height" };
                foreach (var param in requiredParams)
                {
                    if (!operation.Parameters.ContainsKey(param))
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, $"Crop operation requires '{param}' parameter.");
                }
                break;

            case PhotoEditType.Flip:
                if (operation.Parameters.TryGetValue("direction", out var direction))
                {
                    if (direction != "horizontal" && direction != "vertical")
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Flip direction must be 'horizontal' or 'vertical'.");
                }
                break;

            case PhotoEditType.Brightness:
            case PhotoEditType.Contrast:
            case PhotoEditType.Saturation:
                if (operation.Parameters.TryGetValue("value", out var valueStr))
                {
                    if (!float.TryParse(valueStr, out var value) || value < -1.0f || value > 1.0f)
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, $"{operation.OperationType} value must be between -1.0 and 1.0.");
                }
                break;

            case PhotoEditType.Sharpen:
            case PhotoEditType.Blur:
                if (operation.Parameters.TryGetValue("radius", out var radiusStr))
                {
                    if (!float.TryParse(radiusStr, out var radius) || radius < 0 || radius > 100)
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, $"{operation.OperationType} radius must be between 0 and 100.");
                }
                break;
        }
    }
}
