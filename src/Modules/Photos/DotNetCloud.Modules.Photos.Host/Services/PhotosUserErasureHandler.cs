using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos.Host.Services;

/// <summary>
/// Erases a deleted account's photos, albums, edit history and shares on behalf of the core's
/// account-erasure orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// The module declares <c>dataStore: "isolated"</c>, so every photo lives in a schema only this
/// module can reach. Deleting the account must therefore delete them here (F12); the core records
/// the acknowledgement and retries for 72 hours, and an account deletion never reports success while
/// a module row is outstanding.
/// </para>
/// <para>
/// <c>IgnoreQueryFilters</c> is deliberate: photos and albums carry a soft-delete filter, and a row
/// a user had already "deleted" in the UI is still personal data that must not survive an account
/// deletion.
/// </para>
/// <para>
/// Shares are deleted in both directions — shares the account created and shares aimed at it — because
/// each is personal data (who shared what with whom). Albums and photos owned by another user are
/// never touched.
/// </para>
/// <para>
/// Idempotent by construction: after the first successful pass there is nothing left to delete, so a
/// retried request reports zero rows and still acknowledges.
/// </para>
/// </remarks>
public sealed class PhotosUserErasureHandler : IModuleUserErasureHandler
{
    /// <summary>Scope key reporting the number of photos deleted.</summary>
    public const string PhotosScopeKey = "photos";

    /// <summary>Scope key reporting the number of albums deleted.</summary>
    public const string AlbumsScopeKey = "albums";

    /// <summary>Scope key reporting the number of shares deleted.</summary>
    public const string SharesScopeKey = "shares";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhotosUserErasureHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="PhotosUserErasureHandler"/> class.</summary>
    /// <param name="scopeFactory">Creates the scope the scoped <see cref="PhotosDbContext"/> comes from.</param>
    /// <param name="logger">The logger.</param>
    public PhotosUserErasureHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<PhotosUserErasureHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ModuleUserErasureResult> EraseAsync(
        ModuleUserErasureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();

        var userId = context.UserId;

        var photos = await db.Photos
            .IgnoreQueryFilters()
            .Where(photo => photo.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var albums = await db.Albums
            .IgnoreQueryFilters()
            .Where(album => album.OwnerId == userId)
            .ToListAsync(cancellationToken);

        var photoIds = photos.Select(photo => photo.Id).ToList();
        var albumIds = albums.Select(album => album.Id).ToList();

        // Shares name a photo or an album, so they are collected before the parents are removed and
        // include both directions: created by the account, and aimed at it.
        var shares = await db.PhotoShares
            .Where(share =>
                share.SharedByUserId == userId
                || share.SharedWithUserId == userId
                || (share.PhotoId != null && photoIds.Contains(share.PhotoId.Value))
                || (share.AlbumId != null && albumIds.Contains(share.AlbumId.Value)))
            .ToListAsync(cancellationToken);

        if (shares.Count > 0)
        {
            db.PhotoShares.RemoveRange(shares);
        }

        if (photoIds.Count > 0 || albumIds.Count > 0)
        {
            var albumPhotos = await db.AlbumPhotos
                .Where(link => photoIds.Contains(link.PhotoId) || albumIds.Contains(link.AlbumId))
                .ToListAsync(cancellationToken);

            var metadata = await db.PhotoMetadata
                .Where(item => photoIds.Contains(item.PhotoId))
                .ToListAsync(cancellationToken);

            var tags = await db.PhotoTags
                .Where(tag => photoIds.Contains(tag.PhotoId))
                .ToListAsync(cancellationToken);

            var edits = await db.PhotoEditRecords
                .Where(edit => photoIds.Contains(edit.PhotoId) || edit.EditedByUserId == userId)
                .ToListAsync(cancellationToken);

            db.AlbumPhotos.RemoveRange(albumPhotos);
            db.PhotoMetadata.RemoveRange(metadata);
            db.PhotoTags.RemoveRange(tags);
            db.PhotoEditRecords.RemoveRange(edits);

            if (albums.Count > 0)
            {
                db.Albums.RemoveRange(albums);
            }

            if (photos.Count > 0)
            {
                db.Photos.RemoveRange(photos);
            }
        }

        if (shares.Count > 0 || photoIds.Count > 0 || albumIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // The account id is personal data: it is deliberately never logged
        // (cs/exposure-of-sensitive-information).
        _logger.LogInformation(
            "Erased {PhotoCount} photo(s), {AlbumCount} album(s) and {ShareCount} share(s) for a deleted account.",
            photos.Count,
            albums.Count,
            shares.Count);

        return ModuleUserErasureResult.Success(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [PhotosScopeKey] = photos.Count,
                [AlbumsScopeKey] = albums.Count,
                [SharesScopeKey] = shares.Count,
            });
    }
}
