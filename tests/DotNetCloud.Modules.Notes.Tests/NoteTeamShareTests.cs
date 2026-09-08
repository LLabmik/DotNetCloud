using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using DotNetCloud.Modules.Notes.Models;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Tests for team-targeted note shares: create/list/revoke via the share service and
/// team-membership read/edit access through <see cref="NoteService"/>.
/// </summary>
[TestClass]
public class NoteTeamShareTests
{
    private NotesDbContext _db = default!;
    private NoteShareService _shareService = default!;
    private CallerContext _owner = default!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new NotesDbContext(options);
        var eventBusMock = new Mock<IEventBus>();
        _shareService = new NoteShareService(_db, eventBusMock.Object, NullLogger<NoteShareService>.Instance);
        _owner = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>Builds a NoteService with a team directory that maps a member to a team.</summary>
    private (NoteService Service, CallerContext Member, Guid TeamId) CreateServiceWithMember(Guid? memberId = null, Guid? teamId = null)
    {
        var team = teamId ?? Guid.CreateVersion7();
        var member = new CallerContext(memberId ?? Guid.CreateVersion7(), ["user"], CallerType.User);

        var teamDirectory = new Mock<DotNetCloud.Core.Capabilities.ITeamDirectory>();
        // Default for everyone else: no memberships. Must be registered before the
        // member-specific setup so the more specific setup (registered last) wins.
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DotNetCloud.Core.Capabilities.TeamInfo>());
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(member.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DotNetCloud.Core.Capabilities.TeamInfo
                {
                    Id = team,
                    OrganizationId = Guid.CreateVersion7(),
                    Name = "Eng",
                    MemberCount = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            });

        var service = new NoteService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<NoteService>.Instance,
            teamDirectory.Object);

        return (service, member, team);
    }

    private async Task<NoteDto> CreateOwnedNoteAsync(string title = "Team note")
        => await new NoteService(
                _db,
                new Mock<IEventBus>().Object,
                Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
                NullLogger<NoteService>.Instance)
            .CreateNoteAsync(new CreateNoteDto { Title = title }, _owner);

    // ─── Share service ────────────────────────────────────────────────

    [TestMethod]
    public async Task ShareNote_TeamTarget_CreatesTeamShare()
    {
        var note = await CreateOwnedNoteAsync();
        var teamId = Guid.CreateVersion7();

        var share = await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.AreEqual(Guid.Empty, share.SharedWithUserId);
        Assert.AreEqual(NoteSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareNote_TeamDedupe_UpdatesPermission()
    {
        var note = await CreateOwnedNoteAsync();
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);
        var updated = await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadWrite, _owner);

        Assert.AreEqual(teamId, updated.SharedWithTeamId);
        Assert.AreEqual(NoteSharePermission.ReadWrite, updated.Permission);

        var shares = await _shareService.ListSharesAsync(note.Id, _owner);
        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task ShareNote_NoTarget_Throws()
    {
        var note = await CreateOwnedNoteAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareNoteAsync(note.Id, null, null, NoteSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareNote_BothTargets_Throws()
    {
        var note = await CreateOwnedNoteAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareNoteAsync(note.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), NoteSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task RemoveShare_TeamShare_OwnerCanRevoke()
    {
        var note = await CreateOwnedNoteAsync();
        var teamId = Guid.CreateVersion7();
        var share = await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(note.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    // ─── Read / edit access via membership ────────────────────────────

    [TestMethod]
    public async Task GetNoteAsync_TeamMember_CanReadTeamSharedNote()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        var result = await service.GetNoteAsync(note.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(note.Id, result.Id);
    }

    [TestMethod]
    public async Task GetNoteAsync_NonMember_CannotReadTeamSharedNote()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        // A caller with no team memberships must not see the note.
        var stranger = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        var result = await service.GetNoteAsync(note.Id, stranger);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListNotesAsync_TeamMember_IncludesTeamSharedNote()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        var notes = await service.ListNotesAsync(member);

        Assert.IsTrue(notes.Any(n => n.Id == note.Id));
    }

    [TestMethod]
    public async Task UpdateNoteAsync_ReadWriteTeamMember_CanUpdate()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadWrite, _owner);

        var updated = await service.UpdateNoteAsync(note.Id, new UpdateNoteDto { Content = "edited" }, member);

        Assert.AreEqual("edited", updated.Content);
    }

    [TestMethod]
    public async Task UpdateNoteAsync_ReadOnlyTeamMember_CannotUpdate()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => service.UpdateNoteAsync(note.Id, new UpdateNoteDto { Content = "nope" }, member));
    }

    // ─── ViewerPermission (read-only vs editable affordance) ───────────────

    [TestMethod]
    public async Task GetNoteAsync_Owner_ViewerPermissionIsNull()
    {
        var note = await CreateOwnedNoteAsync();
        var ownerService = new NoteService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<NoteService>.Instance);

        var result = await ownerService.GetNoteAsync(note.Id, _owner);

        Assert.IsNotNull(result);
        Assert.IsNull(result.ViewerPermission);
    }

    [TestMethod]
    public async Task GetNoteAsync_ReadOnlyViewer_ExposesReadOnlyPermission()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, _) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, member.UserId, NoteSharePermission.ReadOnly, _owner);

        var result = await service.GetNoteAsync(note.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(NoteSharePermission.ReadOnly, result.ViewerPermission);
    }

    [TestMethod]
    public async Task GetNoteAsync_ReadWriteViewer_ExposesReadWritePermission()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, _) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, member.UserId, NoteSharePermission.ReadWrite, _owner);

        var result = await service.GetNoteAsync(note.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(NoteSharePermission.ReadWrite, result.ViewerPermission);
    }

    [TestMethod]
    public async Task ListNotesAsync_TeamReadWriteMember_ExposesReadWritePermission()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadWrite, _owner);

        var notes = await service.ListNotesAsync(member);

        var result = notes.Single(n => n.Id == note.Id);
        Assert.AreEqual(NoteSharePermission.ReadWrite, result.ViewerPermission);
    }

    [TestMethod]
    public async Task ListNotesAsync_TeamReadOnlyMember_ExposesReadOnlyPermission()
    {
        var note = await CreateOwnedNoteAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareNoteAsync(note.Id, null, teamId, NoteSharePermission.ReadOnly, _owner);

        var notes = await service.ListNotesAsync(member);

        var result = notes.Single(n => n.Id == note.Id);
        Assert.AreEqual(NoteSharePermission.ReadOnly, result.ViewerPermission);
    }
}
