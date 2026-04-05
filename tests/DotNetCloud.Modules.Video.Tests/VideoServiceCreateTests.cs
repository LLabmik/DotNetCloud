using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoServiceCreateTests
{
    private VideoDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private VideoService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new VideoService(_db, _eventBusMock.Object, Mock.Of<ILogger<VideoService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateVideoAsync_NewFile_CreatesVideoRecord()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        var result = await _service.CreateVideoAsync(
            fileNodeId, "test.mp4", "video/mp4", 50_000_000, ownerId, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("test", result.Title);
        Assert.AreEqual("test.mp4", result.FileName);
    }

    [TestMethod]
    public async Task CreateVideoAsync_NewFile_SetsCorrectProperties()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        var result = await _service.CreateVideoAsync(
            fileNodeId, "documentary.mkv", "video/x-matroska", 1_500_000_000, ownerId, caller);

        Assert.AreEqual(fileNodeId, result.FileNodeId);
        Assert.AreEqual("documentary", result.Title);
        Assert.AreEqual("documentary.mkv", result.FileName);
        Assert.AreEqual("video/x-matroska", result.MimeType);
    }

    [TestMethod]
    public async Task CreateVideoAsync_NewFile_PublishesVideoAddedEvent()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        await _service.CreateVideoAsync(
            fileNodeId, "movie.mp4", "video/mp4", 100_000_000, ownerId, caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.Is<VideoAddedEvent>(v =>
                v.FileNodeId == fileNodeId &&
                v.OwnerId == ownerId &&
                v.FileName == "movie.mp4"),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateVideoAsync_DuplicateFileNodeId_ReturnsExistingVideo()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        var first = await _service.CreateVideoAsync(
            fileNodeId, "original.mp4", "video/mp4", 50_000_000, ownerId, caller);
        var second = await _service.CreateVideoAsync(
            fileNodeId, "duplicate.mp4", "video/mp4", 50_000_000, ownerId, caller);

        Assert.IsNotNull(second);
        // Returns existing, not creating a new one
        Assert.AreEqual(first.Title, second.Title);
    }

    [TestMethod]
    public async Task CreateVideoAsync_DuplicateFileNodeId_DoesNotPublishSecondEvent()
    {
        var fileNodeId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        await _service.CreateVideoAsync(
            fileNodeId, "original.mp4", "video/mp4", 50_000_000, ownerId, caller);
        await _service.CreateVideoAsync(
            fileNodeId, "duplicate.mp4", "video/mp4", 50_000_000, ownerId, caller);

        _eventBusMock.Verify(e => e.PublishAsync(
            It.IsAny<VideoAddedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateVideoAsync_StripExtensionForTitle()
    {
        var caller = CallerContext.CreateSystemContext();

        var result = await _service.CreateVideoAsync(
            Guid.NewGuid(), "family-vacation-2025.mp4", "video/mp4", 1024, Guid.NewGuid(), caller);

        Assert.AreEqual("family-vacation-2025", result.Title);
    }

    [TestMethod]
    public async Task CreateVideoAsync_FileWithNoExtension_UsesFullNameAsTitle()
    {
        var caller = CallerContext.CreateSystemContext();

        var result = await _service.CreateVideoAsync(
            Guid.NewGuid(), "noextension", "video/mp4", 1024, Guid.NewGuid(), caller);

        Assert.AreEqual("noextension", result.Title);
    }

    [TestMethod]
    public async Task CreateVideoAsync_PersistsToDatabase()
    {
        var fileNodeId = Guid.NewGuid();
        var caller = CallerContext.CreateSystemContext();

        await _service.CreateVideoAsync(
            fileNodeId, "test.mp4", "video/mp4", 1024, Guid.NewGuid(), caller);

        var video = await _db.Videos.FindAsync(fileNodeId);
        // We need to query differently since FindAsync uses the primary key
        var count = _db.Videos.Count(v => v.FileNodeId == fileNodeId);
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task CreateVideoAsync_MultipleUniqueFiles_CreatesMultipleRecords()
    {
        var caller = CallerContext.CreateSystemContext();
        var ownerId = Guid.NewGuid();

        await _service.CreateVideoAsync(Guid.NewGuid(), "vid1.mp4", "video/mp4", 1024, ownerId, caller);
        await _service.CreateVideoAsync(Guid.NewGuid(), "vid2.mkv", "video/x-matroska", 2048, ownerId, caller);
        await _service.CreateVideoAsync(Guid.NewGuid(), "vid3.webm", "video/webm", 512, ownerId, caller);

        Assert.AreEqual(3, _db.Videos.Count());
    }
}
