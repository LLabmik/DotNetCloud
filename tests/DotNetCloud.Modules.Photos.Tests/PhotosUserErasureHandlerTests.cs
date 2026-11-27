using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Host.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Account erasure (F12): the module owns a deleted user's photo library, so it owns deleting it.
/// </summary>
[TestClass]
public class PhotosUserErasureHandlerTests
{
    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        // One database name for every scope: the store is keyed by name, so evaluating the name
        // inside the options callback would give each scope its own empty database.
        var databaseName = Guid.CreateVersion7().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PhotosDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup() => _provider.Dispose();

    private PhotosUserErasureHandler CreateHandler() =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PhotosUserErasureHandler>.Instance);

    private async Task<PhotosDbContext> CreateScopeDbAsync() =>
        _provider.CreateScope().ServiceProvider.GetRequiredService<PhotosDbContext>();

    [TestMethod]
    public async Task EraseAsync_RemovesTheAccountsPhotosAlbumsAndShares()
    {
        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();

        await using (var db = await CreateScopeDbAsync())
        {
            var photo = await SeedPhotoAsync(db, userId);
            var otherPhoto = await SeedPhotoAsync(db, otherUserId);
            var album = await SeedAlbumAsync(db, userId);

            db.AlbumPhotos.Add(new Models.AlbumPhoto { AlbumId = album.Id, PhotoId = photo.Id });
            db.PhotoTags.Add(new Models.PhotoTag { PhotoId = photo.Id, Tag = "holiday" });
            db.PhotoMetadata.Add(new Models.PhotoMetadata { PhotoId = photo.Id, CameraMake = "Canon" });
            db.PhotoEditRecords.Add(new Models.PhotoEditRecord
            {
                PhotoId = photo.Id,
                OperationType = "Rotate",
                ParametersJson = """{"value":"90"}""",
                EditedByUserId = userId,
            });
            db.PhotoShares.Add(new Models.PhotoShare
            {
                PhotoId = photo.Id,
                SharedByUserId = userId,
                SharedWithUserId = otherUserId,
            });
            db.PhotoShares.Add(new Models.PhotoShare
            {
                PhotoId = otherPhoto.Id,
                SharedByUserId = otherUserId,
                SharedWithUserId = userId,
            });

            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().EraseAsync(new ModuleUserErasureContext(Guid.CreateVersion7(), userId, []));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(1, result.DeletedCounts["photos"]);
        Assert.AreEqual(1, result.DeletedCounts["albums"]);
        Assert.AreEqual(2, result.DeletedCounts["shares"]);

        await using var verifyDb = await CreateScopeDbAsync();
        Assert.AreEqual(0, await verifyDb.Photos.CountAsync(p => p.OwnerId == userId));
        Assert.AreEqual(0, await verifyDb.Albums.CountAsync(a => a.OwnerId == userId));
        Assert.AreEqual(0, await verifyDb.PhotoShares.CountAsync());
        Assert.AreEqual(0, await verifyDb.PhotoTags.CountAsync());

        // Another account's photo is untouched.
        Assert.AreEqual(1, await verifyDb.Photos.CountAsync(p => p.OwnerId == otherUserId));
    }

    [TestMethod]
    public async Task EraseAsync_IncludesSoftDeletedRows()
    {
        var userId = Guid.CreateVersion7();

        await using (var db = await CreateScopeDbAsync())
        {
            var photo = await SeedPhotoAsync(db, userId);
            photo.IsDeleted = true;
            photo.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().EraseAsync(new ModuleUserErasureContext(Guid.CreateVersion7(), userId, []));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(1, result.DeletedCounts["photos"]);
    }

    [TestMethod]
    public async Task EraseAsync_IsIdempotent()
    {
        var userId = Guid.CreateVersion7();

        await using (var db = await CreateScopeDbAsync())
        {
            await SeedPhotoAsync(db, userId);
        }

        var handler = CreateHandler();
        var first = await handler.EraseAsync(new ModuleUserErasureContext(Guid.CreateVersion7(), userId, []));
        var second = await handler.EraseAsync(new ModuleUserErasureContext(Guid.CreateVersion7(), userId, []));

        Assert.IsTrue(first.Acknowledged);
        Assert.IsTrue(second.Acknowledged);
        Assert.AreEqual(0, second.DeletedCounts["photos"]);
    }

    [TestMethod]
    public async Task EraseAsync_WithNoData_Succeeds()
    {
        var result = await CreateHandler().EraseAsync(new ModuleUserErasureContext(Guid.CreateVersion7(), Guid.CreateVersion7(), []));

        Assert.IsTrue(result.Acknowledged);
        Assert.AreEqual(0, result.DeletedCounts["photos"]);
    }

    private static async Task<Models.Photo> SeedPhotoAsync(PhotosDbContext db, Guid ownerId)
    {
        var photo = new Models.Photo
        {
            FileNodeId = Guid.CreateVersion7(),
            OwnerId = ownerId,
            FileName = "photo.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            TakenAt = DateTime.UtcNow,
        };

        db.Photos.Add(photo);
        await db.SaveChangesAsync();
        return photo;
    }

    private static async Task<Models.Album> SeedAlbumAsync(PhotosDbContext db, Guid ownerId)
    {
        var album = new Models.Album { OwnerId = ownerId, Title = "Trip" };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        return album;
    }
}
