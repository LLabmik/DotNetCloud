using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Assignment display-name resolution on the work-item payloads the framed UI renders — the
/// assignee chip showed "?" because <see cref="WorkItemAssignmentDto.DisplayName"/> was never
/// populated (the assignment row holds only the user id).
/// </summary>
[TestClass]
public class WorkItemAssignmentNamesTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Assignee = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string AssigneeName = "Ben Kimball";

    private TracksDbContext _db = null!;

    [TestInitialize]
    public void Setup() => _db = TestHelpers.CreateDb();

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task GetWorkItemAsync_ReturnsTheAssigneeDisplayName()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Owner);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, null, Owner);
        await TestHelpers.SeedAssignmentAsync(_db, item.Id, Assignee);

        var result = await CreateService(UserDirectoryReturning(Assignee, AssigneeName))
            .GetWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(1, result.Assignments.Count);
        Assert.AreEqual(Assignee, result.Assignments[0].UserId);
        Assert.AreEqual(AssigneeName, result.Assignments[0].DisplayName);
    }

    [TestMethod]
    public async Task GetWorkItemAsync_WithoutAUserDirectory_LeavesTheDisplayNameBlank()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Owner);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, null, Owner);
        await TestHelpers.SeedAssignmentAsync(_db, item.Id, Assignee);

        var result = await CreateService(userDirectory: null)
            .GetWorkItemAsync(item.Id, CancellationToken.None);

        Assert.AreEqual(Assignee, result.Assignments[0].UserId);
        Assert.IsNull(result.Assignments[0].DisplayName);
    }

    [TestMethod]
    public async Task GetWorkItemsBySwimlaneAsync_IncludesTheAssignmentsWithTheirNames()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Owner);
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Owner);
        await TestHelpers.SeedAssignmentAsync(_db, item.Id, Assignee);

        var result = await CreateService(UserDirectoryReturning(Assignee, AssigneeName))
            .GetWorkItemsBySwimlaneAsync(swimlane.Id, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(1, result[0].Assignments.Count);
        Assert.AreEqual(AssigneeName, result[0].Assignments[0].DisplayName);
    }

    [TestMethod]
    public async Task AssignUserAsync_ReturnsTheAssigneeDisplayName()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Owner);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, null, Owner);

        var result = await CreateService(UserDirectoryReturning(Assignee, AssigneeName))
            .AssignUserAsync(item.Id, Assignee, CancellationToken.None);

        Assert.AreEqual(Assignee, result.UserId);
        Assert.AreEqual(AssigneeName, result.DisplayName);
    }

    private WorkItemService CreateService(IUserDirectory? userDirectory)
    {
        var userDirMock = new Mock<IUserDirectory>();
        userDirMock
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var activityService = new ActivityService(_db, userDirMock.Object);

        return new WorkItemService(
            _db,
            new SwimlaneTransitionService(_db),
            Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
            activityService,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            TestHelpers.CreateServiceProvider(userDirectory));
    }

    private static IUserDirectory UserDirectoryReturning(Guid userId, string displayName)
    {
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [userId] = displayName });
        return directory.Object;
    }
}
