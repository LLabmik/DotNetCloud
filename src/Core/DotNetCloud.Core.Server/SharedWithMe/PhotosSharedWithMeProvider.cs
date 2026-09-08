using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Adapts the Photos module's in-process services into an <see cref="ISharedWithMeProvider"/>
/// so Files can surface shared photos and albums as virtual deep-link entries under
/// <c>_DotNetCloud/SharedWithMe/Photos</c>.
/// </summary>
/// <remarks>
/// Lives in Core.Server (aggregation is a core concern) where the Photos UI services are registered
/// in-process (<c>AddPhotosUiServices</c>). A fresh DI scope is created per call so scoped services
/// (and their scoped Photos DbContext) never outlive the request. All module failures degrade
/// gracefully to an empty list rather than throwing into the Files listing.
/// </remarks>
public sealed class PhotosSharedWithMeProvider : ISharedWithMeProvider
{
    /// <summary>Stable module id for Photos.</summary>
    public const string PhotosModuleId = "photos";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotosSharedWithMeProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotosSharedWithMeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per call.</param>
    /// <param name="logger">Logger for graceful module-failure diagnostics.</param>
    public PhotosSharedWithMeProvider(IServiceScopeFactory scopeFactory, ILogger<PhotosSharedWithMeProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => PhotosModuleId;

    /// <inheritdoc />
    public string DisplayName => "Photos";

    /// <inheritdoc />
    public string IconName => "photo_library";

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await ListAsync(userId, cancellationToken)).Count;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var shareService = scope.ServiceProvider.GetRequiredService<IPhotoShareService>();
        var albumService = scope.ServiceProvider.GetRequiredService<IAlbumService>();
        var photoService = scope.ServiceProvider.GetRequiredService<IPhotoService>();
        var caller = new CallerContext(userId, ["user"], CallerType.User);

        IReadOnlyList<PhotoShareDto> shares;
        try
        {
            shares = await shareService.GetSharedWithMeAsync(caller, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Photos shared-with-me lookup failed for {UserId}; returning empty", userId);
            return [];
        }

        if (shares.Count == 0)
        {
            return [];
        }

        return await MapItemsAsync(shares, albumService, photoService, caller, cancellationToken);
    }

    /// <summary>
    /// Maps photo/album shares to shared-with-me items, resolving titles from the module services.
    /// Entities that can no longer be resolved (deleted or access revoked) are skipped, and the
    /// same entity is only surfaced once even when multiple shares reference it.
    /// </summary>
    /// <param name="shares">Shares visible to the caller (user + team, unexpired).</param>
    /// <param name="albumService">Album service used to resolve album titles.</param>
    /// <param name="photoService">Photo service used to resolve photo file names.</param>
    /// <param name="caller">The caller context for the recipient.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<IReadOnlyList<SharedWithMeModuleItem>> MapItemsAsync(
        IReadOnlyList<PhotoShareDto> shares,
        IAlbumService albumService,
        IPhotoService photoService,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        // Resolve each unique album/photo once (avoid N+1 on duplicate shares of one entity).
        var albumIds = shares
            .Where(s => s.AlbumId.HasValue)
            .Select(s => s.AlbumId!.Value)
            .Distinct()
            .ToList();
        var photoIds = shares
            .Where(s => s.PhotoId.HasValue)
            .Select(s => s.PhotoId!.Value)
            .Distinct()
            .ToList();

        var albums = new Dictionary<Guid, AlbumDto>();
        foreach (var albumId in albumIds)
        {
            try
            {
                var album = await albumService.GetAlbumAsync(albumId, caller, cancellationToken);
                if (album is not null)
                {
                    albums[albumId] = album;
                }
            }
            catch (Exception ex)
            {
                // Fall through: album unresolved -> its shares are skipped below.
                _logger.LogWarning(ex, "Failed to resolve shared album {AlbumId}", albumId);
            }
        }

        var photos = new Dictionary<Guid, PhotoDto>();
        foreach (var photoId in photoIds)
        {
            try
            {
                var photo = await photoService.GetPhotoAsync(photoId, caller, cancellationToken);
                if (photo is not null)
                {
                    photos[photoId] = photo;
                }
            }
            catch (Exception ex)
            {
                // Fall through: photo unresolved -> its shares are skipped below.
                _logger.LogWarning(ex, "Failed to resolve shared photo {PhotoId}", photoId);
            }
        }

        var seen = new HashSet<(string Type, Guid Id)>();
        var items = new List<SharedWithMeModuleItem>();
        foreach (var share in shares)
        {
            if (share.AlbumId is { } albumId && albums.TryGetValue(albumId, out var album))
            {
                if (!seen.Add(("Album", albumId)))
                {
                    continue;
                }

                items.Add(new SharedWithMeModuleItem(
                    ModuleId: PhotosModuleId,
                    DisplayName: "Photos",
                    EntityId: albumId,
                    EntityType: "Album",
                    Title: album.Title,
                    Subtitle: "Album",
                    DeepLink: $"/apps/photos?albumId={albumId}",
                    IconName: "photo_library",
                    UpdatedAt: album.UpdatedAt == default ? share.CreatedAt : album.UpdatedAt));
            }
            else if (share.PhotoId is { } photoId && photos.TryGetValue(photoId, out var photo))
            {
                if (!seen.Add(("Photo", photoId)))
                {
                    continue;
                }

                items.Add(new SharedWithMeModuleItem(
                    ModuleId: PhotosModuleId,
                    DisplayName: "Photos",
                    EntityId: photoId,
                    EntityType: "Photo",
                    Title: photo.FileName,
                    Subtitle: "Photo",
                    DeepLink: $"/apps/photos?photoId={photoId}",
                    IconName: "image",
                    UpdatedAt: photo.UpdatedAt == default ? share.CreatedAt : photo.UpdatedAt));
            }
        }

        return items;
    }
}
