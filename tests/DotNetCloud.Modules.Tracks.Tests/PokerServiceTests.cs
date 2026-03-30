using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class PokerServiceTests
{
    private TracksDbContext _db = null!;
    private PokerService _service = null!;
    private BoardService _boardService = null!;
    private CallerContext _caller;
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;
    private Card _card = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var mock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, mock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, mock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _service = new PokerService(_db, _boardService, activityService, NullLogger<PokerService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _caller.UserId);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── StartSession ─────────────────────────────────────────────────

    [TestMethod]
    public async Task StartSession_ValidCard_CreatesSession()
    {
        var dto = new CreatePokerSessionDto { Scale = PokerScale.Fibonacci };

        var result = await _service.StartSessionAsync(_card.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(_card.Id, result.CardId);
        Assert.AreEqual(PokerScale.Fibonacci, result.Scale);
        Assert.AreEqual(PokerSessionStatus.Voting, result.Status);
        Assert.AreEqual(1, result.Round);
    }

    [TestMethod]
    public async Task StartSession_NonBoardMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, outsider));
    }

    [TestMethod]
    public async Task StartSession_MemberRole_Throws()
    {
        var member = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, member.UserId, BoardMemberRole.Member);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, member));
    }

    [TestMethod]
    public async Task StartSession_CardNotFound_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(Guid.NewGuid(), new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller));
    }

    [TestMethod]
    public async Task StartSession_DuplicateActive_Throws()
    {
        await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller));
    }

    // ─── SubmitVote ───────────────────────────────────────────────────

    [TestMethod]
    public async Task SubmitVote_FibonacciValidEstimate_RecordsVote()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);

        var result = await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "8" }, _caller);

        // Votes are masked as "?" until session is revealed — only count is reliable here
        Assert.AreEqual(1, result.Votes.Count);
    }

    [TestMethod]
    public async Task SubmitVote_InvalidEstimate_Throws()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "INVALID" }, _caller));
    }

    [TestMethod]
    public async Task SubmitVote_SessionNotFound_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SubmitVoteAsync(Guid.NewGuid(), new SubmitPokerVoteDto { Estimate = "5" }, _caller));
    }

    [TestMethod]
    public async Task SubmitVote_UpdatesExistingVote()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);
        await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "5" }, _caller);

        var updated = await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "13" }, _caller);

        // Still exactly one vote (updated, not duplicated); value is masked until reveal
        Assert.AreEqual(1, updated.Votes.Count);
    }

    // ─── RevealSession ────────────────────────────────────────────────

    [TestMethod]
    public async Task RevealSession_WithVotes_StatusBecomesRevealed()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);
        await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "8" }, _caller);

        var result = await _service.RevealSessionAsync(session.Id, _caller);

        Assert.AreEqual(PokerSessionStatus.Revealed, result.Status);
        Assert.AreEqual("8", result.Votes[0].Estimate);
    }

    [TestMethod]
    public async Task RevealSession_NoVotes_RevealsEmptySession()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);

        // Revealing with no votes succeeds — just transitions to Revealed with empty vote list
        var result = await _service.RevealSessionAsync(session.Id, _caller);

        Assert.AreEqual(PokerSessionStatus.Revealed, result.Status);
        Assert.AreEqual(0, result.Votes.Count);
    }

    // ─── AcceptEstimate ───────────────────────────────────────────────

    [TestMethod]
    public async Task AcceptEstimate_ValidEstimate_CompletesSession()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);
        await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "8" }, _caller);
        await _service.RevealSessionAsync(session.Id, _caller);

        var result = await _service.AcceptEstimateAsync(session.Id, new AcceptPokerEstimateDto { AcceptedEstimate = "8", StoryPoints = 8 }, _caller);

        Assert.AreEqual(PokerSessionStatus.Completed, result.Status);
        Assert.AreEqual("8", result.AcceptedEstimate);
    }

    // ─── StartNewRound ────────────────────────────────────────────────

    [TestMethod]
    public async Task StartNewRound_IncrementsRound()
    {
        var session = await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);
        await _service.SubmitVoteAsync(session.Id, new SubmitPokerVoteDto { Estimate = "5" }, _caller);
        await _service.RevealSessionAsync(session.Id, _caller);

        var result = await _service.StartNewRoundAsync(session.Id, _caller);

        Assert.AreEqual(2, result.Round);
        Assert.AreEqual(PokerSessionStatus.Voting, result.Status);
        Assert.AreEqual(0, result.Votes.Count);
    }

    // ─── GetCardSessions ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetCardSessions_ReturnsAllSessions()
    {
        await _service.StartSessionAsync(_card.Id, new CreatePokerSessionDto { Scale = PokerScale.Fibonacci }, _caller);

        var results = await _service.GetCardSessionsAsync(_card.Id, _caller);

        Assert.AreEqual(1, results.Count);
    }

    [TestMethod]
    public async Task GetCardSessions_CardNotFound_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.GetCardSessionsAsync(Guid.NewGuid(), _caller));
    }
}
