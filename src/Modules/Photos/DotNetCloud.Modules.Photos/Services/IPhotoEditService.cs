using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Provides non-destructive photo editing operations.
/// </summary>
public interface IPhotoEditService
{
    /// <summary>Applies an edit operation to a photo.</summary>
    Task ApplyEditAsync(Guid photoId, PhotoEditOperationDto operation, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the edit history stack for a photo.</summary>
    Task<IReadOnlyList<PhotoEditOperationDto>> GetEditStackAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>Reverts all edits on a photo.</summary>
    Task RevertAllAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Undoes the last edit on a photo.</summary>
    Task UndoLastEditAsync(Guid photoId, CallerContext caller, CancellationToken cancellationToken = default);
}
