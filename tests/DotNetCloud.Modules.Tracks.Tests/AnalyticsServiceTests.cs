using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class AnalyticsServiceTests
{
    private TracksDbContext _db = null!;
    private AnalyticsService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new AnalyticsService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task GetProductAnalyticsAsync_ReturnsAnalyticsForProduct()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Task 1");

        var result = await _service.GetProductAnalyticsAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalItems);
    }

    [TestMethod]
    public async Task GetProductAnalyticsAsync_EmptyProduct_ReturnsZeros()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetProductAnalyticsAsync(product.Id, CancellationToken.None);

        Assert.AreEqual(0, result.TotalItems);
    }

    [TestMethod]
    public async Task GetVelocityDataAsync_ReturnsVelocityList()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetVelocityDataAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetProductDashboardAsync_ReturnsDashboard()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetProductDashboardAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetRoadmapDataAsync_ReturnsRoadmap()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetRoadmapDataAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetSprintCapacityAsync_ReturnsCapacity()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint 1",
            Status = SprintStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        var result = await _service.GetSprintCapacityAsync(sprint.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetMemberCapacityAsync_ReturnsMemberCapacities()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetMemberCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetProductCapacityAsync_ReturnsProductCapacity()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.GetProductCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }
}
