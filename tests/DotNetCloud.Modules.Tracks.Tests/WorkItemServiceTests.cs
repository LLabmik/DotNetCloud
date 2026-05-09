using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class WorkItemServiceTests
{
    private TracksDbContext _db = null!;
    private WorkItemService _service = null!;
    private SwimlaneTransitionService _transitionService = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _transitionService = new SwimlaneTransitionService(_db);
        _service = new WorkItemService(_db, _transitionService);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateWorkItemAsync_CreatesItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
        var feature = await TestHelpers.SeedWorkItemAsync(_db, product.Id, null, Guid.NewGuid(), "Feature", WorkItemType.Feature);
        feature.ParentWorkItemId = epic.Id;
        await _db.SaveChangesAsync();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, feature.Id, SwimlaneContainerType.WorkItem);
        var dto = new CreateWorkItemDto { Title = "New Task" };

        var result = await _service.CreateWorkItemAsync(product.Id, swimlane.Id, WorkItemType.Item, Guid.NewGuid(), dto, CancellationToken.None);

        Assert.AreEqual("New Task", result.Title);
        Assert.AreEqual(swimlane.Id, result.SwimlaneId);
        Assert.AreEqual(WorkItemType.Item, result.Type);
    }

    [TestMethod]
    public async Task CreateWorkItemAsync_EpicType_Works()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var dto = new CreateWorkItemDto { Title = "Epic Goal" };

        var result = await _service.CreateWorkItemAsync(product.Id, swimlane.Id, WorkItemType.Epic, Guid.NewGuid(), dto, CancellationToken.None);

        Assert.AreEqual(WorkItemType.Epic, result.Type);
    }

    [TestMethod]
    public async Task GetWorkItemAsync_ReturnsItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());

        var result = await _service.GetWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(item.Id, result.Id);
        Assert.AreEqual(item.Title, result.Title);
    }

    [TestMethod]
    public async Task GetWorkItemByNumberAsync_ReturnsItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());

        var result = await _service.GetWorkItemByNumberAsync(product.Id, item.ItemNumber, CancellationToken.None);

        Assert.AreEqual(item.Id, result.Id);
    }

    [TestMethod]
    public async Task UpdateWorkItemAsync_UpdatesTitle()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var dto = new UpdateWorkItemDto { Title = "Updated Title" };

        var result = await _service.UpdateWorkItemAsync(item.Id, dto, CancellationToken.None);

        Assert.AreEqual("Updated Title", result.Title);
    }

    [TestMethod]
    public async Task DeleteWorkItemAsync_SoftDeletes()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());

        await _service.DeleteWorkItemAsync(item.Id, CancellationToken.None);

        var deleted = await _db.WorkItems.IgnoreQueryFilters().FirstOrDefaultAsync(wi => wi.Id == item.Id);
        Assert.IsNotNull(deleted);
        Assert.IsTrue(deleted.IsDeleted);
    }

    [TestMethod]
    public async Task MoveWorkItemAsync_MovesBetweenSwimlanes()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var source = await TestHelpers.SeedSwimlaneAsync(_db, product.Id, SwimlaneContainerType.Product, "Source");
        var target = await TestHelpers.SeedSwimlaneAsync(_db, product.Id, SwimlaneContainerType.Product, "Target");
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, source.Id, Guid.NewGuid());
        var dto = new MoveWorkItemDto { TargetSwimlaneId = target.Id };

        var result = await _service.MoveWorkItemAsync(item.Id, dto, CancellationToken.None);

        Assert.AreEqual(target.Id, result.SwimlaneId);
    }

    [TestMethod]
    public async Task GetWorkItemsBySwimlaneAsync_ReturnsItemsInSwimlane()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "A");
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "B");

        var result = await _service.GetWorkItemsBySwimlaneAsync(swimlane.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetChildWorkItemsAsync_ReturnsChildren()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var parent = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Parent", WorkItemType.Feature);
        var child = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Child", WorkItemType.Item);
        child.ParentWorkItemId = parent.Id;
        await _db.SaveChangesAsync();

        var result = await _service.GetChildWorkItemsAsync(parent.Id, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(child.Id, result[0].Id);
    }

    [TestMethod]
    public async Task AssignUserAsync_AddsAssignment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();

        var result = await _service.AssignUserAsync(item.Id, userId, CancellationToken.None);

        Assert.AreEqual(userId, result.UserId);
    }

    [TestMethod]
    public async Task RemoveAssignmentAsync_RemovesAssignment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.AssignUserAsync(item.Id, userId, CancellationToken.None);

        await _service.RemoveAssignmentAsync(item.Id, userId, CancellationToken.None);

        var item2 = await _service.GetWorkItemAsync(item.Id, CancellationToken.None);
        Assert.AreEqual(0, item2.Assignments.Count);
    }

    [TestMethod]
    public async Task AddLabelAsync_AddsLabelToWorkItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var label = new Label { ProductId = product.Id, Title = "Bug", Color = "#f00" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();

        await _service.AddLabelAsync(item.Id, label.Id, CancellationToken.None);

        var wl = await _db.WorkItemLabels.FirstOrDefaultAsync(wl => wl.WorkItemId == item.Id && wl.LabelId == label.Id);
        Assert.IsNotNull(wl);
    }

    [TestMethod]
    public async Task RemoveLabelAsync_RemovesLabelFromWorkItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var label = new Label { ProductId = product.Id, Title = "Bug", Color = "#f00" };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();
        await _service.AddLabelAsync(item.Id, label.Id, CancellationToken.None);

        await _service.RemoveLabelAsync(item.Id, label.Id, CancellationToken.None);

        var wl = await _db.WorkItemLabels.FirstOrDefaultAsync(wl => wl.WorkItemId == item.Id && wl.LabelId == label.Id);
        Assert.IsNull(wl);
    }
}
