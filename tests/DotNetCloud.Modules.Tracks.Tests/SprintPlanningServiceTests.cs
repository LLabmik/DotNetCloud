using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class SprintPlanningServiceTests
{
    private TracksDbContext _db = null!;
    private SprintPlanningService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new SprintPlanningService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<WorkItem> SeedTestEpic()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        return await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
    }

    [TestMethod]
    public async Task CreateSprintPlanAsync_CreatesSprints()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintPlanDto
        {
            NumberOfSprints = 4,
            SprintDurationWeeks = 2,
            StartDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("Sprint 1", result[0].Title);
        Assert.AreEqual(1, result[0].PlannedOrder);
    }

    [TestMethod]
    public async Task CreateSprintPlanAsync_SprintsHaveSequentialDates()
    {
        var epic = await SeedTestEpic();
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateSprintPlanDto
        {
            NumberOfSprints = 3,
            SprintDurationWeeks = 2,
            StartDate = start
        };

        var result = await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        Assert.AreEqual(start, result[0].StartDate);
        Assert.AreEqual(start.AddDays(13), result[0].EndDate);
        Assert.AreEqual(start.AddDays(14), result[1].StartDate);
    }

    [TestMethod]
    public async Task CreateSprintPlanAsync_SprintsHavePlanningStatus()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintPlanDto
        {
            NumberOfSprints = 3,
            SprintDurationWeeks = 2,
            StartDate = DateTime.UtcNow
        };

        var result = await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        Assert.IsTrue(result.All(s => s.Status == SprintStatus.Planning));
    }

    [TestMethod]
    public async Task GetSprintPlanAsync_ReturnsSprintsForEpic()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintPlanDto { NumberOfSprints = 4, SprintDurationWeeks = 2 };
        await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        var result = await _service.GetSprintPlanAsync(epic.Id, CancellationToken.None);

        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public async Task GetSprintPlanAsync_EmptyEpic_ReturnsEmptyList()
    {
        var epic = await SeedTestEpic();

        var result = await _service.GetSprintPlanAsync(epic.Id, CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task AdjustSprintDatesAsync_ChangesDuration()
    {
        var epic = await SeedTestEpic();
        var plan = await _service.CreateSprintPlanAsync(epic.Id,
            new CreateSprintPlanDto { NumberOfSprints = 3, SprintDurationWeeks = 2 }, CancellationToken.None);
        var sprintId = plan[0].Id;

        var result = await _service.AdjustSprintDatesAsync(sprintId,
            new AdjustSprintDto { DurationWeeks = 3 }, CancellationToken.None);

        Assert.AreEqual(3, result.DurationWeeks);
    }

    [TestMethod]
    public async Task AdjustSprintDatesAsync_CascadesToSubsequentSprints()
    {
        var epic = await SeedTestEpic();
        var start = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var plan = await _service.CreateSprintPlanAsync(epic.Id,
            new CreateSprintPlanDto { NumberOfSprints = 3, SprintDurationWeeks = 2, StartDate = start }, CancellationToken.None);

        await _service.AdjustSprintDatesAsync(plan[0].Id,
            new AdjustSprintDto { DurationWeeks = 3 }, CancellationToken.None);

        // Re-read to verify cascade
        var updated = await _service.GetSprintPlanAsync(epic.Id, CancellationToken.None);
        Assert.AreEqual(start, updated[0].StartDate);
        Assert.AreEqual(start.AddDays(20), updated[0].EndDate);
        Assert.AreEqual(start.AddDays(21), updated[1].StartDate);
    }

    [TestMethod]
    public async Task CreateSprintPlanAsync_MinDuration_Succeeds()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintPlanDto { NumberOfSprints = 1, SprintDurationWeeks = 1 };

        var result = await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public async Task CreateSprintPlanAsync_MaxDuration_Succeeds()
    {
        var epic = await SeedTestEpic();
        var dto = new CreateSprintPlanDto { NumberOfSprints = 1, SprintDurationWeeks = 16 };

        var result = await _service.CreateSprintPlanAsync(epic.Id, dto, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
    }
}
