using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class SprintServiceTests
{
    private TracksDbContext _db = null!;
    private SprintService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new SprintService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<WorkItem> SeedTestEpic()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        return await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
    }

    [TestMethod]
    public async Task CreateSprintAsync_CreatesSprint()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintDto { Title = "Sprint 1", DurationWeeks = 2 };

        var result = await _service.CreateSprintAsync(epic.Id, dto, CancellationToken.None);

        Assert.AreEqual("Sprint 1", result.Title);
        Assert.AreEqual(epic.Id, result.EpicId);
    }

    [TestMethod]
    public async Task GetSprintAsync_ReturnsSprint()
    {
        var epic = await SeedTestEpic();
        var created = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Sprint 1" }, CancellationToken.None);

        var result = await _service.GetSprintAsync(created.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task GetSprintsByEpicAsync_ReturnsSprintsForEpic()
    {
        var epic = await SeedTestEpic();
        await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "A" }, CancellationToken.None);
        await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "B" }, CancellationToken.None);

        var result = await _service.GetSprintsByEpicAsync(epic.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task UpdateSprintAsync_UpdatesTitle()
    {
        var epic = await SeedTestEpic();
        var created = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Old" }, CancellationToken.None);
        var dto = new UpdateSprintDto { Title = "New" };

        var result = await _service.UpdateSprintAsync(created.Id, dto, CancellationToken.None);

        Assert.AreEqual("New", result.Title);
    }

    [TestMethod]
    public async Task DeleteSprintAsync_RemovesSprint()
    {
        var epic = await SeedTestEpic();
        var created = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "To Delete" }, CancellationToken.None);

        await _service.DeleteSprintAsync(created.Id, CancellationToken.None);

        var result = await _service.GetSprintAsync(created.Id, CancellationToken.None);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task StartSprintAsync_SetsStatusToActive()
    {
        var epic = await SeedTestEpic();
        var sprint = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) }, CancellationToken.None);

        var result = await _service.StartSprintAsync(sprint.Id, CancellationToken.None);

        Assert.AreEqual(SprintStatus.Active, result.Status);
    }

    [TestMethod]
    public async Task CompleteSprintAsync_SetsStatusToCompleted()
    {
        var epic = await SeedTestEpic();
        var sprint = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Sprint 1" }, CancellationToken.None);
        await _service.StartSprintAsync(sprint.Id, CancellationToken.None);

        var result = await _service.CompleteSprintAsync(sprint.Id, CancellationToken.None);

        Assert.AreEqual(SprintStatus.Completed, result.Status);
    }

    [TestMethod]
    public async Task AddItemToSprintAsync_AddsWorkItemToSprint()
    {
        var epic = await SeedTestEpic();
        var sprint = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Sprint 1" }, CancellationToken.None);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, epic.ProductId);
        var item = await TestHelpers.SeedWorkItemAsync(_db, epic.ProductId, swimlane.Id, Guid.NewGuid());

        await _service.AddItemToSprintAsync(sprint.Id, item.Id, CancellationToken.None);

        var sprintItems = await _db.SprintItems.Where(si => si.SprintId == sprint.Id).ToListAsync();
        Assert.AreEqual(1, sprintItems.Count);
        Assert.AreEqual(item.Id, sprintItems[0].ItemId);
    }

    [TestMethod]
    public async Task RemoveItemFromSprintAsync_RemovesWorkItemFromSprint()
    {
        var epic = await SeedTestEpic();
        var sprint = await _service.CreateSprintAsync(epic.Id, new CreateSprintDto { Title = "Sprint 1" }, CancellationToken.None);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, epic.ProductId);
        var item = await TestHelpers.SeedWorkItemAsync(_db, epic.ProductId, swimlane.Id, Guid.NewGuid());
        await _service.AddItemToSprintAsync(sprint.Id, item.Id, CancellationToken.None);

        await _service.RemoveItemFromSprintAsync(sprint.Id, item.Id, CancellationToken.None);

        var sprintItems = await _db.SprintItems.Where(si => si.SprintId == sprint.Id).ToListAsync();
        Assert.AreEqual(0, sprintItems.Count);
    }

    [TestMethod]
    public async Task GetBacklogItemsAsync_ReturnsItemsNotInSprint()
    {
        var epic = await SeedTestEpic();
        var feature = await TestHelpers.SeedWorkItemAsync(_db, epic.ProductId, null, Guid.NewGuid(), "Feature", WorkItemType.Feature);
        feature.ParentWorkItemId = epic.Id;
        await _db.SaveChangesAsync();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, feature.Id, SwimlaneContainerType.WorkItem);
        var item = await TestHelpers.SeedWorkItemAsync(_db, epic.ProductId, swimlane.Id, Guid.NewGuid(), "Child Item", WorkItemType.Item);
        item.ParentWorkItemId = feature.Id;
        await _db.SaveChangesAsync();

        var result = await _service.GetBacklogItemsAsync(epic.Id, CancellationToken.None);

        Assert.IsTrue(result.Any(i => i.Id == item.Id));
    }
}
