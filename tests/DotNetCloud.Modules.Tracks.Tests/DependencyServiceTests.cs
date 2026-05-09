using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class DependencyServiceTests
{
    private TracksDbContext _db = null!;
    private DependencyService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new DependencyService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task AddDependencyAsync_CreatesDependency()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var itemA = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item A");
        var itemB = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item B");
        var dto = new AddWorkItemDependencyDto { DependsOnWorkItemId = itemA.Id, Type = DependencyType.BlockedBy };

        var result = await _service.AddDependencyAsync(itemB.Id, dto, CancellationToken.None);

        Assert.AreEqual(itemB.Id, result.WorkItemId);
        Assert.AreEqual(itemA.Id, result.DependsOnWorkItemId);
        Assert.AreEqual(DependencyType.BlockedBy, result.Type);
    }

    [TestMethod]
    public async Task GetDependenciesByWorkItemAsync_ReturnsDependencies()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var itemA = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item A");
        var itemB = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item B");
        await _service.AddDependencyAsync(itemB.Id, new AddWorkItemDependencyDto { DependsOnWorkItemId = itemA.Id }, CancellationToken.None);

        // Get what itemA depends on (itemA → nothing)
        var deps = await _service.GetDependenciesByWorkItemAsync(itemA.Id, CancellationToken.None);
        Assert.AreEqual(0, deps.Count);

        // Get what itemB depends on (itemB → itemA)
        deps = await _service.GetDependenciesByWorkItemAsync(itemB.Id, CancellationToken.None);
        Assert.AreEqual(1, deps.Count);
        Assert.AreEqual(itemA.Id, deps[0].DependsOnWorkItemId);
    }

    [TestMethod]
    public async Task GetDependentsByWorkItemAsync_ReturnsDependents()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var itemA = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item A");
        var itemB = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item B");
        await _service.AddDependencyAsync(itemB.Id, new AddWorkItemDependencyDto { DependsOnWorkItemId = itemA.Id }, CancellationToken.None);

        var result = await _service.GetDependentsByWorkItemAsync(itemA.Id, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(itemB.Id, result[0].WorkItemId);
    }

    [TestMethod]
    public async Task RemoveDependencyAsync_DeletesDependency()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var itemA = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item A");
        var itemB = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Item B");
        var dep = await _service.AddDependencyAsync(itemB.Id, new AddWorkItemDependencyDto { DependsOnWorkItemId = itemA.Id }, CancellationToken.None);

        await _service.RemoveDependencyAsync(dep.Id, CancellationToken.None);

        var result = await _service.GetDependenciesByWorkItemAsync(itemB.Id, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetDependenciesByWorkItemAsync_NoDependencies_ReturnsEmptyList()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());

        var result = await _service.GetDependenciesByWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }
}
