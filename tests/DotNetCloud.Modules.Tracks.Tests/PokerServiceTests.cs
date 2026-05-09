using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class PokerServiceTests
{
    private TracksDbContext _db = null!;
    private PokerService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new PokerService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>Creates a product, an epic, a feature under the epic, and an item under the feature.</summary>
    private async Task<(WorkItem Epic, WorkItem Item)> SeedEpicWithItemAsync()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.NewGuid());
        var feature = await TestHelpers.SeedWorkItemAsync(_db, product.Id, null, Guid.NewGuid(), "Feature", WorkItemType.Feature);
        feature.ParentWorkItemId = epic.Id;
        await _db.SaveChangesAsync();
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, feature.Id, SwimlaneContainerType.WorkItem);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid(), "Poker Item", WorkItemType.Item);
        item.ParentWorkItemId = feature.Id;
        await _db.SaveChangesAsync();
        return (epic, item);
    }

    [TestMethod]
    public async Task StartSessionAsync_CreatesPokerSession()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var userId = Guid.NewGuid();
        var dto = new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci };

        var result = await _service.StartSessionAsync(epic.Id, userId, dto, CancellationToken.None);

        Assert.AreEqual(PokerSessionStatus.Voting, result.Status);
        Assert.AreEqual(PokerScale.Fibonacci, result.Scale);
    }

    [TestMethod]
    public async Task GetSessionAsync_ReturnsSession()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var userId = Guid.NewGuid();
        var created = await _service.StartSessionAsync(epic.Id, userId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);

        var result = await _service.GetSessionAsync(created.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task SubmitVoteAsync_RecordsVote()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var hostId = Guid.NewGuid();
        var voterId = Guid.NewGuid();
        var session = await _service.StartSessionAsync(epic.Id, hostId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);

        var result = await _service.SubmitVoteAsync(session.Id, voterId,
            new SubmitPokerVoteDto { Estimate = "5" }, CancellationToken.None);

        Assert.AreEqual(PokerSessionStatus.Voting, result.Status);
    }

    [TestMethod]
    public async Task RevealVotesAsync_RevealsAllVotes()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var hostId = Guid.NewGuid();
        var voterId = Guid.NewGuid();
        var session = await _service.StartSessionAsync(epic.Id, hostId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);
        await _service.SubmitVoteAsync(session.Id, voterId, new SubmitPokerVoteDto { Estimate = "5" }, CancellationToken.None);

        var result = await _service.RevealVotesAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(PokerSessionStatus.Revealed, result.Status);
    }

    [TestMethod]
    public async Task AcceptEstimateAsync_CompletesSession()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var hostId = Guid.NewGuid();
        var voterId = Guid.NewGuid();
        var session = await _service.StartSessionAsync(epic.Id, hostId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);
        await _service.SubmitVoteAsync(session.Id, voterId, new SubmitPokerVoteDto { Estimate = "5" }, CancellationToken.None);
        await _service.RevealVotesAsync(session.Id, CancellationToken.None);

        var result = await _service.AcceptEstimateAsync(session.Id, "5", CancellationToken.None);

        Assert.AreEqual(PokerSessionStatus.Completed, result.Status);
    }

    [TestMethod]
    public async Task NewRoundAsync_ResetsVotes()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var hostId = Guid.NewGuid();
        var voterId = Guid.NewGuid();
        var session = await _service.StartSessionAsync(epic.Id, hostId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);
        await _service.SubmitVoteAsync(session.Id, voterId, new SubmitPokerVoteDto { Estimate = "5" }, CancellationToken.None);
        await _service.RevealVotesAsync(session.Id, CancellationToken.None);

        var result = await _service.NewRoundAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(PokerSessionStatus.Voting, result.Status);
    }

    [TestMethod]
    public async Task GetVoteStatusAsync_ReturnsVoterStatus()
    {
        var (epic, item) = await SeedEpicWithItemAsync();
        var hostId = Guid.NewGuid();
        var voterId = Guid.NewGuid();
        var session = await _service.StartSessionAsync(epic.Id, hostId, new CreatePokerSessionDto { ItemId = item.Id, Scale = PokerScale.Fibonacci }, CancellationToken.None);
        await _service.SubmitVoteAsync(session.Id, voterId, new SubmitPokerVoteDto { Estimate = "5" }, CancellationToken.None);

        var status = await _service.GetVoteStatusAsync(session.Id, CancellationToken.None);

        Assert.AreEqual(1, status.Count);
        Assert.IsTrue(status[0].HasVoted);
    }
}
