using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// Service for creating slideshows from albums or photo selections.
/// </summary>
public sealed class SlideshowService : ISlideshowService
{
    private readonly PhotosDbContext _db;
    private readonly ILogger<SlideshowService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlideshowService"/> class.
    /// </summary>
    public SlideshowService(PhotosDbContext db, ILogger<SlideshowService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a slideshow from an album.
    /// </summary>
    public async Task<SlideshowDto> CreateFromAlbumAsync(Guid albumId, int intervalSeconds = 5, SlideshowTransition transition = SlideshowTransition.Fade, CancellationToken cancellationToken = default)
    {
        var photoIds = await _db.AlbumPhotos
            .Where(ap => ap.AlbumId == albumId)
            .OrderBy(ap => ap.SortOrder)
            .Select(ap => ap.PhotoId)
            .ToListAsync(cancellationToken);

        return new SlideshowDto
        {
            Id = Guid.NewGuid(),
            AlbumId = albumId,
            PhotoIds = photoIds,
            IntervalSeconds = intervalSeconds,
            Transition = transition
        };
    }

    /// <summary>
    /// Creates a slideshow from a selection of photo IDs.
    /// </summary>
    public Task<SlideshowDto> CreateFromSelectionAsync(IReadOnlyList<Guid> photoIds, int intervalSeconds = 5, SlideshowTransition transition = SlideshowTransition.Fade)
    {
        return Task.FromResult(new SlideshowDto
        {
            Id = Guid.NewGuid(),
            PhotoIds = photoIds,
            IntervalSeconds = intervalSeconds,
            Transition = transition
        });
    }
}
