using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoCollectionServiceTests
{
    private VideoDbContext _db = null!;
    private VideoCollectionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new VideoCollectionService(_db, Mock.Of<ILogger<VideoCollectionService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateCollectionAsync_CreatesAndReturnsCollection()
    {
        var caller = TestHelpers.CreateCaller();
        var dto = new Core.DTOs.CreateVideoCollectionDto { Name = "My Collection", Description = "Testing" };

        var result = await _service.CreateCollectionAsync(dto, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Collection", result.Name);
        Assert.AreEqual("Testing", result.Description);
        Assert.AreEqual(0, result.VideoCount);
    }

    [TestMethod]
    public async Task ListCollectionsAsync_ReturnsUserCollections()
    {
        var caller = TestHelpers.CreateCaller();
        await TestHelpers.SeedCollectionAsync(_db, "Collection 1", caller.UserId);
        await TestHelpers.SeedCollectionAsync(_db, "Collection 2", caller.UserId);
        await TestHelpers.SeedCollectionAsync(_db, "Other User", Guid.NewGuid());

        var result = await _service.ListCollectionsAsync(caller);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetCollectionAsync_ReturnsCollection_WhenOwned()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Mine", caller.UserId);

        var result = await _service.GetCollectionAsync(collection.Id, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Mine", result.Name);
    }

    [TestMethod]
    public async Task GetCollectionAsync_ReturnsNull_WhenNotOwned()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "NotMine", Guid.NewGuid());

        var result = await _service.GetCollectionAsync(collection.Id, caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateCollectionAsync_UpdatesNameAndDescription()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Original", caller.UserId);
        var dto = new Core.DTOs.UpdateVideoCollectionDto { Name = "Updated", Description = "New desc" };

        var result = await _service.UpdateCollectionAsync(collection.Id, dto, caller);

        Assert.AreEqual("Updated", result.Name);
        Assert.AreEqual("New desc", result.Description);
    }

    [TestMethod]
    public async Task UpdateCollectionAsync_ThrowsForNonExistent()
    {
        var caller = TestHelpers.CreateCaller();
        var dto = new Core.DTOs.UpdateVideoCollectionDto { Name = "X" };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.UpdateCollectionAsync(Guid.NewGuid(), dto, caller));

        Assert.AreEqual(ErrorCodes.VideoCollectionNotFound, ex.ErrorCode);
    }

    [TestMethod]
    public async Task DeleteCollectionAsync_SoftDeletesCollection()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "ToDelete", caller.UserId);

        await _service.DeleteCollectionAsync(collection.Id, caller);

        var result = await _service.GetCollectionAsync(collection.Id, caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task AddVideoAsync_AddsVideoToCollection()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Collection", caller.UserId);
        var video = await TestHelpers.SeedVideoAsync(_db, "Video", ownerId: caller.UserId);

        await _service.AddVideoAsync(collection.Id, video.Id, caller);

        var videos = await _service.GetCollectionVideosAsync(collection.Id, caller);
        Assert.AreEqual(1, videos.Count);
    }

    [TestMethod]
    public async Task AddVideoAsync_ThrowsOnDuplicate()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Collection", caller.UserId);
        var video = await TestHelpers.SeedVideoAsync(_db, "Video", ownerId: caller.UserId);

        await _service.AddVideoAsync(collection.Id, video.Id, caller);

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.AddVideoAsync(collection.Id, video.Id, caller));

        Assert.AreEqual(ErrorCodes.VideoAlreadyInCollection, ex.ErrorCode);
    }

    [TestMethod]
    public async Task RemoveVideoAsync_RemovesVideoFromCollection()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Collection", caller.UserId);
        var video = await TestHelpers.SeedVideoAsync(_db, "Video", ownerId: caller.UserId);

        await _service.AddVideoAsync(collection.Id, video.Id, caller);
        await _service.RemoveVideoAsync(collection.Id, video.Id, caller);

        var videos = await _service.GetCollectionVideosAsync(collection.Id, caller);
        Assert.AreEqual(0, videos.Count);
    }

    [TestMethod]
    public async Task GetCollectionVideosAsync_ReturnsOrderedVideos()
    {
        var caller = TestHelpers.CreateCaller();
        var collection = await TestHelpers.SeedCollectionAsync(_db, "Collection", caller.UserId);
        var v1 = await TestHelpers.SeedVideoAsync(_db, "First", ownerId: caller.UserId);
        var v2 = await TestHelpers.SeedVideoAsync(_db, "Second", ownerId: caller.UserId);

        await _service.AddVideoAsync(collection.Id, v1.Id, caller);
        await _service.AddVideoAsync(collection.Id, v2.Id, caller);

        var videos = await _service.GetCollectionVideosAsync(collection.Id, caller);
        Assert.AreEqual(2, videos.Count);
        Assert.AreEqual("First", videos[0].Title);
    }
}
