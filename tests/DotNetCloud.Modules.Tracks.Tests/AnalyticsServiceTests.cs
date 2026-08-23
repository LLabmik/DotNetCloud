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
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.CreateVersion7(), "Task 1");

        var result = await _service.GetProductAnalyticsAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalItems);
    }

    [TestMethod]
    public async Task GetProductAnalyticsAsync_EmptyProduct_ReturnsZeros()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetProductAnalyticsAsync(product.Id, CancellationToken.None);

        Assert.AreEqual(0, result.TotalItems);
        Assert.AreEqual(0, result.TotalEpics);
        Assert.AreEqual(0, result.TotalFeatures);
        Assert.AreEqual(0, result.ItemsCompletedThisWeek);
        Assert.AreEqual(0, result.ActiveSprints);
        Assert.AreEqual(0, result.AvgCycleTimeDays);
        Assert.IsNotNull(result.DailyCompletions);
    }

    [TestMethod]
    public async Task GetProductAnalyticsAsync_WithCompletions_PopulatesDailyCompletions()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.CreateVersion7(), "Task 1");

        var result = await _service.GetProductAnalyticsAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result.DailyCompletions);
        Assert.IsTrue(result.DailyCompletions.Count > 0, "Should have daily completion entries");
    }

    [TestMethod]
    public async Task GetVelocityDataAsync_ReturnsVelocityList()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetVelocityDataAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetProductDashboardAsync_ReturnsDashboard()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetProductDashboardAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(product.Id, result.ProductId);
        Assert.IsNotNull(result.StatusBreakdown);
        Assert.IsNotNull(result.PriorityBreakdown);
        Assert.IsNotNull(result.Workload);
        Assert.IsNotNull(result.RecentlyUpdated);
        Assert.IsNotNull(result.UpcomingDueDates);
    }

    [TestMethod]
    public async Task GetProductDashboardAsync_WithWorkItems_PopulatesBreakdowns()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var todoLane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id, SwimlaneContainerType.Product, "To Do");
        // Add a done swimlane for completion calculations
        var doneLane = new Swimlane { ContainerId = product.Id, ContainerType = SwimlaneContainerType.Product, Title = "Done", IsDone = true, Position = 2000 };
        _db.Swimlanes.Add(doneLane);
        await _db.SaveChangesAsync();

        // Create work items with all properties set before saving
        var item1 = new WorkItem { ProductId = product.Id, SwimlaneId = todoLane.Id, Title = "High Pri", Type = WorkItemType.Feature, Priority = Priority.High, StoryPoints = 8, DueDate = DateTime.UtcNow.AddDays(3), CreatedByUserId = userId, Position = 1000 };
        item1.Assignments!.Add(new WorkItemAssignment { WorkItemId = item1.Id, UserId = userId });
        _db.WorkItems.Add(item1);

        var item2 = new WorkItem { ProductId = product.Id, SwimlaneId = todoLane.Id, Title = "Medium Pri", Type = WorkItemType.Item, Priority = Priority.Medium, StoryPoints = 3, CreatedByUserId = userId, Position = 2000 };
        item2.Assignments!.Add(new WorkItemAssignment { WorkItemId = item2.Id, UserId = userId });
        _db.WorkItems.Add(item2);
        await _db.SaveChangesAsync();

        var result = await _service.GetProductDashboardAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalItems);
        Assert.AreEqual(1, result.TotalFeatures);
        Assert.IsTrue(result.StatusBreakdown.Count > 0, "Should have status breakdown entries");
        Assert.IsTrue(result.PriorityBreakdown.Count > 0, "Should have priority breakdown entries");
        Assert.IsTrue(result.Workload.Count > 0, "Should have workload entries");
        Assert.AreEqual(11, result.Workload[0].TotalStoryPoints);
        Assert.IsTrue(result.UpcomingDueDates.Count > 0, "Should have upcoming due dates");
    }

    [TestMethod]
    public async Task GetRoadmapDataAsync_ReturnsRoadmap()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetRoadmapDataAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(product.Id, result.ProductId);
        Assert.IsNotNull(result.Items);
        Assert.IsNotNull(result.Milestones);
    }

    [TestMethod]
    public async Task GetRoadmapDataAsync_WithTimelineItems_ReturnsItemsWithFields()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var swimlane = new Swimlane { ContainerId = product.Id, ContainerType = SwimlaneContainerType.Product, Title = "To Do", Color = "#3b82f6", Position = 1000 };
        _db.Swimlanes.Add(swimlane);
        await _db.SaveChangesAsync();

        // Create a milestone first
        var milestone = new DotNetCloud.Modules.Tracks.Models.Milestone
        {
            ProductId = product.Id,
            Title = "Beta Release",
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = MilestoneStatus.Upcoming,
            Color = "#f59e0b"
        };
        _db.Milestones.Add(milestone);
        await _db.SaveChangesAsync();

        // Create an epic with dates (roadmap-eligible)
        var epic = new WorkItem { ProductId = product.Id, SwimlaneId = swimlane.Id, Title = "Q3 Launch", Type = WorkItemType.Epic, Priority = Priority.High, StartDate = DateTime.UtcNow.AddDays(1), DueDate = DateTime.UtcNow.AddDays(60), MilestoneId = milestone.Id, CreatedByUserId = userId, Position = 1000 };
        epic.Assignments!.Add(new WorkItemAssignment { WorkItemId = epic.Id, UserId = userId });
        _db.WorkItems.Add(epic);

        // Create a feature with dates
        var feature = new WorkItem { ProductId = product.Id, SwimlaneId = swimlane.Id, Title = "Login Flow", Type = WorkItemType.Feature, Priority = Priority.Urgent, StartDate = DateTime.UtcNow.AddDays(5), DueDate = DateTime.UtcNow.AddDays(20), CreatedByUserId = userId, Position = 2000 };
        _db.WorkItems.Add(feature);
        await _db.SaveChangesAsync();

        var result = await _service.GetRoadmapDataAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(product.Id, result.ProductId);
        Assert.IsTrue(result.Items.Count >= 2, $"Should have at least 2 roadmap items, got {result.Items.Count}");
        Assert.IsTrue(result.Milestones.Count >= 1, $"Should have at least 1 milestone, got {result.Milestones.Count}");

        // Verify item fields
        var epicItem = result.Items.FirstOrDefault(i => i.Type == WorkItemType.Epic);
        Assert.IsNotNull(epicItem, "Should contain the epic");
        Assert.AreEqual("Q3 Launch", epicItem!.Title);
        Assert.IsNotNull(epicItem.StartDate);
        Assert.IsNotNull(epicItem.DueDate);
        Assert.AreEqual(milestone.Id, epicItem.MilestoneId);
        Assert.AreEqual("Beta Release", epicItem.MilestoneTitle);
        Assert.IsNotNull(epicItem.AssigneeUserId);
        Assert.IsNotNull(epicItem.SwimlaneColor);

        // Verify milestone fields
        var ms = result.Milestones.First();
        Assert.AreEqual("Beta Release", ms.Title);
        Assert.AreEqual(MilestoneStatus.Upcoming, ms.Status);
        Assert.AreEqual("#f59e0b", ms.Color);
    }

    [TestMethod]
    public async Task GetSprintCapacityAsync_ReturnsCapacity()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.CreateVersion7());
        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint 1",
            Status = SprintStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1,
            TargetStoryPoints = 21
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        var result = await _service.GetSprintCapacityAsync(sprint.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(sprint.Id, result.SprintId);
        Assert.AreEqual("Sprint 1", result.SprintTitle);
        Assert.AreEqual(21, result.TargetStoryPoints);
        Assert.AreEqual(0, result.TotalStoryPoints);
        Assert.AreEqual(0, result.CompletedStoryPoints);
    }

    [TestMethod]
    public async Task GetSprintCapacityAsync_WithItems_CalculatesStoryPoints()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, userId);
        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint 1",
            Status = SprintStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1,
            TargetStoryPoints = 30
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, userId, "Task 1");
        item.StoryPoints = 5;
        _db.SprintItems.Add(new SprintItem { SprintId = sprint.Id, ItemId = item.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetSprintCapacityAsync(sprint.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.TotalStoryPoints);
        Assert.AreEqual(30, result.TargetStoryPoints);
    }

    [TestMethod]
    public async Task GetMemberCapacityAsync_ReturnsMemberCapacities()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetMemberCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetMemberCapacityAsync_WithAssignments_CalculatesCapacityPercent()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, userId);

        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Active Sprint",
            Status = SprintStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        // Create items with properties set before saving
        var item1 = new WorkItem { ProductId = product.Id, SwimlaneId = swimlane.Id, Title = "Task A", Type = WorkItemType.Item, StoryPoints = 8, CreatedByUserId = userId, Position = 1000 };
        item1.Assignments!.Add(new WorkItemAssignment { WorkItemId = item1.Id, UserId = userId });
        _db.WorkItems.Add(item1);

        var item2 = new WorkItem { ProductId = product.Id, SwimlaneId = swimlane.Id, Title = "Task B", Type = WorkItemType.Item, StoryPoints = 5, CreatedByUserId = userId, Position = 2000 };
        item2.Assignments!.Add(new WorkItemAssignment { WorkItemId = item2.Id, UserId = userId });
        _db.WorkItems.Add(item2);
        await _db.SaveChangesAsync();

        _db.SprintItems.Add(new SprintItem { SprintId = sprint.Id, ItemId = item1.Id });
        _db.SprintItems.Add(new SprintItem { SprintId = sprint.Id, ItemId = item2.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetMemberCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0, "Should have member capacity entries");
        var member = result.First();
        Assert.AreEqual(13, member.AssignedStoryPoints);
        Assert.AreEqual(2, member.AssignedItemCount);
        Assert.IsTrue(member.CapacityPercent > 0, "Capacity percent should be positive");
        Assert.IsTrue(member.SprintTitles.Contains("Active Sprint"));
    }

    [TestMethod]
    public async Task GetProductCapacityAsync_ReturnsProductCapacity()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());

        var result = await _service.GetProductCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(product.Id, result.ProductId);
        Assert.IsNotNull(result.Members);
        Assert.AreEqual(0, result.TotalAssignedStoryPoints);
        Assert.AreEqual(0, result.TotalMembers);
        Assert.AreEqual(0, result.OverloadedMembers);
    }

    [TestMethod]
    public async Task GetProductCapacityAsync_WithMembers_CalculatesOverloaded()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, userId);

        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint X",
            Status = SprintStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            DurationWeeks = 2,
            PlannedOrder = 1
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();

        // Create item with properties set before saving
        var item = new WorkItem { ProductId = product.Id, SwimlaneId = swimlane.Id, Title = "Big Task", Type = WorkItemType.Item, StoryPoints = 25, CreatedByUserId = userId, Position = 1000 };
        item.Assignments!.Add(new WorkItemAssignment { WorkItemId = item.Id, UserId = userId });
        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync();

        _db.SprintItems.Add(new SprintItem { SprintId = sprint.Id, ItemId = item.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetProductCapacityAsync(product.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalMembers);
        Assert.AreEqual(25, result.TotalAssignedStoryPoints);
        Assert.AreEqual(1, result.OverloadedMembers, "Member with 125% capacity should be overloaded");
        var member = result.Members.First();
        Assert.AreEqual(25, member.AssignedStoryPoints);
        Assert.IsTrue(member.CapacityPercent > 100, "Capacity should exceed 100%");
    }
}
