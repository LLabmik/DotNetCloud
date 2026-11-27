using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ActivityServiceTests
{
    private TracksDbContext _db = null!;
    private ActivityService _service = null!;
    private Mock<IUserDirectory> _userDirectoryMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _userDirectoryMock = new Mock<IUserDirectory>();
        _userDirectoryMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _service = new ActivityService(_db, _userDirectoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task WriteActivityAsync_CreatesActivityEntry()
    {
        var productId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var entityId = Guid.CreateVersion7();

        await _service.WriteActivityAsync(productId, userId, "workitem.created", "WorkItem", entityId, "{\"title\":\"Test\"}", CancellationToken.None);

        var activities = await _service.GetActivitiesByProductAsync(productId, 0, 50, CancellationToken.None);

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual("workitem.created", activities[0].Action);
        Assert.AreEqual("WorkItem", activities[0].EntityType);
        Assert.AreEqual(userId, activities[0].UserId);
    }

    [TestMethod]
    public async Task GetActivitiesByProductAsync_ReturnsNewestFirst()
    {
        var productId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        await _service.WriteActivityAsync(productId, userId, "first", "WorkItem", Guid.CreateVersion7(), null, CancellationToken.None);
        await Task.Delay(10);
        await _service.WriteActivityAsync(productId, userId, "second", "WorkItem", Guid.CreateVersion7(), null, CancellationToken.None);

        var activities = await _service.GetActivitiesByProductAsync(productId, 0, 50, CancellationToken.None);

        Assert.AreEqual("second", activities[0].Action);
        Assert.AreEqual("first", activities[1].Action);
    }

    [TestMethod]
    public async Task GetActivitiesByProductAsync_Pagination_Works()
    {
        var productId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        for (var i = 0; i < 5; i++)
            await _service.WriteActivityAsync(productId, userId, $"action{i}", "WorkItem", Guid.CreateVersion7(), null, CancellationToken.None);

        var page1 = await _service.GetActivitiesByProductAsync(productId, 0, 2, CancellationToken.None);
        var page2 = await _service.GetActivitiesByProductAsync(productId, 2, 2, CancellationToken.None);

        Assert.AreEqual(2, page1.Count);
        Assert.AreEqual(2, page2.Count);
    }

    [TestMethod]
    public async Task GetActivitiesByWorkItemAsync_FiltersByWorkItemId()
    {
        var productId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var workItemId = Guid.CreateVersion7();
        var otherItemId = Guid.CreateVersion7();

        await _service.WriteActivityAsync(productId, userId, "item.created", "WorkItem", workItemId, null, CancellationToken.None);
        await _service.WriteActivityAsync(productId, userId, "item.created", "WorkItem", otherItemId, null, CancellationToken.None);

        var activities = await _service.GetActivitiesByWorkItemAsync(workItemId, 0, 50, CancellationToken.None);

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual(workItemId, activities[0].EntityId);
    }
}
