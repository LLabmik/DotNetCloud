using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

/// <summary>
/// Tests that <see cref="VideoService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create and delete operations.
/// </summary>
[TestClass]
public class VideoServiceSearchIndexTests
{
    private Video.Data.VideoDbContext _db = null!;
    private VideoService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<VideoAddedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<SearchIndexRequestEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _service = new VideoService(_db, _eventBusMock.Object, Mock.Of<IVideoSeriesService>(), Mock.Of<DotNetCloud.Core.Data.Naming.ITableNamingStrategy>(), Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), Mock.Of<ILogger<VideoService>>());
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateVideo_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var fileNodeId = Guid.CreateVersion7();
        var result = await _service.CreateVideoAsync(
            fileNodeId, "video.mp4", "video/mp4", 10240, _caller.UserId, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "video" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteVideo_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var fileNodeId = Guid.CreateVersion7();
        var created = await _service.CreateVideoAsync(
            fileNodeId, "video.mp4", "video/mp4", 10240, _caller.UserId, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeleteVideoAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "video" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
