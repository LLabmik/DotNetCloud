using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

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
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        return await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
    }

    [TestMethod]
    public async Task StartReviewSessionAsync_CreatesSession()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.NewGuid();

        var result = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        Assert.AreEqual(ReviewSessionStatus.Active, result.Status);
        Assert.AreEqual(hostId, result.HostUserId);
    }

    [TestMethod]
    public async Task GetReviewSessionAsync_ReturnsSession()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.NewGuid();
        var created = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.GetReviewSessionAsync(created.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task JoinSessionAsync_AddsParticipant()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.JoinSessionAsync(session.Id, memberId, CancellationToken.None);

        Assert.AreEqual(memberId, result.UserId);
    }

    [TestMethod]
    public async Task LeaveSessionAsync_DisconnectsParticipant()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
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
        var hostId = Guid.NewGuid();
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
        var hostId = Guid.NewGuid();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);

        var result = await _service.EndSessionAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(ReviewSessionStatus.Ended, result.Status);
    }

    [TestMethod]
    public async Task GetParticipantsAsync_ReturnsAllParticipants()
    {
        var epic = await SeedTestEpic();
        var hostId = Guid.NewGuid();
        var session = await _service.StartReviewSessionAsync(epic.Id, hostId, CancellationToken.None);
        await _service.JoinSessionAsync(session.Id, Guid.NewGuid(), CancellationToken.None);
        await _service.JoinSessionAsync(session.Id, Guid.NewGuid(), CancellationToken.None);

        var result = await _service.GetParticipantsAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(3, result.Count);
    }
}
