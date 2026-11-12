using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for non-destructive photo editing.
/// Edit operations are stored as a JSON stack; originals are never modified.
/// </summary>
public sealed class PhotoEditService : IPhotoEditService
{
    private readonly PhotosDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PhotoEditService> _logger;
    private readonly IPhotoThumbnailService? _previews;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoEditService"/> class.
    /// </summary>
    /// <param name="db">Photos database context.</param>
    /// <param name="eventBus">Event bus.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="previews">
    /// Preview renderer, used to un-bake edits that were already saved. Optional so a unit test that
    /// only exercises the stack does not need the whole image pipeline.
    /// </param>
    public PhotoEditService(
        PhotosDbContext db,
        IEventBus eventBus,
        ILogger<PhotoEditService> logger,
        IPhotoThumbnailService? previews = null)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _previews = previews;
    }

    /// <summary>
    /// Applies a non-destructive edit operation to a photo.
    /// </summary>
    public async Task<PhotoEditRecordDto> ApplyEditAsync(Guid photoId, PhotoEditOperationDto operation, CallerContext caller, CancellationToken cancellationToken = default)
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            PhotoId = photoId,
            EditedByUserId = caller.UserId,
            EditType = operation.OperationType.ToString()
        }, caller, cancellationToken);

        // Project to the boundary DTO here. Returning the entity would serialize its Photo navigation,
        // i.e. every cached thumbnail as a base64 blob — a 27 MB answer to a 40-byte edit.
        return new PhotoEditRecordDto
        {
            Id = record.Id,
            PhotoId = record.PhotoId,
            OperationType = record.OperationType,
            Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(record.ParametersJson)
                ?? new Dictionary<string, string>(),
            StackOrder = record.StackOrder,
            CreatedAt = record.CreatedAt
        };
    }

    /// <inheritdoc />
    async Task<PhotoEditRecordDto> IPhotoEditService.ApplyEditAsync(Guid photoId, PhotoEditOperationDto operation, CallerContext caller, CancellationToken cancellationToken)
    {
        return await ApplyEditAsync(photoId, operation, caller, cancellationToken);
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

        // The previews may have been baked with the edits that were just dropped, so "revert all" has
        // to re-render the original or it leaves the edits visible on screen.
        await RebuildPreviewsAsync(photoId, caller.UserId, cancellationToken);
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

        // Undo has the same problem as revert: a saved stack is baked into the previews, so dropping
        // the top operation leaves it visible until the previews are rendered again.
        await RebuildPreviewsAsync(photoId, caller.UserId, cancellationToken);
    }

    private async Task RebuildPreviewsAsync(Guid photoId, Guid userId, CancellationToken cancellationToken)
    {
        if (_previews is null)
        {
            return;
        }

        try
        {
            await _previews.RegeneratePreviewsAsync(photoId, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            // The stack change is committed and is the source of truth; a preview rebuild that fails
            // (the core could not serve the original, say) must not fail the user's request.
            _logger.LogWarning(ex, "Could not rebuild previews for photo {PhotoId} after an edit-stack change", photoId);
        }
    }

    private static void ValidateOperation(PhotoEditOperationDto operation)
    {
        switch (operation.OperationType)
        {
            case PhotoEditType.Rotate:
                // `value` is the key the image pipeline applies (PhotoThumbnailService.ApplyEditToImage):
                // clockwise degrees. ImageSharp only has 0/90/180/270, so "-90" is not a rotation.
                if (!operation.Parameters.TryGetValue("value", out var degreesStr))
                    throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Rotate requires a 'value' of 90, 180, or 270 degrees.");

                if (!int.TryParse(degreesStr, out var degrees) || (degrees != 90 && degrees != 180 && degrees != 270))
                    throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Rotation must be 90, 180, or 270 degrees.");
                break;

            case PhotoEditType.Crop:
                var requiredParams = new[] { "x", "y", "width", "height" };
                foreach (var param in requiredParams)
                {
                    if (!operation.Parameters.TryGetValue(param, out var raw)
                        || !int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                        || parsed < 0)
                    {
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, $"Crop operation requires a non-negative integer '{param}' parameter.");
                    }
                }

                if (int.Parse(operation.Parameters["width"], System.Globalization.CultureInfo.InvariantCulture) == 0
                    || int.Parse(operation.Parameters["height"], System.Globalization.CultureInfo.InvariantCulture) == 0)
                {
                    throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Crop width and height must be at least 1 pixel.");
                }

                break;

            case PhotoEditType.Flip:
                if (!operation.Parameters.TryGetValue("value", out var direction))
                    throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Flip requires a 'value' of 0 (horizontal) or 1 (vertical).");

                if (!int.TryParse(direction, out var flip) || (flip != 0 && flip != 1))
                    throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, "Flip value must be 0 (horizontal) or 1 (vertical).");
                break;

            case PhotoEditType.Brightness:
            case PhotoEditType.Contrast:
            case PhotoEditType.Saturation:
                if (operation.Parameters.TryGetValue("value", out var valueStr))
                {
                    if (!float.TryParse(valueStr, out var value) || value < -100f || value > 100f)
                        throw new BusinessRuleException(ErrorCodes.InvalidPhotoEdit, $"{operation.OperationType} value must be between -100 and 100.");
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
