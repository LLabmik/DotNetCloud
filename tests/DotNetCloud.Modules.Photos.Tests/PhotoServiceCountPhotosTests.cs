using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests for <see cref="PhotoService.CountPhotosAsync"/> — the exact total the gallery
/// pager uses to compute the number of pages.
/// </summary>
[TestClass]
public class PhotoServiceCountPhotosTests
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

    [TestMethod]
    public async Task CountPhotos_NoPhotos_ReturnsZero()
    {
        var caller = TestHelpers.CreateCaller();

        var count = await _service.CountPhotosAsync(caller);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task CountPhotos_MultiplePhotos_ReturnsExactTotal()
    {
        var caller = TestHelpers.CreateCaller();
        for (var i = 0; i < 7; i++)
            await TestHelpers.SeedPhotoAsync(_db, caller.UserId, $"photo{i}.jpg");

        var count = await _service.CountPhotosAsync(caller);

        Assert.AreEqual(7, count);
    }

    [TestMethod]
    public async Task CountPhotos_OtherUsersPhotos_AreNotCounted()
    {
        var caller = TestHelpers.CreateCaller();
        var otherUser = Guid.CreateVersion7();

        await TestHelpers.SeedPhotoAsync(_db, caller.UserId, "mine-1.jpg");
        await TestHelpers.SeedPhotoAsync(_db, caller.UserId, "mine-2.jpg");
        await TestHelpers.SeedPhotoAsync(_db, otherUser, "theirs.jpg");

        var count = await _service.CountPhotosAsync(caller);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task CountPhotos_SoftDeletedPhoto_IsNotCounted()
    {
        var caller = TestHelpers.CreateCaller();
        await TestHelpers.SeedPhotoAsync(_db, caller.UserId, "kept.jpg");
        var deleted = await TestHelpers.SeedPhotoAsync(_db, caller.UserId, "deleted.jpg");

        deleted.IsDeleted = true;
        await _db.SaveChangesAsync();

        var count = await _service.CountPhotosAsync(caller);

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task CountPhotos_IsIndependentOfListPaging()
    {
        var caller = TestHelpers.CreateCaller();
        for (var i = 0; i < 12; i++)
            await TestHelpers.SeedPhotoAsync(_db, caller.UserId, $"photo{i}.jpg");

        var count = await _service.CountPhotosAsync(caller);
        var page = await _service.ListPhotosAsync(caller, skip: 0, take: 5);

        // The total must describe the whole library, not the page that was fetched.
        Assert.AreEqual(12, count);
        Assert.AreEqual(5, page.Count);
    }
}
