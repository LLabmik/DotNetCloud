using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="WorkItemService.GetMyUpcomingDueItemsAsync"/>.
/// </summary>
[TestClass]
public class WorkItemServiceGetMyUpcomingDueItemsTests
{
    private TracksDbContext _db = null!;
    private WorkItemService _service = null!;
    private SwimlaneTransitionService _transitionService = null!;
    private Mock<DotNetCloud.Core.Events.IEventBus> _eventBusMock = null!;
    private ActivityService _activityService = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _transitionService = new SwimlaneTransitionService(_db);
        _eventBusMock = new Mock<DotNetCloud.Core.Events.IEventBus>();
        var userDirMock = new Mock<IUserDirectory>();
        userDirMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _activityService = new ActivityService(_db, userDirMock.Object);
        _service = new WorkItemService(_db, _transitionService, _eventBusMock.Object, _activityService, Mock.Of<IAuditLogger>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<WorkItem> SeedUpcomingItemAsync(
        Guid productId,
        Guid? swimlaneId,
        string title,
        DateTime? dueDate,
        Guid? assigneeId = null,
        Guid? watcherId = null,
        bool isArchived = false,
        bool isDeleted = false)
    {
        var item = new WorkItem
        {
            ProductId = productId,
            SwimlaneId = swimlaneId,
            Type = WorkItemType.Item,
            Title = title,
            Position = 1000.0,
            CreatedByUserId = Guid.CreateVersion7(),
            DueDate = dueDate,
            IsArchived = isArchived,
            IsDeleted = isDeleted
        };
        if (assigneeId.HasValue)
            item.Assignments.Add(new WorkItemAssignment { UserId = assigneeId.Value });
        if (watcherId.HasValue)
            item.Watchers.Add(new WorkItemWatcher { UserId = watcherId.Value });
        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_AssignedOrWatched_ReturnsItemsOrderedByDueDateAscending()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Alpha", now.AddDays(3), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Beta", now.AddDays(1), watcherId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Gamma", now.AddDays(2), assigneeId: user);

        var result = await _service.GetMyUpcomingDueItemsAsync(user);

        CollectionAssert.AreEqual(
            new[] { "Beta", "Gamma", "Alpha" },
            result.Select(i => i.Title).ToArray());
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_PastOrBeyondHorizon_ExcludesItems()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "InWindow", now.AddDays(2), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Overdue", now.AddDays(-2), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "TooFarOut", now.AddDays(30), assigneeId: user);

        var result = await _service.GetMyUpcomingDueItemsAsync(user);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("InWindow", result[0].Title);
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_DoneSwimlane_ExcludesItems()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var activeSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id, SwimlaneContainerType.Product, "Active");
        var doneSwimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id, SwimlaneContainerType.Product, "Done");
        doneSwimlane.IsDone = true;
        await _db.SaveChangesAsync();
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await SeedUpcomingItemAsync(product.Id, activeSwimlane.Id, "Doing", now.AddDays(2), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, doneSwimlane.Id, "Finished", now.AddDays(2), assigneeId: user);

        var result = await _service.GetMyUpcomingDueItemsAsync(user);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Doing", result[0].Title);
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_ArchivedOrDeleted_ExcludesItems()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Live", now.AddDays(2), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Archived", now.AddDays(2), assigneeId: user, isArchived: true);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "Deleted", now.AddDays(2), assigneeId: user, isDeleted: true);

        var result = await _service.GetMyUpcomingDueItemsAsync(user);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Live", result[0].Title);
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_RespectsCount_ReturnsOnlyRequestedNumberOfEarliest()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "DueSoonest", now.AddDays(1), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "DueSecond", now.AddDays(2), assigneeId: user);
        await SeedUpcomingItemAsync(product.Id, swimlane.Id, "DueLatest", now.AddDays(3), assigneeId: user);

        var result = await _service.GetMyUpcomingDueItemsAsync(user, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "DueSoonest", "DueSecond" },
            result.Select(i => i.Title).ToArray());
    }

    [TestMethod]
    public async Task GetMyUpcomingDueItems_NoItems_ReturnsEmptyList()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var user = Guid.CreateVersion7();

        var result = await _service.GetMyUpcomingDueItemsAsync(user);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
