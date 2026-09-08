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
/// Tests for <see cref="ContactShareService.ListSharedWithMeAsync"/> — the user + team "shared
/// with me" query that feeds aggregated surfaces such as the Files shared-with-me tree.
/// </summary>
[TestClass]
public class ContactShareServiceSharedWithMeTests
{
    private ContactsDbContext _db = null!;
    private ContactShareService _shareService = null!;
    private ContactService _contactService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _recipient = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _contactService = new ContactService(_db, _eventBusMock.Object, Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(), NullLogger<ContactService>.Instance);
        _shareService = new ContactShareService(_db, _eventBusMock.Object, NullLogger<ContactShareService>.Instance);
        _owner = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        _recipient = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task ListSharedWithMe_UserShare_ReturnsSharedContactWithDisplayName()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Jane Doe" }, _owner);
        await _shareService.ShareContactAsync(
            contact.Id, _recipient.UserId, null, ContactSharePermission.ReadOnly, _owner);

        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual(contact.Id, item.ContactId);
        Assert.AreEqual("Jane Doe", item.DisplayName);
        Assert.AreEqual(_owner.UserId, item.SharedByUserId);
        Assert.AreEqual(_recipient.UserId, item.SharedWithUserId);
        Assert.IsNull(item.SharedWithTeamId);
    }

    [TestMethod]
    public async Task ListSharedWithMe_TeamShare_ReturnsContactForTeamMember()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Team Contact" }, _owner);
        var teamId = Guid.CreateVersion7();
        await _shareService.ShareContactAsync(
            contact.Id, null, teamId, ContactSharePermission.ReadWrite, _owner);

        // Recipient is a member of the target team.
        var teamDirectory = new Mock<DotNetCloud.Core.Capabilities.ITeamDirectory>();
        teamDirectory
            .Setup(x => x.GetTeamsForUserAsync(_recipient.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DotNetCloud.Core.Capabilities.TeamInfo
                {
                    Id = teamId,
                    OrganizationId = Guid.CreateVersion7(),
                    Name = "Eng",
                    MemberCount = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            });
        var shareService = new ContactShareService(_db, _eventBusMock.Object, NullLogger<ContactShareService>.Instance, teamDirectory.Object);

        var items = await shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual(contact.Id, item.ContactId);
        Assert.AreEqual("Team Contact", item.DisplayName);
        Assert.AreEqual(teamId, item.SharedWithTeamId);
    }

    [TestMethod]
    public async Task ListSharedWithMe_NotTargetedUser_ReturnsEmpty()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Private" }, _owner);
        await _shareService.ShareContactAsync(
            contact.Id, Guid.CreateVersion7(), null, ContactSharePermission.ReadOnly, _owner);

        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task ListSharedWithMe_ExpiredShare_Excluded()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Temporary" }, _owner);

        _db.ContactShares.Add(new ContactShare
        {
            ContactId = contact.Id,
            SharedByUserId = _owner.UserId,
            SharedWithUserId = _recipient.UserId,
            Permission = ContactSharePermission.ReadOnly,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task ListSharedWithMe_NoShares_ReturnsEmpty()
    {
        var items = await _shareService.ListSharedWithMeAsync(_recipient);

        Assert.AreEqual(0, items.Count);
    }
}
