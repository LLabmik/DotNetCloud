using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class TeamServiceTests
{
    private TracksDbContext _db;
    private Mock<IEventBus> _eventBusMock;
    private Mock<ITeamDirectory> _teamDirectoryMock;
    private Mock<ITeamManager> _teamManagerMock;
    private TeamService _service;
    private CallerContext _caller;

    private static readonly Guid OrgId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _teamDirectoryMock = new Mock<ITeamDirectory>();
        _teamManagerMock = new Mock<ITeamManager>();
        _service = new TeamService(
            _db, _eventBusMock.Object, NullLogger<TeamService>.Instance,
            _teamDirectoryMock.Object, _teamManagerMock.Object);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Create ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateTeam_CreatesCorTeameAndTracksOwnerRole()
    {
        var teamId = Guid.NewGuid();
        _teamManagerMock
            .Setup(m => m.CreateTeamAsync(OrgId, "Dev Team", "Desc", _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "Dev Team", "Desc"));

        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "Dev Team", "Desc"));

        var dto = new CreateTracksTeamDto { Name = "Dev Team", Description = "Desc", OrganizationId = OrgId };
        var result = await _service.CreateTeamAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Dev Team", result.Name);
        Assert.AreEqual(1, result.Members.Count);
        Assert.AreEqual(TracksTeamMemberRole.Owner, result.Members[0].Role);
        Assert.AreEqual(_caller.UserId, result.Members[0].UserId);

        // Verify Tracks TeamRole was persisted
        var role = await _db.TeamRoles.FindAsync(
            _db.TeamRoles.Where(r => r.CoreTeamId == teamId).Select(r => r.Id).First());
        Assert.IsNotNull(role);
        Assert.AreEqual(TracksTeamMemberRole.Owner, role.Role);
    }

    [TestMethod]
    public async Task CreateTeam_WithoutManager_Throws()
    {
        var service = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);

        var dto = new CreateTracksTeamDto { Name = "Team" };

        await Assert.ThrowsExactlyAsync<System.InvalidOperationException>(
            () => service.CreateTeamAsync(dto, _caller));
    }

    [TestMethod]
    public async Task CreateTeam_PublishesEvent()
    {
        var teamId = Guid.NewGuid();
        _teamManagerMock
            .Setup(m => m.CreateTeamAsync(It.IsAny<Guid>(), "T", null, _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "T"));

        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "T"));

        var dto = new CreateTracksTeamDto { Name = "T" };
        await _service.CreateTeamAsync(dto, _caller);

        _eventBusMock.Verify(
            e => e.PublishAsync(It.Is<TeamCreatedEvent>(ev => ev.TeamId == teamId), _caller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Get / List ───────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTeam_NotMember_ReturnsNull()
    {
        _teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(It.IsAny<Guid>(), _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetTeamAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetTeam_AsMember_ReturnsTeam()
    {
        var teamId = Guid.NewGuid();
        _teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "My Team"));

        var result = await _service.GetTeamAsync(teamId, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("My Team", result.Name);
    }

    [TestMethod]
    public async Task ListTeams_NoTeamDirectory_ReturnsEmpty()
    {
        var service = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);

        var result = await service.ListTeamsAsync(_caller);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListTeams_ReturnsMemberTeams()
    {
        var teamId = Guid.NewGuid();
        _teamDirectoryMock
            .Setup(d => d.GetTeamsForUserAsync(_caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeTeamInfo(teamId, "Team A")]);

        // Seed a Tracks role for the user
        _db.TeamRoles.Add(new TeamRole
        {
            CoreTeamId = teamId,
            UserId = _caller.UserId,
            Role = TracksTeamMemberRole.Owner,
            AssignedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.ListTeamsAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Team A", result[0].Name);
        Assert.AreEqual(1, result[0].Members.Count);
    }

    // ─── Update ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateTeam_AsManager_Succeeds()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);

        _teamManagerMock
            .Setup(m => m.UpdateTeamAsync(teamId, "New Name", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "New Name"));
        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "New Name"));

        var dto = new UpdateTracksTeamDto { Name = "New Name" };
        var result = await _service.UpdateTeamAsync(teamId, dto, _caller);

        Assert.AreEqual("New Name", result.Name);
    }

    [TestMethod]
    public async Task UpdateTeam_AsMember_Throws()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Member);

        var dto = new UpdateTracksTeamDto { Name = "X" };

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.UpdateTeamAsync(teamId, dto, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksInsufficientTeamRole));
    }

    // ─── Delete ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTeam_AsOwner_Succeeds()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);

        _teamManagerMock
            .Setup(m => m.DeleteTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.DeleteTeamAsync(teamId, cascade: false, _caller);

        _teamManagerMock.Verify(m => m.DeleteTeamAsync(teamId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(0, _db.TeamRoles.Count());
    }

    [TestMethod]
    public async Task DeleteTeam_WithBoards_NoCascade_Throws()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);

        // Seed a board belonging to this team
        _db.Boards.Add(new Board { Title = "B1", OwnerId = _caller.UserId, TeamId = teamId });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.DeleteTeamAsync(teamId, cascade: false, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksTeamHasBoards));
    }

    [TestMethod]
    public async Task DeleteTeam_WithBoards_Cascade_DeletesAll()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);

        _db.Boards.Add(new Board { Title = "B1", OwnerId = _caller.UserId, TeamId = teamId });
        await _db.SaveChangesAsync();

        _teamManagerMock
            .Setup(m => m.DeleteTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.DeleteTeamAsync(teamId, cascade: true, _caller);

        Assert.IsTrue(_db.Boards.All(b => b.IsDeleted));
        Assert.AreEqual(0, _db.TeamRoles.Count());
    }

    [TestMethod]
    public async Task DeleteTeam_AsManager_Throws()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.DeleteTeamAsync(teamId, cascade: false, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksInsufficientTeamRole));
    }

    // ─── Add Member ───────────────────────────────────────────────────

    [TestMethod]
    public async Task AddMember_AsManager_Succeeds()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);

        var newUserId = Guid.NewGuid();
        _teamManagerMock
            .Setup(m => m.AddMemberAsync(teamId, newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.AddMemberAsync(teamId, newUserId, TracksTeamMemberRole.Member, _caller);

        Assert.AreEqual(newUserId, result.UserId);
        Assert.AreEqual(TracksTeamMemberRole.Member, result.Role);
    }

    [TestMethod]
    public async Task AddMember_AsOwnerRole_Throws()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AddMemberAsync(teamId, Guid.NewGuid(), TracksTeamMemberRole.Owner, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksInsufficientTeamRole));
    }

    [TestMethod]
    public async Task AddMember_AlreadyMember_Throws()
    {
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);
        await SeedTeamRoleAsync(teamId, userId, TracksTeamMemberRole.Member);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.AddMemberAsync(teamId, userId, TracksTeamMemberRole.Member, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksAlreadyTeamMember));
    }

    // ─── Remove Member ────────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveMember_AsManager_Succeeds()
    {
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);
        await SeedTeamRoleAsync(teamId, userId, TracksTeamMemberRole.Member);

        _teamManagerMock
            .Setup(m => m.RemoveMemberAsync(teamId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.RemoveMemberAsync(teamId, userId, _caller);

        Assert.IsFalse(_db.TeamRoles.Any(r => r.CoreTeamId == teamId && r.UserId == userId));
    }

    [TestMethod]
    public async Task RemoveMember_Owner_Throws()
    {
        var teamId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);
        await SeedTeamRoleAsync(teamId, ownerId, TracksTeamMemberRole.Owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.RemoveMemberAsync(teamId, ownerId, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksInsufficientTeamRole));
    }

    // ─── Update Member Role ───────────────────────────────────────────

    [TestMethod]
    public async Task UpdateMemberRole_AsOwner_Succeeds()
    {
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);
        await SeedTeamRoleAsync(teamId, userId, TracksTeamMemberRole.Member);

        await _service.UpdateMemberRoleAsync(teamId, userId, TracksTeamMemberRole.Manager, _caller);

        var updated = _db.TeamRoles.First(r => r.CoreTeamId == teamId && r.UserId == userId);
        Assert.AreEqual(TracksTeamMemberRole.Manager, updated.Role);
    }

    [TestMethod]
    public async Task UpdateMemberRole_DemoteLastOwner_Throws()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);

        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.UpdateMemberRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager, _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.TracksInsufficientTeamRole));
    }

    // ─── Transfer Board ───────────────────────────────────────────────

    [TestMethod]
    public async Task TransferBoard_ToTeam_Succeeds()
    {
        var teamId = Guid.NewGuid();
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Manager);

        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        _teamDirectoryMock
            .Setup(d => d.GetTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTeamInfo(teamId, "T"));

        await _service.TransferBoardAsync(board.Id, teamId, _caller);

        var updated = _db.Boards.First(b => b.Id == board.Id);
        Assert.AreEqual(teamId, updated.TeamId);
    }

    [TestMethod]
    public async Task TransferBoard_ToPersonal_ClearsTeamId()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        // Assign board to team first
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        await _service.TransferBoardAsync(board.Id, null, _caller);

        var updated = _db.Boards.First(b => b.Id == board.Id);
        Assert.IsNull(updated.TeamId);
    }

    [TestMethod]
    public async Task TransferBoard_NonOwner_Throws()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());

        // Caller is not a member
        var ex = await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.TransferBoardAsync(board.Id, Guid.NewGuid(), _caller));
        Assert.IsTrue(ex.Errors.ContainsKey(ErrorCodes.NotBoardMember));
    }

    // ─── Effective Board Role ─────────────────────────────────────────

    [TestMethod]
    public async Task GetEffectiveBoardRole_DirectOnly_ReturnsDirect()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Owner, role);
    }

    [TestMethod]
    public async Task GetEffectiveBoardRole_TeamOwner_MapsToBoardOwner()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());

        // Make it a team board
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        // User is a Tracks team Owner (but not a direct board member)
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Owner);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Owner, role);
    }

    [TestMethod]
    public async Task GetEffectiveBoardRole_TeamMember_MapsToBoardMember()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Member);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Member, role);
    }

    [TestMethod]
    public async Task GetEffectiveBoardRole_DirectHigherThanTeam_ReturnsHigher()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        // Direct: Admin; Team: Member → should return Admin
        await TestHelpers.AddMemberAsync(_db, board.Id, _caller.UserId, BoardMemberRole.Admin);
        await SeedTeamRoleAsync(teamId, _caller.UserId, TracksTeamMemberRole.Member);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Admin, role);
    }

    [TestMethod]
    public async Task GetEffectiveBoardRole_CoreMemberNoTracksRole_GetsDefaultMember()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        // User is a core team member but has no Tracks role
        _teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.AreEqual(BoardMemberRole.Member, role);
    }

    [TestMethod]
    public async Task GetEffectiveBoardRole_NotMemberAtAll_ReturnsNull()
    {
        var teamId = Guid.NewGuid();
        var board = await TestHelpers.SeedBoardAsync(_db, Guid.NewGuid());
        board.TeamId = teamId;
        await _db.SaveChangesAsync();

        _teamDirectoryMock
            .Setup(d => d.IsTeamMemberAsync(teamId, _caller.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var role = await _service.GetEffectiveBoardRoleAsync(board.Id, _caller.UserId);

        Assert.IsNull(role);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private async Task SeedTeamRoleAsync(Guid coreTeamId, Guid userId, TracksTeamMemberRole role)
    {
        _db.TeamRoles.Add(new TeamRole
        {
            CoreTeamId = coreTeamId,
            UserId = userId,
            Role = role,
            AssignedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static TeamInfo MakeTeamInfo(Guid id, string name, string? description = null)
        => new()
        {
            Id = id,
            OrganizationId = OrgId,
            Name = name,
            Description = description,
            MemberCount = 1,
            CreatedAt = DateTime.UtcNow
        };
}
