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
/// Tests for <see cref="ContactShareService"/>.
/// </summary>
[TestClass]
public class ContactShareServiceTests
{
    private ContactsDbContext _db = null!;
    private ContactShareService _shareService = null!;
    private ContactService _contactService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _owner = null!;
    private CallerContext _otherUser = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _contactService = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _shareService = new ContactShareService(_db, NullLogger<ContactShareService>.Instance);
        _owner = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _otherUser = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task ShareContact_ValidUserShare_ReturnsShare()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Alice" }, _owner);

        var share = await _shareService.ShareContactAsync(
            contact.Id, _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner);

        Assert.IsNotNull(share);
        Assert.AreEqual(contact.Id, share.ContactId);
        Assert.AreEqual(_owner.UserId, share.SharedByUserId);
        Assert.AreEqual(_otherUser.UserId, share.SharedWithUserId);
        Assert.IsNull(share.SharedWithTeamId);
        Assert.AreEqual(ContactSharePermission.ReadOnly, share.Permission);
    }

    [TestMethod]
    public async Task ShareContact_TeamShare_SetsTeamId()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Bob" }, _owner);
        var teamId = Guid.NewGuid();

        var share = await _shareService.ShareContactAsync(
            contact.Id, null, teamId, ContactSharePermission.ReadWrite, _owner);

        Assert.IsNotNull(share);
        Assert.IsNull(share.SharedWithUserId);
        Assert.AreEqual(teamId, share.SharedWithTeamId);
        Assert.AreEqual(ContactSharePermission.ReadWrite, share.Permission);
    }

    [TestMethod]
    public async Task ShareContact_NonExistentContact_ThrowsValidation()
    {
        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareContactAsync(
                Guid.NewGuid(), _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task ShareContact_NotOwner_ThrowsValidation()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Carol" }, _owner);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareContactAsync(
                contact.Id, Guid.NewGuid(), null, ContactSharePermission.ReadOnly, _otherUser));
    }

    [TestMethod]
    public async Task ShareContact_NoUserOrTeam_ThrowsArgument()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Dan" }, _owner);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _shareService.ShareContactAsync(
                contact.Id, null, null, ContactSharePermission.ReadOnly, _owner));
    }

    [TestMethod]
    public async Task RemoveShare_OwnerCanRemove()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Eve" }, _owner);
        var share = await _shareService.ShareContactAsync(
            contact.Id, _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _owner);

        var shares = await _shareService.ListSharesAsync(contact.Id, _owner);
        Assert.AreEqual(0, shares.Count);
    }

    [TestMethod]
    public async Task RemoveShare_NonOwner_DoesNothing()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Frank" }, _owner);
        var share = await _shareService.ShareContactAsync(
            contact.Id, _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner);

        await _shareService.RemoveShareAsync(share.Id, _otherUser);

        var shares = await _shareService.ListSharesAsync(contact.Id, _owner);
        Assert.AreEqual(1, shares.Count);
    }

    [TestMethod]
    public async Task ListShares_ReturnsOnlyOwnShares()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Grace" }, _owner);
        await _shareService.ShareContactAsync(
            contact.Id, _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner);
        await _shareService.ShareContactAsync(
            contact.Id, null, Guid.NewGuid(), ContactSharePermission.ReadWrite, _owner);

        var shares = await _shareService.ListSharesAsync(contact.Id, _owner);

        Assert.AreEqual(2, shares.Count);
    }

    [TestMethod]
    public async Task ListShares_OtherUser_ReturnsEmpty()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Heidi" }, _owner);
        await _shareService.ShareContactAsync(
            contact.Id, _otherUser.UserId, null, ContactSharePermission.ReadOnly, _owner);

        var shares = await _shareService.ListSharesAsync(contact.Id, _otherUser);

        Assert.AreEqual(0, shares.Count);
    }
}
