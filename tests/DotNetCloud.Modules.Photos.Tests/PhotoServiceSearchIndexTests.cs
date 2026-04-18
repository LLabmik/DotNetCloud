using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Tests that <see cref="PhotoService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create and delete operations.
/// </summary>
[TestClass]
public class PhotoServiceSearchIndexTests
{
    private PhotosDbContext _db = null!;
    private PhotoService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new PhotoService(_db, _eventBusMock.Object, NullLogger<PhotoService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreatePhoto_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var fileNodeId = Guid.NewGuid();
        var result = await _service.CreatePhotoAsync(
            fileNodeId, "photo.jpg", "image/jpeg", 1024, _caller.UserId, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "photos" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeletePhoto_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var fileNodeId = Guid.NewGuid();
        var created = await _service.CreatePhotoAsync(
            fileNodeId, "photo.jpg", "image/jpeg", 1024, _caller.UserId, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeletePhotoAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "photos" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
