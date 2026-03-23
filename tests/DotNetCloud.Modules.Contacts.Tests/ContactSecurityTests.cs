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
/// Security tests for the Contacts module: tenant isolation and authorization boundaries.
/// </summary>
[TestClass]
public class ContactSecurityTests
{
    private ContactsDbContext _db = null!;
    private ContactService _contactService = null!;
    private ContactGroupService _groupService = null!;
    private ContactShareService _shareService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _userA = null!;
    private CallerContext _userB = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _contactService = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _groupService = new ContactGroupService(_db, NullLogger<ContactGroupService>.Instance);
        _shareService = new ContactShareService(_db, _eventBusMock.Object, NullLogger<ContactShareService>.Instance);
        _userA = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _userB = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Tenant Isolation ────────────────────────────────────────────

    [TestMethod]
    public async Task GetContact_OtherUserContact_ReturnsNull()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Private" }, _userA);

        var result = await _contactService.GetContactAsync(contact.Id, _userB);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListContacts_OnlyReturnsOwnContacts()
    {
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "A's Contact" }, _userA);
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "B's Contact" }, _userB);

        var contactsA = await _contactService.ListContactsAsync(_userA);
        var contactsB = await _contactService.ListContactsAsync(_userB);

        Assert.AreEqual(1, contactsA.Count);
        Assert.AreEqual(1, contactsB.Count);
        Assert.AreEqual("A's Contact", contactsA[0].DisplayName);
        Assert.AreEqual("B's Contact", contactsB[0].DisplayName);
    }

    [TestMethod]
    public async Task UpdateContact_OtherUserContact_Throws()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Original" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _contactService.UpdateContactAsync(contact.Id,
                new UpdateContactDto { DisplayName = "Hijacked" }, _userB));
    }

    [TestMethod]
    public async Task DeleteContact_OtherUserContact_Throws()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Protected" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _contactService.DeleteContactAsync(contact.Id, _userB));
    }

    [TestMethod]
    public async Task SearchContacts_OnlyReturnsOwnResults()
    {
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Alice Confidential" }, _userA);
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Bob Confidential" }, _userB);

        var resultsA = await _contactService.ListContactsAsync(_userA, "Confidential");
        var resultsB = await _contactService.ListContactsAsync(_userB, "Confidential");

        Assert.AreEqual(1, resultsA.Count);
        Assert.AreEqual(1, resultsB.Count);
        Assert.IsTrue(resultsA[0].DisplayName.Contains("Alice"));
        Assert.IsTrue(resultsB[0].DisplayName.Contains("Bob"));
    }

    // ─── Group Isolation ─────────────────────────────────────────────

    [TestMethod]
    public async Task ListGroups_OnlyReturnsOwnGroups()
    {
        await _groupService.CreateGroupAsync("A's Group", _userA);
        await _groupService.CreateGroupAsync("B's Group", _userB);

        var groupsA = await _groupService.ListGroupsAsync(_userA);
        var groupsB = await _groupService.ListGroupsAsync(_userB);

        Assert.AreEqual(1, groupsA.Count);
        Assert.AreEqual(1, groupsB.Count);
    }

    // ─── Share Authorization ─────────────────────────────────────────

    [TestMethod]
    public async Task ShareContact_NonOwner_CannotShare()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Guarded" }, _userA);

        await Assert.ThrowsExactlyAsync<Core.Errors.ValidationException>(
            () => _shareService.ShareContactAsync(
                contact.Id, Guid.NewGuid(), null, ContactSharePermission.ReadOnly, _userB));
    }

    [TestMethod]
    public async Task ListShares_NonOwner_ReturnsEmpty()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Shared" }, _userA);
        await _shareService.ShareContactAsync(
            contact.Id, _userB.UserId, null, ContactSharePermission.ReadOnly, _userA);

        var shares = await _shareService.ListSharesAsync(contact.Id, _userB);

        Assert.AreEqual(0, shares.Count);
    }
}
