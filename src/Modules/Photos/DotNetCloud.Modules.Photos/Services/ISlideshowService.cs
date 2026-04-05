using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Photos.Services;

/// <summary>
/// Creates slideshows from albums or photo selections.
/// </summary>
public interface ISlideshowService
{
    /// <summary>Creates a slideshow from an album.</summary>
    Task<SlideshowDto> CreateFromAlbumAsync(Guid albumId, int intervalSeconds = 5, SlideshowTransition transition = SlideshowTransition.Fade, CancellationToken cancellationToken = default);

    /// <summary>Creates a slideshow from a selection of photos.</summary>
    Task<SlideshowDto> CreateFromSelectionAsync(IReadOnlyList<Guid> photoIds, int intervalSeconds = 5, SlideshowTransition transition = SlideshowTransition.Fade);
}
