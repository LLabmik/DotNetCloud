using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Tests for <see cref="UserDisplayNames"/> — the helper that resolves ids to names through the
/// core user directory (assignments, work-item payloads and any other id-only record).
/// </summary>
[TestClass]
public class UserDisplayNamesTests
{
    private static readonly Guid Assignee = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static WorkItemAssignmentDto Assignment(Guid userId, string? displayName = null)
        => new()
        {
            UserId = userId,
            DisplayName = displayName,
            AssignedAt = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc)
        };

    [TestMethod]
    public async Task ResolveAsync_AssignmentWithoutAName_GetsTheDirectoryName()
    {
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Assignee] = "Ben Kimball" });

        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            [Assignment(Assignee)], directory.Object, CancellationToken.None);

        Assert.AreEqual("Ben Kimball", result[0].DisplayName);
        Assert.AreEqual(Assignee, result[0].UserId);
    }

    [TestMethod]
    public async Task ResolveAsync_AssignmentAlreadyCarryingAName_KeepsItAndSkipsTheLookup()
    {
        var directory = new Mock<IUserDirectory>(MockBehavior.Strict);

        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            [Assignment(Assignee, "Ada Lovelace")], directory.Object, CancellationToken.None);

        Assert.AreEqual("Ada Lovelace", result[0].DisplayName);
        directory.Verify(
            x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ResolveAsync_NoUserDirectory_LeavesTheNameBlank()
    {
        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            [Assignment(Assignee)], userDirectory: null, CancellationToken.None);

        Assert.IsNull(result[0].DisplayName);
        Assert.AreEqual(Assignee, result[0].UserId);
    }

    [TestMethod]
    public async Task ResolveAsync_DirectoryWithoutTheUser_LeavesTheNameBlank()
    {
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            [Assignment(Assignee)], directory.Object, CancellationToken.None);

        Assert.IsNull(result[0].DisplayName);
    }

    [TestMethod]
    public async Task ResolveAsync_DirectoryThrows_LeavesTheNamesBlankInsteadOfFailing()
    {
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("core unreachable"));

        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            [Assignment(Assignee)], directory.Object, CancellationToken.None);

        Assert.IsNull(result[0].DisplayName);
    }

    [TestMethod]
    public async Task ResolveAsync_WorkItems_ResolvesEveryItemWithOneLookup()
    {
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var directory = new Mock<IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>
            {
                [Assignee] = "Ben Kimball",
                [other] = "Grace Hopper"
            });

        var items = new[]
        {
            CreateWorkItem(1, [Assignment(Assignee)]),
            CreateWorkItem(2, [Assignment(other)]),
            CreateWorkItem(3, [])
        };

        var result = await UserDisplayNames.ResolveWorkItemsAsync(items, directory.Object, CancellationToken.None);

        Assert.AreEqual("Ben Kimball", result[0].Assignments[0].DisplayName);
        Assert.AreEqual("Grace Hopper", result[1].Assignments[0].DisplayName);
        Assert.AreEqual(0, result[2].Assignments.Count);

        directory.Verify(
            x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ResolveAsync_EmptyPayload_DoesNotCallTheDirectory()
    {
        var directory = new Mock<IUserDirectory>(MockBehavior.Strict);

        var result = await UserDisplayNames.ResolveAssignmentsAsync(
            (IEnumerable<WorkItemAssignmentDto>)[], directory.Object, CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    private static WorkItemDto CreateWorkItem(int itemNumber, List<WorkItemAssignmentDto> assignments)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7(),
            Type = WorkItemType.Item,
            Title = $"Item {itemNumber}",
            Position = itemNumber * 1000,
            ETag = $"etag-{itemNumber}",
            CreatedAt = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc),
            Assignments = assignments,
            ItemNumber = itemNumber
        };
}
