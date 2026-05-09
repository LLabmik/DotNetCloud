using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ChecklistServiceTests
{
    private TracksDbContext _db = null!;
    private ChecklistService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new ChecklistService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateChecklistAsync_CreatesChecklist()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var dto = new CreateChecklistDto { Title = "Acceptance Criteria" };

        var result = await _service.CreateChecklistAsync(item.Id, dto, CancellationToken.None);

        Assert.AreEqual("Acceptance Criteria", result.Title);
        Assert.AreEqual(item.Id, result.ItemId);
    }

    [TestMethod]
    public async Task GetChecklistsByItemAsync_ReturnsChecklists()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Checklist A" }, CancellationToken.None);
        await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Checklist B" }, CancellationToken.None);

        var result = await _service.GetChecklistsByItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task DeleteChecklistAsync_RemovesChecklist()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var checklist = await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "To Delete" }, CancellationToken.None);

        await _service.DeleteChecklistAsync(checklist.Id, CancellationToken.None);

        var result = await _service.GetChecklistsByItemAsync(item.Id, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task AddChecklistItemAsync_AddsItemToChecklist()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var checklist = await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Tasks" }, CancellationToken.None);

        var result = await _service.AddChecklistItemAsync(checklist.Id, new AddChecklistItemDto { Title = "Do the thing" }, CancellationToken.None);

        Assert.AreEqual("Do the thing", result.Title);
        Assert.IsFalse(result.IsCompleted);
    }

    [TestMethod]
    public async Task ToggleChecklistItemAsync_TogglesCompletion()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var checklist = await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Tasks" }, CancellationToken.None);
        var checkItem = await _service.AddChecklistItemAsync(checklist.Id, new AddChecklistItemDto { Title = "Step 1" }, CancellationToken.None);

        await _service.ToggleChecklistItemAsync(checkItem.Id, CancellationToken.None);

        var updated = await _service.GetChecklistsByItemAsync(item.Id, CancellationToken.None);
        Assert.IsTrue(updated.First().Items.First(i => i.Id == checkItem.Id).IsCompleted);
    }

    [TestMethod]
    public async Task ToggleChecklistItemAsync_Twice_ReturnsToUnchecked()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var checklist = await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Tasks" }, CancellationToken.None);
        var checkItem = await _service.AddChecklistItemAsync(checklist.Id, new AddChecklistItemDto { Title = "Step 1" }, CancellationToken.None);
        await _service.ToggleChecklistItemAsync(checkItem.Id, CancellationToken.None);

        await _service.ToggleChecklistItemAsync(checkItem.Id, CancellationToken.None);

        var updated = await _service.GetChecklistsByItemAsync(item.Id, CancellationToken.None);
        Assert.IsFalse(updated.First().Items.First(i => i.Id == checkItem.Id).IsCompleted);
    }

    [TestMethod]
    public async Task DeleteChecklistItemAsync_RemovesItem()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var checklist = await _service.CreateChecklistAsync(item.Id, new CreateChecklistDto { Title = "Tasks" }, CancellationToken.None);
        var checkItem = await _service.AddChecklistItemAsync(checklist.Id, new AddChecklistItemDto { Title = "Step 1" }, CancellationToken.None);

        await _service.DeleteChecklistItemAsync(checkItem.Id, CancellationToken.None);

        var result = await _service.GetChecklistsByItemAsync(item.Id, CancellationToken.None);
        Assert.AreEqual(0, result.First().Items.Count);
    }
}
