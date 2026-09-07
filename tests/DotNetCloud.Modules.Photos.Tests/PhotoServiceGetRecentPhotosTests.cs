using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests for <see cref="PhotoService.GetRecentPhotosAsync"/>.
/// </summary>
[TestClass]
public class PhotoServiceGetRecentPhotosTests
{
    private PhotosDbContext _db = null!;
    private PhotoService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new PhotoService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<PhotoService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static async Task SeedPhotoWithTakenAtAsync(
        PhotosDbContext db, Guid ownerId, string fileName, DateTime takenAt)
    {
        db.Photos.Add(new Photo
        {
            FileNodeId = Guid.CreateVersion7(),
            OwnerId = ownerId,
            FileName = fileName,
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            TakenAt = takenAt
        });
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetRecentPhotos_NewestFirst_ReturnsPhotosOrderedByTakenAtDescending()
    {
        var caller = TestHelpers.CreateCaller();
        var now = DateTime.UtcNow;

        await SeedPhotoWithTakenAtAsync(_db, caller.UserId, "oldest.jpg", now.AddDays(-3));
        await SeedPhotoWithTakenAtAsync(_db, caller.UserId, "middle.jpg", now.AddDays(-2));
        await SeedPhotoWithTakenAtAsync(_db, caller.UserId, "newest.jpg", now.AddDays(-1));

        var result = await _service.GetRecentPhotosAsync(caller);

        CollectionAssert.AreEqual(
            new[] { "newest.jpg", "middle.jpg", "oldest.jpg" },
            result.Select(p => p.FileName).ToArray());
    }

    [TestMethod]
    public async Task GetRecentPhotos_RespectsCount_ReturnsOnlyRequestedNumberOfNewest()
    {
        var caller = TestHelpers.CreateCaller();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
            await SeedPhotoWithTakenAtAsync(_db, caller.UserId, $"photo{i}.jpg", now.AddMinutes(i));

        var result = await _service.GetRecentPhotosAsync(caller, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "photo4.jpg", "photo3.jpg" },
            result.Select(p => p.FileName).ToArray());
    }

    [TestMethod]
    public async Task GetRecentPhotos_OnlyCallersPhotos_ExcludesOtherUsersPhotos()
    {
        var caller = TestHelpers.CreateCaller();
        var now = DateTime.UtcNow;

        await SeedPhotoWithTakenAtAsync(_db, caller.UserId, "mine.jpg", now);
        await SeedPhotoWithTakenAtAsync(_db, Guid.CreateVersion7(), "theirs.jpg", now);

        var result = await _service.GetRecentPhotosAsync(caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mine.jpg", result[0].FileName);
    }

    [TestMethod]
    public async Task GetRecentPhotos_NoPhotos_ReturnsEmptyList()
    {
        var caller = TestHelpers.CreateCaller();

        var result = await _service.GetRecentPhotosAsync(caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
