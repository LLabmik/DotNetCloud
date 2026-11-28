using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests.Host;

/// <summary>
/// Tests for <see cref="WidgetController"/> — the assertion-scoped read behind the manifest-declared
/// Home-page widget (manifest v2 <c>ui.widget.path</c>).
/// </summary>
[TestClass]
public class WidgetControllerTests
{
    private static readonly Guid Caller = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private TracksDbContext _db = null!;
    private WorkItemService _workItems = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();

        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        _workItems = new WorkItemService(
            _db,
            new SwimlaneTransitionService(_db),
            Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
            new ActivityService(_db, userDirectory.Object),
            Mock.Of<IAuditLogger>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private static WidgetController CreateController(WorkItemService workItems, Guid? callerId = null)
    {
        var controller = new WidgetController(workItems);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, (callerId ?? Caller).ToString())],
            "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return controller;
    }

    private async Task<Guid> SeedAssignedItemAsync(Guid? assigneeId, string title, DateTime dueDate)
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);

        var item = new WorkItem
        {
            ProductId = product.Id,
            SwimlaneId = swimlane.Id,
            Type = WorkItemType.Item,
            Title = title,
            Position = 1000.0,
            CreatedByUserId = Guid.CreateVersion7(),
            DueDate = dueDate,
        };

        if (assigneeId.HasValue)
        {
            item.Assignments.Add(new WorkItemAssignment { UserId = assigneeId.Value });
        }

        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync();

        return item.Id;
    }

    [TestMethod]
    public async Task GetUpcomingAsync_ReturnsOnlyTheActingUsersItems()
    {
        var mine = await SeedAssignedItemAsync(Caller, "Mine", DateTime.UtcNow.AddDays(2));
        await SeedAssignedItemAsync(Guid.CreateVersion7(), "SomeoneElse", DateTime.UtcNow.AddDays(1));

        var controller = CreateController(_workItems);

        var result = await controller.GetUpcomingAsync(5, CancellationToken.None) as OkObjectResult;

        Assert.IsNotNull(result);
        var payload = ReadItems(result.Value);
        Assert.AreEqual(1, payload.Count);
        Assert.AreEqual(mine, payload[0].Id);
        Assert.AreEqual("Mine", payload[0].Title);
    }

    [TestMethod]
    public async Task GetUpcomingAsync_WithoutAnAuthenticatedCaller_ThrowsUnauthorized()
    {
        var controller = new WidgetController(_workItems)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(
            () => controller.GetUpcomingAsync(5, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetUpcomingAsync_TakeOutOfRange_IsClampedToOne()
    {
        await SeedAssignedItemAsync(Caller, "First", DateTime.UtcNow.AddDays(1));
        await SeedAssignedItemAsync(Caller, "Second", DateTime.UtcNow.AddDays(2));

        var controller = CreateController(_workItems);

        var result = await controller.GetUpcomingAsync(0, CancellationToken.None) as OkObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(1, ReadItems(result.Value).Count);
    }

    [TestMethod]
    public async Task GetUpcomingAsync_ReturnsTheFieldsTheWidgetRenders()
    {
        var id = await SeedAssignedItemAsync(Caller, "Rendered", DateTime.UtcNow.AddDays(3));

        var controller = CreateController(_workItems);

        var result = await controller.GetUpcomingAsync(5, CancellationToken.None) as OkObjectResult;

        Assert.IsNotNull(result);
        var payload = ReadItems(result.Value);
        Assert.AreEqual(1, payload.Count);
        Assert.AreEqual(id, payload[0].Id);
        Assert.IsFalse(string.IsNullOrEmpty(payload[0].Title));
        Assert.IsTrue(payload[0].DueDate.HasValue, "the widget shows the due date");
        Assert.IsFalse(string.IsNullOrEmpty(payload[0].Priority), "the widget colours its priority dot from this");
    }

    /// <summary>
    /// Reads the envelope's <c>data</c> array without depending on anonymous-type reflection at the
    /// call site: the response shape is the contract the widget's JavaScript consumes.
    /// </summary>
    private static List<WidgetItemView> ReadItems(object? envelope)
    {
        Assert.IsNotNull(envelope);

        var data = envelope.GetType().GetProperty("data")!.GetValue(envelope)
            as System.Collections.IEnumerable;
        Assert.IsNotNull(data);

        return data.Cast<object>()
            .Select(row => new WidgetItemView
            {
                Id = (Guid)row.GetType().GetProperty("id")!.GetValue(row)!,
                Title = (string)row.GetType().GetProperty("title")!.GetValue(row)!,
                DueDate = (DateTime?)row.GetType().GetProperty("dueDate")!.GetValue(row),
                Priority = (string)row.GetType().GetProperty("priority")!.GetValue(row)!,
            })
            .ToList();
    }

    private sealed class WidgetItemView
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public DateTime? DueDate { get; init; }

        public string Priority { get; init; } = string.Empty;
    }
}
