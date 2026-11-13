using System.Security.Claims;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests.Host;

/// <summary>
/// Tests for <see cref="WorkItemsController"/> — specifically that the work-item payload the framed
/// UI renders carries the assignee's display name instead of a raw user id.
/// </summary>
[TestClass]
public class WorkItemsControllerTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Assignee = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [TestMethod]
    public async Task ListProductWorkItems_ReturnsTheAssigneeDisplayName()
    {
        await using var db = TestHelpers.CreateDb();
        var product = await TestHelpers.SeedProductAsync(db, Guid.CreateVersion7(), Owner);
        var item = await TestHelpers.SeedWorkItemAsync(db, product.Id, null, Owner, "Widget smoke item");
        await TestHelpers.SeedAssignmentAsync(db, item.Id, Assignee);

        var controller = CreateController(db, UserDirectoryReturning("Ben Kimball"));

        var result = await controller.ListProductWorkItemsAsync(product.Id, ct: CancellationToken.None);

        var items = ReadData<WorkItemDto>(result);
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(1, items[0].Assignments.Count);
        Assert.AreEqual(Assignee, items[0].Assignments[0].UserId);
        Assert.AreEqual("Ben Kimball", items[0].Assignments[0].DisplayName);
    }

    [TestMethod]
    public async Task ListProductWorkItems_WithoutAUserDirectory_StillAnswersWithTheAssignment()
    {
        await using var db = TestHelpers.CreateDb();
        var product = await TestHelpers.SeedProductAsync(db, Guid.CreateVersion7(), Owner);
        var item = await TestHelpers.SeedWorkItemAsync(db, product.Id, null, Owner, "Widget smoke item");
        await TestHelpers.SeedAssignmentAsync(db, item.Id, Assignee);

        var controller = CreateController(db, userDirectory: null);

        var result = await controller.ListProductWorkItemsAsync(product.Id, ct: CancellationToken.None);

        var items = ReadData<WorkItemDto>(result);
        Assert.AreEqual(Assignee, items[0].Assignments[0].UserId);
        Assert.IsNull(items[0].Assignments[0].DisplayName);
    }

    private static WorkItemsController CreateController(TracksDbContext db, IUserDirectory? userDirectory)
    {
        var directoryMock = new Mock<IUserDirectory>();
        directoryMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var activityService = new ActivityService(db, directoryMock.Object);
        var workItemService = new WorkItemService(
            db,
            new SwimlaneTransitionService(db),
            Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
            activityService,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            TestHelpers.CreateServiceProvider(userDirectory));

        var controller = new WorkItemsController(
            workItemService,
            new ProductService(db),
            activityService,
            Mock.Of<IFileValidationService>(),
            db,
            NullLogger<WorkItemsController>.Instance,
            TestHelpers.CreateServiceProvider(userDirectory));

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Owner.ToString())],
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

    private static IUserDirectory UserDirectoryReturning(string displayName)
    {
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Assignee] = displayName });
        return directory.Object;
    }

    /// <summary>Reads the <c>data</c> member of the standard <c>{ success, data }</c> envelope.</summary>
    private static List<T> ReadData<T>(IActionResult result)
    {
        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok, $"Expected an OkObjectResult, got {result.GetType().Name}.");

        var json = JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);

        return JsonSerializer.Deserialize<List<T>>(
            document.RootElement.GetProperty("data").GetRawText(),
            JsonSerializerOptions.Web) ?? [];
    }
}
