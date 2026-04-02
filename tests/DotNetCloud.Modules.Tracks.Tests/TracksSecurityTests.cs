using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Security tests for the Tracks module: board role authorization, team role escalation
/// prevention, tenant isolation, and Markdown XSS content safety validation.
/// </summary>
[TestClass]
public class TracksSecurityTests
{
    private TracksDbContext _db = null!;
    private BoardService _boardService = null!;
    private CardService _cardService = null!;
    private SwimlaneService _swimlaneService = null!;
    private CommentService _commentService = null!;
    private LabelService _labelService = null!;
    private SprintService _sprintService = null!;
    private TeamService _teamService = null!;
    private DependencyService _dependencyService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _admin = null!;
    private CallerContext _member = null!;
    private CallerContext _viewer = null!;
    private CallerContext _outsider = null!;
    private Board _board = null!;
    private BoardSwimlane _swimlane = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        _teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _boardService = new BoardService(_db, _eventBusMock.Object, activityService, _teamService, NullLogger<BoardService>.Instance);
        _swimlaneService = new SwimlaneService(_db, _boardService, activityService, NullLogger<SwimlaneService>.Instance);
        _cardService = new CardService(_db, _boardService, activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _commentService = new CommentService(_db, _boardService, activityService, _eventBusMock.Object, NullLogger<CommentService>.Instance);
        _labelService = new LabelService(_db, _boardService, activityService, NullLogger<LabelService>.Instance);
        _sprintService = new SprintService(_db, _boardService, activityService, _eventBusMock.Object, NullLogger<SprintService>.Instance);
        _dependencyService = new DependencyService(_db, _boardService, activityService, NullLogger<DependencyService>.Instance);

        // Create users with different roles
        _owner = TestHelpers.CreateCaller();
        _admin = TestHelpers.CreateCaller();
        _member = TestHelpers.CreateCaller();
        _viewer = TestHelpers.CreateCaller();
        _outsider = TestHelpers.CreateCaller();

        // Seed board with Owner
        _board = await TestHelpers.SeedBoardAsync(_db, _owner.UserId);
        await TestHelpers.AddMemberAsync(_db, _board.Id, _admin.UserId, BoardMemberRole.Admin);
        await TestHelpers.AddMemberAsync(_db, _board.Id, _member.UserId, BoardMemberRole.Member);
        await TestHelpers.AddMemberAsync(_db, _board.Id, _viewer.UserId, BoardMemberRole.Viewer);

        // Seed a list and card for tests
        _swimlane = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Board Tenant Isolation ──────────────────────────────────────

    [TestMethod]
    public async Task GetBoard_Outsider_ReturnsNull()
    {
        var result = await _boardService.GetBoardAsync(_board.Id, _outsider);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListBoards_OnlyReturnsMemberBoards()
    {
        // Create second board owned by outsider
        var otherBoard = await TestHelpers.SeedBoardAsync(_db, _outsider.UserId, "Other Board");

        var ownerBoards = await _boardService.ListBoardsAsync(_owner);
        var outsiderBoards = await _boardService.ListBoardsAsync(_outsider);

        Assert.AreEqual(1, ownerBoards.Count);
        Assert.AreEqual(_board.Id, ownerBoards[0].Id);
        Assert.AreEqual(1, outsiderBoards.Count);
        Assert.AreEqual(otherBoard.Id, outsiderBoards[0].Id);
    }

    // ─── Board Role Authorization ────────────────────────────────────

    [TestMethod]
    public async Task DeleteBoard_ByViewer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _boardService.DeleteBoardAsync(_board.Id, _viewer));
    }

