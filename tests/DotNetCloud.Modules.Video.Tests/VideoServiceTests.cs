using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoServiceTests
{
    private VideoDbContext _db = null!;
    private VideoService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new VideoService(_db, Mock.Of<IEventBus>(), Mock.Of<IVideoSeriesService>(), Mock.Of<DotNetCloud.Core.Data.Naming.ITableNamingStrategy>(), Mock.Of<ILogger<VideoService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task ListVideosAsync_ReturnsUserVideos()
    {
        var caller = TestHelpers.CreateCaller();
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Video 1", contentHash: "hash1", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Video 2", contentHash: "hash2", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Other User Video", contentHash: "hash3", ownerId: Guid.CreateVersion7());

        var result = await _service.ListVideosAsync(caller, 0, 50);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task ListVideosAsync_Pagination_RespectsSkipAndTake()
    {
        var caller = TestHelpers.CreateCaller();
        for (var i = 0; i < 5; i++)
            await TestHelpers.SeedCanonicalVideoAsync(_db, $"Video {i}", contentHash: $"hash{i}", ownerId: caller.UserId);

        var result = await _service.ListVideosAsync(caller, 2, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetVideoAsync_ReturnsVideo_WhenOwnedByCaller()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "My Video", ownerId: caller.UserId);

        var result = await _service.GetVideoAsync(video.Id, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Video", result.Title);
    }

    [TestMethod]
    public async Task GetVideoAsync_ReturnsNull_WhenNotOwned()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "Not Mine", ownerId: Guid.CreateVersion7());

        var result = await _service.GetVideoAsync(video.Id, caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchAsync_FindsMatchingVideos()
    {
        var caller = TestHelpers.CreateCaller();
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Vacation Highlights", contentHash: "shash1", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Birthday Party", contentHash: "shash2", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Vacation Clips", contentHash: "shash3", ownerId: caller.UserId);

        var result = await _service.SearchAsync(caller, "Vacation", 10);

        Assert.AreEqual(2, result.StandaloneVideos.Count);
    }

    [TestMethod]
    public async Task GetRecentVideosAsync_ReturnsOrderedByCreatedAt()
    {
        var caller = TestHelpers.CreateCaller();
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Older", contentHash: "rhash1", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Newer", contentHash: "rhash2", ownerId: caller.UserId);

        var result = await _service.GetRecentVideosAsync(caller, take: 10);

        Assert.AreEqual(2, result.Count);
        // Most recent should be first (ordered desc)
        Assert.AreEqual("Newer", result[0].Title);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_TogglesFavoriteStatus()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "Fav Video", ownerId: caller.UserId);

        var isFav = await _service.ToggleFavoriteAsync(video.Id, caller);
        Assert.IsTrue(isFav);

        isFav = await _service.ToggleFavoriteAsync(video.Id, caller);
        Assert.IsFalse(isFav);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_ThrowsForNonExistent()
    {
        var caller = TestHelpers.CreateCaller();

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.ToggleFavoriteAsync(Guid.CreateVersion7(), caller));

        Assert.AreEqual(ErrorCodes.VideoNotFound, ex.ErrorCode);
    }

    [TestMethod]
    public async Task GetFavoriteVideosAsync_ReturnsFavoritesOnly()
    {
        var caller = TestHelpers.CreateCaller();
        var (_, favVideo) = await TestHelpers.SeedCanonicalVideoAsync(_db, "Fav", contentHash: "fhash1", ownerId: caller.UserId);
        await TestHelpers.SeedCanonicalVideoAsync(_db, "Not Fav", contentHash: "fhash2", ownerId: caller.UserId);

        await _service.ToggleFavoriteAsync(favVideo.Id, caller);

        var result = await _service.GetFavoritesAsync(caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Fav", result[0].Title);
    }

    [TestMethod]
    public async Task DeleteVideoAsync_SoftDeletesVideo()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, "ToDelete", ownerId: caller.UserId);

        await _service.DeleteVideoAsync(video.Id, caller);

        var result = await _service.GetVideoAsync(video.Id, caller);
        Assert.IsNull(result); // Soft-deleted, not visible
    }

    [TestMethod]
    public async Task DeleteVideoAsync_ThrowsForNonExistent()
    {
        var caller = TestHelpers.CreateCaller();

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.DeleteVideoAsync(Guid.CreateVersion7(), caller));

        Assert.AreEqual(ErrorCodes.VideoNotFound, ex.ErrorCode);
    }
}
