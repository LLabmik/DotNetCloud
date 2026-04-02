using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class BoardServiceTests
{
    private TracksDbContext _db;
    private BoardService _service;
    private Mock<IEventBus> _eventBusMock;
    private CallerContext _caller;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        _service = new BoardService(_db, _eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateBoard_ValidDto_ReturnsBoardWithOwnerMember()
    {
        var dto = new CreateBoardDto { Title = "My Board", Description = "Desc", Color = "#FF0000" };

        var result = await _service.CreateBoardAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Board", result.Title);
        Assert.AreEqual("Desc", result.Description);
        Assert.AreEqual("#FF0000", result.Color);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
        Assert.AreEqual(1, result.Members.Count);
        Assert.AreEqual(BoardMemberRole.Owner, result.Members[0].Role);
    }

    [TestMethod]
    public async Task CreateBoard_PublishesBoardCreatedEvent()
    {
        var dto = new CreateBoardDto { Title = "Event Board" };

        await _service.CreateBoardAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<BoardCreatedEvent>(e => e.Title == "Event Board" && e.OwnerId == _caller.UserId),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Get ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetBoard_AsMember_ReturnsBoard()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        var result = await _service.GetBoardAsync(board.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(board.Title, result.Title);
    }

    [TestMethod]
    public async Task GetBoard_NonMember_ReturnsNull()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());
        var otherCaller = TestHelpers.CreateCaller();

        var result = await _service.GetBoardAsync(board.Id, otherCaller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBoard_NonExistent_ReturnsNull()
    {
        var result = await _service.GetBoardAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    // ─── List ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListBoards_ReturnsOnlyMemberBoards()
    {
        await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "My Board");
        await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid(), "Other Board");

        var results = await _service.ListBoardsAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("My Board", results[0].Title);
    }

    [TestMethod]
    public async Task ListBoards_ExcludesArchived_ByDefault()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Archived");
        board.IsArchived = true;
        await _db.SaveChangesAsync();

        var results = await _service.ListBoardsAsync(_caller);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task ListBoards_IncludesArchived_WhenRequested()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Archived");
        board.IsArchived = true;
        await _db.SaveChangesAsync();

        var results = await _service.ListBoardsAsync(_caller, includeArchived: true);

        Assert.AreEqual(1, results.Count);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateBoard_AsAdmin_Updates()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        var result = await _service.UpdateBoardAsync(board.Id, new UpdateBoardDto { Title = "Updated" }, _caller);

        Assert.AreEqual("Updated", result.Title);
    }

    [TestMethod]
    public async Task UpdateBoard_AsViewer_Throws()
    {
        var ownerId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, ownerId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _caller.UserId, BoardMemberRole.Viewer);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.UpdateBoardAsync(board.Id, new UpdateBoardDto { Title = "Nope" }, _caller));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteBoard_AsOwner_SoftDeletes()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        await _service.DeleteBoardAsync(board.Id, _caller);

        var dbBoard = await _db.Boards.FindAsync(board.Id);
        Assert.IsTrue(dbBoard!.IsDeleted);
        Assert.IsNotNull(dbBoard.DeletedAt);
    }

    [TestMethod]
    public async Task DeleteBoard_AsAdmin_Throws()
    {
        var ownerId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, ownerId);
        await TestHelpers.AddMemberAsync(_db, board.Id, _caller.UserId, BoardMemberRole.Admin);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.DeleteBoardAsync(board.Id, _caller));
    }

    // ─── Members ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddMember_AsAdmin_AddsMember()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var newUserId = Guid.NewGuid();

        var result = await _service.AddMemberAsync(board.Id, newUserId, BoardMemberRole.Member, _caller);

        Assert.AreEqual(newUserId, result.UserId);
        Assert.AreEqual(BoardMemberRole.Member, result.Role);
    }

    [TestMethod]
    public async Task AddMember_DuplicateUser_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.AddMemberAsync(board.Id, _caller.UserId, BoardMemberRole.Member, _caller));
    }

    [TestMethod]
    public async Task AddMember_AsOwnerRole_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.AddMemberAsync(board.Id, Guid.NewGuid(), BoardMemberRole.Owner, _caller));
    }

    [TestMethod]
    public async Task RemoveMember_RemovesMember()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var memberId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, board.Id, memberId);

        await _service.RemoveMemberAsync(board.Id, memberId, _caller);

        var exists = await _db.BoardMembers.AnyAsync(m => m.UserId == memberId && m.BoardId == board.Id);
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task RemoveMember_CannotRemoveOwner()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.RemoveMemberAsync(board.Id, _caller.UserId, _caller));
    }

    [TestMethod]
    public async Task UpdateMemberRole_AsOwner_Changes()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        var memberId = Guid.NewGuid();
        await TestHelpers.AddMemberAsync(_db, board.Id, memberId, BoardMemberRole.Member);

        await _service.UpdateMemberRoleAsync(board.Id, memberId, BoardMemberRole.Admin, _caller);

        var member = await _db.BoardMembers.FirstAsync(m => m.UserId == memberId && m.BoardId == board.Id);
        Assert.AreEqual(BoardMemberRole.Admin, member.Role);
    }

    [TestMethod]
    public async Task GetMemberRole_ReturnsMemberRole()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        var role = await _service.GetMemberRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Owner, role);
    }

    [TestMethod]
    public async Task GetMemberRole_NonMember_ReturnsNull()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        var role = await _service.GetMemberRoleAsync(board.Id, Guid.NewGuid());

        Assert.IsNull(role);
    }

    // ─── Board Mode (Phase A) ────────────────────────────────────────

    [TestMethod]
    public async Task CreateBoard_DefaultMode_IsPersonal()
    {
        var dto = new CreateBoardDto { Title = "Personal Board" };

        var result = await _service.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Personal, result.Mode);
    }

    [TestMethod]
    public async Task CreateBoard_TeamMode_SetsMode()
    {
        var dto = new CreateBoardDto { Title = "Team Board", Mode = BoardMode.Team };

        var result = await _service.CreateBoardAsync(dto, _caller);

        Assert.AreEqual(BoardMode.Team, result.Mode);
    }

    [TestMethod]
    public async Task ListBoards_ModeFilter_ReturnsOnlyMatchingMode()
    {
        var personal = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Personal");
        var teamBoard = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Team");
        teamBoard.Mode = BoardMode.Team;
        _db.Update(teamBoard);
        await _db.SaveChangesAsync();

        var personalResults = await _service.ListBoardsAsync(_caller, modeFilter: BoardMode.Personal);
        var teamResults = await _service.ListBoardsAsync(_caller, modeFilter: BoardMode.Team);

        Assert.AreEqual(1, personalResults.Count);
        Assert.AreEqual("Personal", personalResults[0].Title);
        Assert.AreEqual(1, teamResults.Count);
        Assert.AreEqual("Team", teamResults[0].Title);
    }

    [TestMethod]
    public async Task ListBoards_NoFilter_ReturnsBothModes()
    {
        await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Personal");
        var teamBoard = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Team");
        teamBoard.Mode = BoardMode.Team;
        _db.Update(teamBoard);
        await _db.SaveChangesAsync();

        var results = await _service.ListBoardsAsync(_caller);

        Assert.AreEqual(2, results.Count);
    }

    [TestMethod]
    public async Task EnsureTeamMode_PersonalBoard_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Personal");
        // Default mode is Personal

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _service.EnsureTeamModeAsync(board.Id));
    }

    [TestMethod]
    public async Task EnsureTeamMode_TeamBoard_Succeeds()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Team");
        board.Mode = BoardMode.Team;
        _db.Update(board);
        await _db.SaveChangesAsync();

        // Should not throw
        await _service.EnsureTeamModeAsync(board.Id);
    }
}
