using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ReviewSessionServiceTests
{
    private TracksDbContext _db = null!;
    private ReviewSessionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new ReviewSessionService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<WorkItem> SeedTestEpic()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        return await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.CreateVersion7());
    }

    [TestMethod]
    public async Task StartReviewSessionAsync_CreatesSession()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();

        var result = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        Assert.AreEqual(ReviewSessionStatus.Active, result.Status);
        Assert.AreEqual(hostId, result.HostUserId);
    }

    [TestMethod]
    public async Task GetReviewSessionAsync_ReturnsSession()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var created = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.GetReviewSessionAsync(created.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task JoinSessionAsync_AddsParticipant()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.JoinSessionAsync(session.Id, memberId, CancellationToken.None);

        Assert.AreEqual(memberId, result.UserId);
    }

    [TestMethod]
    public async Task LeaveSessionAsync_DisconnectsParticipant()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var memberId = Guid.CreateVersion7();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);
        await _service.JoinSessionAsync(session.Id, memberId, CancellationToken.None);

        await _service.LeaveSessionAsync(session.Id, memberId, CancellationToken.None);

        var participants = await _service.GetParticipantsAsync(session.Id, CancellationToken.None);
        Assert.AreEqual(2, participants.Count);
        Assert.IsFalse(participants.First(p => p.UserId == memberId).IsConnected);
    }

    [TestMethod]
    public async Task SetCurrentItemAsync_SetsCurrentWorkItem()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, epic.ProductId);
        var item = await TestHelpers.SeedWorkItemAsync(_db, epic.ProductId, swimlane.Id, hostId);
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.SetCurrentItemAsync(session.Id, item.Id, CancellationToken.None);

        Assert.AreEqual(item.Id, result.CurrentItemId);
    }

    [TestMethod]
    public async Task EndSessionAsync_EndsSession()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.EndSessionAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(ReviewSessionStatus.Ended, result.Status);
    }

    [TestMethod]
    public async Task GetParticipantsAsync_ReturnsAllParticipants()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);
        await _service.JoinSessionAsync(session.Id, Guid.CreateVersion7(), CancellationToken.None);
        await _service.JoinSessionAsync(session.Id, Guid.CreateVersion7(), CancellationToken.None);

        var result = await _service.GetParticipantsAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public async Task GetParticipantsAsync_ResolvesDisplayNamesFromTheUserDirectory()
    {
        var directory = new Mock<DotNetCloud.Core.Capabilities.IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => (IReadOnlyDictionary<Guid, string>)ids.ToDictionary(
                id => id,
                id => $"User {id:N}"[..12]));
        var service = new ReviewSessionService(_db, TestHelpers.CreateServiceProvider(directory.Object));

        var epic = await SeedTestEpic();
        var hostId = Guid.CreateVersion7();
        var session = await service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);
        await service.JoinSessionAsync(session.Id, Guid.CreateVersion7(), CancellationToken.None);

        var result = await service.GetParticipantsAsync(session.Id, CancellationToken.None);

        Assert.IsTrue(result.All(p => !string.IsNullOrWhiteSpace(p.DisplayName)), "every participant should carry a name");
        Assert.IsTrue(result.Any(p => p.UserId == hostId && p.DisplayName!.StartsWith("User ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task JoinSessionAsync_ReturnsTheParticipantDisplayName()
    {
        var directory = new Mock<DotNetCloud.Core.Capabilities.IUserDirectory>();
        directory
            .Setup(x => x.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Participant] = "Ben Kimball" });
        var service = new ReviewSessionService(_db, TestHelpers.CreateServiceProvider(directory.Object));

        var epic = await SeedTestEpic();
        var session = await service.StartReviewSessionAsync(epic.Id, Guid.CreateVersion7(), CancellationToken.None);

        var result = await service.JoinSessionAsync(session.Id, Participant, CancellationToken.None);

        Assert.AreEqual("Ben Kimball", result.DisplayName);
    }

    [TestMethod]
    public async Task GetParticipantsAsync_WithoutAUserDirectory_LeavesTheNameBlank()
    {
        var epic = await SeedTestEpic();
        var session = await _service.StartReviewSessionAsync(epic.Id, Guid.CreateVersion7(), CancellationToken.None);

        var result = await _service.GetParticipantsAsync(session.Id, CancellationToken.None);

        Assert.IsNull(result[0].DisplayName);
    }

    private static readonly Guid Participant = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
}
