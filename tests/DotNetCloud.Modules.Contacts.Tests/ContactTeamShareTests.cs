using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for team-targeted contact shares: create/list/revoke via the share service and
/// team-membership read access through <see cref="ContactService"/>.
/// </summary>
[TestClass]
public class ContactTeamShareTests
{
    private ContactsDbContext _db = default!;
    private ContactShareService _shareService = default!;
    private Mock<IEventBus> _eventBusMock = default!;
    private CallerContext _owner = default!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _shareService = new ContactShareService(_db, _eventBusMock.Object, NullLogger<ContactShareService>.Instance);
        _owner = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    /// <summary>Builds a ContactService with a team directory that maps a member to a team.</summary>
    private (ContactService Service, CallerContext Member, Guid TeamId) CreateServiceWithMember(Guid? memberId = null, Guid? teamId = null)
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

        var service = new ContactService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<ContactService>.Instance,
            teamDirectory.Object);

        return (service, member, team);
    }

    private async Task<ContactDto> CreateOwnedContactAsync(string displayName = "Team contact")
        => await new ContactService(
                _db,
                new Mock<IEventBus>().Object,
                Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
                NullLogger<ContactService>.Instance)
            .CreateContactAsync(new CreateContactDto { ContactType = ContactType.Person, DisplayName = displayName }, _owner);

    // ─── Share service ────────────────────────────────────────────────

    [TestMethod]
    public async Task ShareContact_TeamTarget_CreatesTeamShare()
    {
        var contact = await CreateOwnedContactAsync();
        var teamId = Guid.CreateVersion7();

        var share = await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.AreEqual(ContactSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareContact_TeamTarget_PublishesTeamEvent()
    {
        var contact = await CreateOwnedContactAsync();
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<ResourceSharedEvent>(e => e.SharedWithTeamId == teamId && e.SharedWithUserId == null),
                _owner,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ShareContact_TeamDedupe_UpdatesPermission()
    {
        var contact = await CreateOwnedContactAsync();
        var teamId = Guid.CreateVersion7();

        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);
        var updated = await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadWrite, _owner);

        Assert.AreEqual(teamId, updated.SharedWithTeamId);
        Assert.AreEqual(ContactSharePermission.ReadWrite, updated.Permission);

        var shares = await _shareService.ListSharesAsync(contact.Id, _owner);
        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task ShareContact_NoTarget_Throws()
    {
        var contact = await CreateOwnedContactAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareContactAsync(contact.Id, null, null, ContactSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareContact_BothTargets_Throws()
    {
        var contact = await CreateOwnedContactAsync();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareContactAsync(contact.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), ContactSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task RemoveShare_TeamShare_OwnerCanRevoke()
    {
        var contact = await CreateOwnedContactAsync();
        var teamId = Guid.CreateVersion7();
        var share = await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(contact.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    // ─── Read access via membership ───────────────────────────────────

    [TestMethod]
    public async Task GetContactAsync_TeamMember_CanReadTeamSharedContact()
    {
        var contact = await CreateOwnedContactAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        var result = await service.GetContactAsync(contact.Id, member);

        Assert.IsNotNull(result);
        Assert.AreEqual(contact.Id, result.Id);
    }

    [TestMethod]
    public async Task GetContactAsync_NonMember_CannotReadTeamSharedContact()
    {
        var contact = await CreateOwnedContactAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        var stranger = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        var result = await service.GetContactAsync(contact.Id, stranger);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListContactsAsync_TeamMember_IncludesTeamSharedContact()
    {
        var contact = await CreateOwnedContactAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        var contacts = await service.ListContactsAsync(member);

        Assert.IsTrue(contacts.Any(c => c.Id == contact.Id));
    }

    [TestMethod]
    public async Task ListContactsAsync_NonMember_ExcludesTeamSharedContact()
    {
        var contact = await CreateOwnedContactAsync();
        var (service, member, teamId) = CreateServiceWithMember();
        await _shareService.ShareContactAsync(contact.Id, null, teamId, ContactSharePermission.ReadOnly, _owner);

        var stranger = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        var contacts = await service.ListContactsAsync(stranger);

        Assert.IsFalse(contacts.Any(c => c.Id == contact.Id));
    }
}