    [TestMethod]
    public async Task DeleteBoard_ByMember_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _boardService.DeleteBoardAsync(_board.Id, _member));
    }

    [TestMethod]
    public async Task DeleteBoard_ByOutsider_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _boardService.DeleteBoardAsync(_board.Id, _outsider));
    }

    [TestMethod]
    public async Task DeleteBoard_ByOwner_Succeeds()
    {
        await _boardService.DeleteBoardAsync(_board.Id, _owner);

        var result = await _boardService.GetBoardAsync(_board.Id, _owner);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateBoard_ByViewer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _boardService.UpdateBoardAsync(_board.Id,
                new UpdateBoardDto { Title = "Hijacked" }, _viewer));
    }

    [TestMethod]
    public async Task UpdateBoard_ByAdmin_Succeeds()
    {
        var result = await _boardService.UpdateBoardAsync(_board.Id,
            new UpdateBoardDto { Title = "Admin Title" }, _admin);

        Assert.IsNotNull(result);
        Assert.AreEqual("Admin Title", result.Title);
    }

    // ─── List Role Authorization ─────────────────────────────────────

    [TestMethod]
    public async Task CreateList_ByViewer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _swimlaneService.CreateSwimlaneAsync(_board.Id,
                new CreateBoardSwimlaneDto { Title = "Blocked" }, _viewer));
    }

    [TestMethod]
    public async Task CreateList_ByMember_Succeeds()
    {
        var result = await _swimlaneService.CreateSwimlaneAsync(_board.Id,
            new CreateBoardSwimlaneDto { Title = "Allowed" }, _member);

        Assert.IsNotNull(result);
        Assert.AreEqual("Allowed", result.Title);
    }

    [TestMethod]
    public async Task CreateList_ByOutsider_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _swimlaneService.CreateSwimlaneAsync(_board.Id,
                new CreateBoardSwimlaneDto { Title = "No Access" }, _outsider));
    }

    // ─── Card Role Authorization ─────────────────────────────────────

    [TestMethod]
    public async Task CreateCard_ByViewer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _cardService.CreateCardAsync(_swimlane.Id,
                new CreateCardDto { Title = "Blocked" }, _viewer));
    }

    [TestMethod]
    public async Task CreateCard_ByMember_Succeeds()
    {
        var result = await _cardService.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "Allowed" }, _member);

        Assert.IsNotNull(result);
        Assert.AreEqual("Allowed", result.Title);
    }

    [TestMethod]
    public async Task CreateCard_ByOutsider_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _cardService.CreateCardAsync(_swimlane.Id,
                new CreateCardDto { Title = "Blocked" }, _outsider));
    }

    [TestMethod]
    public async Task MoveCard_ByViewer_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _member.UserId);
        var targetList = await TestHelpers.SeedSwimlaneAsync(_db, _board.Id, "Target");

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _cardService.MoveCardAsync(card.Id,
                new MoveCardDto { TargetSwimlaneId = targetList.Id, Position = 1000 }, _viewer));
    }

    // ─── Comment Authorization ───────────────────────────────────────

    [TestMethod]
    public async Task AddComment_ByViewer_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _member.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _commentService.CreateCommentAsync(card.Id, "Blocked", _viewer));
    }

    [TestMethod]
    public async Task AddComment_ByMember_Succeeds()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _member.UserId);

        var result = await _commentService.CreateCommentAsync(card.Id, "Allowed", _member);

        Assert.IsNotNull(result);
        Assert.AreEqual("Allowed", result.Content);
    }

    [TestMethod]
    public async Task AddComment_ByOutsider_Throws()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _member.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _commentService.CreateCommentAsync(card.Id, "Blocked", _outsider));
    }

    // ─── Sprint Authorization ────────────────────────────────────────

    [TestMethod]
    public async Task CreateSprint_ByMember_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _sprintService.CreateSprintAsync(_board.Id,
                new CreateSprintDto
                {
                    Title = "Sprint 1",
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(14)
                }, _member));
    }

    [TestMethod]
    public async Task CreateSprint_ByViewer_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _sprintService.CreateSprintAsync(_board.Id,
                new CreateSprintDto
                {
                    Title = "Sprint 1",
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(14)
                }, _viewer));
    }

    [TestMethod]
    public async Task CreateSprint_ByAdmin_Succeeds()
    {
        var result = await _sprintService.CreateSprintAsync(_board.Id,
            new CreateSprintDto
            {
                Title = "Sprint 1",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(14)
            }, _admin);

        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 1", result.Title);
    }

    // ─── Label Authorization ─────────────────────────────────────────

    [TestMethod]
    public async Task CreateLabel_ByMember_Throws()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _labelService.CreateLabelAsync(_board.Id,
                new CreateLabelDto { Title = "Bug", Color = "#FF0000" }, _member));
    }

    [TestMethod]
    public async Task CreateLabel_ByAdmin_Succeeds()
    {
        var result = await _labelService.CreateLabelAsync(_board.Id,
            new CreateLabelDto { Title = "Bug", Color = "#FF0000" }, _admin);

        Assert.IsNotNull(result);
        Assert.AreEqual("Bug", result.Title);
    }

    // ─── Dependency Cycle Detection ──────────────────────────────────

    [TestMethod]
    public async Task AddDependency_DeepCycle_Throws()
    {
        // A → B → C → D → A should fail (4-node cycle)
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "B");
        var cardC = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "C");
        var cardD = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "D");

        await _dependencyService.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _owner);
        await _dependencyService.AddDependencyAsync(cardB.Id, cardC.Id, CardDependencyType.BlockedBy, _owner);
        await _dependencyService.AddDependencyAsync(cardC.Id, cardD.Id, CardDependencyType.BlockedBy, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _dependencyService.AddDependencyAsync(cardD.Id, cardA.Id, CardDependencyType.BlockedBy, _owner));
    }

    [TestMethod]
    public async Task AddDependency_DirectCycle_Throws()
    {
        // A → B, then B → A should fail
        var cardA = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "A");
        var cardB = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId, "B");

        await _dependencyService.AddDependencyAsync(cardA.Id, cardB.Id, CardDependencyType.BlockedBy, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _dependencyService.AddDependencyAsync(cardB.Id, cardA.Id, CardDependencyType.BlockedBy, _owner));
    }

    // ─── Team Role Escalation Prevention ─────────────────────────────

    [TestMethod]
    public async Task TeamService_MemberCannotEscalateToOwner()
    {
        // Setup TeamService with mocked capabilities
        var teamId = Guid.NewGuid();
        var teamDirectoryMock = new Mock<ITeamDirectory>();
        var teamManagerMock = new Mock<ITeamManager>();

        teamManagerMock
            .Setup(m => m.CreateTeamAsync(It.IsAny<Guid>(), "Test Team", null, _owner.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfo { Id = teamId, OrganizationId = Guid.Empty, Name = "Test Team", MemberCount = 1, CreatedAt = DateTime.UtcNow });
        teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfo { Id = teamId, OrganizationId = Guid.Empty, Name = "Test Team", MemberCount = 1, CreatedAt = DateTime.UtcNow });
        teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var teamServiceWithCaps = new TeamService(
            _db, _eventBusMock.Object, NullLogger<TeamService>.Instance,
            teamDirectoryMock.Object, teamManagerMock.Object);

        // Create team as owner
        var team = await teamServiceWithCaps.CreateTeamAsync(
            new CreateTracksTeamDto { Name = "Test Team" }, _owner);

        // Add member
        await teamServiceWithCaps.AddMemberAsync(team.Id, _member.UserId, TracksTeamMemberRole.Member, _owner);

        // Member tries to update their own role to Owner — should throw
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => teamServiceWithCaps.UpdateMemberRoleAsync(team.Id, _member.UserId, TracksTeamMemberRole.Owner, _member));
    }

    [TestMethod]
    public async Task TeamService_NonMemberCannotAccessTeam()
    {
        var teamId = Guid.NewGuid();
        var teamDirectoryMock = new Mock<ITeamDirectory>();
        var teamManagerMock = new Mock<ITeamManager>();

        teamManagerMock
            .Setup(m => m.CreateTeamAsync(It.IsAny<Guid>(), "Private Team", null, _owner.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfo { Id = teamId, OrganizationId = Guid.Empty, Name = "Private Team", MemberCount = 1, CreatedAt = DateTime.UtcNow });
        teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfo { Id = teamId, OrganizationId = Guid.Empty, Name = "Private Team", MemberCount = 1, CreatedAt = DateTime.UtcNow });
        teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, _owner.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, _outsider.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var teamServiceWithCaps = new TeamService(
            _db, _eventBusMock.Object, NullLogger<TeamService>.Instance,
            teamDirectoryMock.Object, teamManagerMock.Object);

        var team = await teamServiceWithCaps.CreateTeamAsync(
            new CreateTracksTeamDto { Name = "Private Team" }, _owner);

        // Outsider tries to get team — should return null
        var result = await teamServiceWithCaps.GetTeamAsync(team.Id, _outsider);
        Assert.IsNull(result);
    }

    // ─── Markdown Content Safety (XSS) ──────────────────────────────
    // Card descriptions and comments store Markdown as-is.
    // Sanitization is a presentation-layer concern (Markdig + HtmlSanitizer on render).
    // These tests verify the service layer stores potentially dangerous content correctly.

    [TestMethod]
    public async Task CreateCard_WithScriptTag_StoresContent()
    {
        var xssContent = "<script>alert('xss')</script>";
        var card = await _cardService.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "XSS Card", Description = xssContent }, _owner);

        var retrieved = await _cardService.GetCardAsync(card.Id, _owner);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Description);
    }

    [TestMethod]
    public async Task AddComment_WithScriptTag_StoresContent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId);
        var xssContent = "<script>document.cookie</script>";

        var comment = await _commentService.CreateCommentAsync(card.Id, xssContent, _owner);

        Assert.IsNotNull(comment);
        Assert.AreEqual(xssContent, comment.Content);
    }

    [TestMethod]
    public async Task CreateCard_WithImgOnError_StoresContent()
    {
        var xssContent = "<img src=x onerror=alert('xss')>";
        var card = await _cardService.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "Img XSS", Description = xssContent }, _owner);

        var retrieved = await _cardService.GetCardAsync(card.Id, _owner);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Description);
    }

    [TestMethod]
    public async Task AddComment_WithIframe_StoresContent()
    {
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId);
        var xssContent = "<iframe src='https://evil.example.com'></iframe>";

        var comment = await _commentService.CreateCommentAsync(card.Id, xssContent, _owner);

        Assert.IsNotNull(comment);
        Assert.AreEqual(xssContent, comment.Content);
    }

    [TestMethod]
    public async Task CreateCard_WithJavascriptUrl_StoresContent()
    {
        var xssContent = "[click](javascript:alert('xss'))";
        var card = await _cardService.CreateCardAsync(_swimlane.Id,
            new CreateCardDto { Title = "JS URL", Description = xssContent }, _owner);

        var retrieved = await _cardService.GetCardAsync(card.Id, _owner);

        Assert.IsNotNull(retrieved);
        Assert.AreEqual(xssContent, retrieved.Description);
    }

    // ─── Cross-Board Isolation ───────────────────────────────────────

    [TestMethod]
    public async Task MoveCard_ToOtherBoardSwimlane_OwnerNotMemberOfTarget_Succeeds()
    {
        // Note: MoveCardAsync moves by target list ID. If the caller is a member
        // of the card's current board, the operation proceeds. Cross-board moves
        // are permitted at the service layer — board membership check is on the source board.
        var otherBoard = await TestHelpers.SeedBoardAsync(_db, _outsider.UserId, "Other Board");
        var otherList = await TestHelpers.SeedSwimlaneAsync(_db, otherBoard.Id, "Other List");
        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId);

        // Move succeeds because owner is a member of the card's source board
        var moved = await _cardService.MoveCardAsync(card.Id,
            new MoveCardDto { TargetSwimlaneId = otherList.Id, Position = 1000 }, _owner);

        Assert.IsNotNull(moved);
        Assert.AreEqual(otherList.Id, moved.SwimlaneId);
    }

    [TestMethod]
    public async Task AddLabel_FromOtherBoard_Throws()
    {
        // Labels from board A cannot be applied to cards on board B
        var otherBoard = await TestHelpers.SeedBoardAsync(_db, _owner.UserId, "Board B");
        var otherList = await TestHelpers.SeedSwimlaneAsync(_db, otherBoard.Id, "B List");
        var boardBCard = await TestHelpers.SeedCardAsync(_db, otherList.Id, _owner.UserId);
        var boardALabel = await _labelService.CreateLabelAsync(_board.Id,
            new CreateLabelDto { Title = "Board A Label", Color = "#000000" }, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _labelService.AddLabelToCardAsync(boardBCard.Id, boardALabel.Id, _owner));
    }

    // ─── Board Mode Enforcement (Phase A) ────────────────────────────

    [TestMethod]
    public async Task SprintPlanning_PersonalBoard_Throws()
    {
        // Sprint planning requires Team mode
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var planningService = new SprintPlanningService(_db, _boardService, activityService, NullLogger<SprintPlanningService>.Instance);

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 4,
            DefaultDurationWeeks = 2
        };

        // Default board is Personal mode
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => planningService.CreateYearPlanAsync(_board.Id, dto, _owner));
    }

    [TestMethod]
    public async Task SprintPlanning_TeamBoard_Succeeds()
    {
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var planningService = new SprintPlanningService(_db, _boardService, activityService, NullLogger<SprintPlanningService>.Instance);

        var dto = new CreateSprintPlanDto
        {
            StartDate = DateTime.UtcNow,
            SprintCount = 2,
            DefaultDurationWeeks = 2
        };

        var result = await planningService.CreateYearPlanAsync(_board.Id, dto, _owner);
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Sprints.Count);
    }

    // ─── Review Session Authorization (Phase B) ──────────────────────

    [TestMethod]
    public async Task ReviewSession_PersonalBoard_Throws()
    {
        // Default board is Personal mode; review sessions require Team mode
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var realtimeMock = new DotNetCloud.Modules.Tracks.Tests.NullTracksRealtimeService();
        var pokerService = new PokerService(_db, _boardService, activityService, realtimeMock, NullLogger<PokerService>.Instance);
        var reviewService = new ReviewSessionService(_db, _boardService, pokerService, realtimeMock, NullLogger<ReviewSessionService>.Instance);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => reviewService.StartSessionAsync(_board.Id, _owner));
    }

    [TestMethod]
    public async Task ReviewSession_NonHost_CannotSetCard()
    {
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var realtimeMock = new DotNetCloud.Modules.Tracks.Tests.NullTracksRealtimeService();
        var pokerService = new PokerService(_db, _boardService, activityService, realtimeMock, NullLogger<PokerService>.Instance);
        var reviewService = new ReviewSessionService(_db, _boardService, pokerService, realtimeMock, NullLogger<ReviewSessionService>.Instance);

        var session = await reviewService.StartSessionAsync(_board.Id, _owner);
        await reviewService.JoinSessionAsync(session.Id, _member);

        var card = await TestHelpers.SeedCardAsync(_db, _swimlane.Id, _owner.UserId);

        // Member (non-host) cannot set current card
        var ex = await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => reviewService.SetCurrentCardAsync(session.Id, card.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task ReviewSession_NonHost_CannotEndSession()
    {
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var realtimeMock = new DotNetCloud.Modules.Tracks.Tests.NullTracksRealtimeService();
        var pokerService = new PokerService(_db, _boardService, activityService, realtimeMock, NullLogger<PokerService>.Instance);
        var reviewService = new ReviewSessionService(_db, _boardService, pokerService, realtimeMock, NullLogger<ReviewSessionService>.Instance);

        var session = await reviewService.StartSessionAsync(_board.Id, _owner);
        await reviewService.JoinSessionAsync(session.Id, _member);

        var ex = await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => reviewService.EndSessionAsync(session.Id, _member));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.ReviewSessionNotHost));
    }

    [TestMethod]
    public async Task ReviewSession_NonAdmin_CannotStart()
    {
        _board.Mode = BoardMode.Team;
        _db.Update(_board);
        await _db.SaveChangesAsync();

        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var realtimeMock = new DotNetCloud.Modules.Tracks.Tests.NullTracksRealtimeService();
        var pokerService = new PokerService(_db, _boardService, activityService, realtimeMock, NullLogger<PokerService>.Instance);
        var reviewService = new ReviewSessionService(_db, _boardService, pokerService, realtimeMock, NullLogger<ReviewSessionService>.Instance);

        // Member (non-Admin) cannot start a review session
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => reviewService.StartSessionAsync(_board.Id, _member));
    }
}
