using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Shared helpers for Photos service tests.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Creates a fresh InMemory PhotosDbContext.</summary>
    public static PhotosDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PhotosDbContext(options);
    }

    /// <summary>Creates a CallerContext for a user.</summary>
    public static CallerContext CreateCaller(Guid? userId = null)
        => new(userId ?? Guid.NewGuid(), ["user"], CallerType.User);

    /// <summary>Seeds a photo in the database.</summary>
    public static async Task<Photo> SeedPhotoAsync(
        PhotosDbContext db,
        Guid ownerId,
        string fileName = "test.jpg",
        string mimeType = "image/jpeg",
        long sizeBytes = 1024)
    {
        var photo = new Photo
        {
            FileNodeId = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = fileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            TakenAt = DateTime.UtcNow
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();
        return photo;
    }

    /// <summary>Seeds an album in the database.</summary>
    public static async Task<Album> SeedAlbumAsync(
        PhotosDbContext db,
        Guid ownerId,
        string title = "Test Album")
    {
        var album = new Album
        {
            OwnerId = ownerId,
            Title = title
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }

    /// <summary>Seeds a photo with EXIF metadata attached.</summary>
    public static async Task<Photo> SeedPhotoWithMetadataAsync(
        PhotosDbContext db,
        Guid ownerId,
        double? latitude = null,
        double? longitude = null)
    {
        var photo = await SeedPhotoAsync(db, ownerId);
        var metadata = new PhotoMetadata
        {
            PhotoId = photo.Id,
            CameraMake = "Canon",
            CameraModel = "EOS R5",
            Iso = 200,
            Latitude = latitude,
            Longitude = longitude
        };
        db.PhotoMetadata.Add(metadata);
        await db.SaveChangesAsync();
        photo.Metadata = metadata;
        return photo;
    }

    /// <summary>Seeds a photo share.</summary>
    public static async Task<PhotoShare> SeedPhotoShareAsync(
        PhotosDbContext db,
        Guid photoId,
        Guid sharedByUserId,
        Guid sharedWithUserId)
    {
        var share = new PhotoShare
        {
            PhotoId = photoId,
            SharedByUserId = sharedByUserId,
            SharedWithUserId = sharedWithUserId,
            Permission = PhotoSharePermissionLevel.ReadOnly
        };
        db.PhotoShares.Add(share);
        await db.SaveChangesAsync();
        return share;
    }
}
