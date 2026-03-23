using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactGroupService"/>.
/// </summary>
[TestClass]
public class ContactGroupServiceTests
{
    private ContactsDbContext _db = null!;
    private ContactGroupService _groupService = null!;
    private ContactService _contactService = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _groupService = new ContactGroupService(_db, NullLogger<ContactGroupService>.Instance);
        _contactService = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateGroup_ValidName_ReturnsGroup()
    {
        var result = await _groupService.CreateGroupAsync("Family", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Family", result.Name);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateGroup_EmptyName_StillCreates()
    {
        var result = await _groupService.CreateGroupAsync("", _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("", result.Name);
    }

    [TestMethod]
    public async Task CreateGroup_DuplicateName_ThrowsValidation()
    {
        await _groupService.CreateGroupAsync("Work", _caller);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _groupService.CreateGroupAsync("Work", _caller));
    }

    [TestMethod]
    public async Task ListGroups_ReturnsOwnGroups()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        await _groupService.CreateGroupAsync("Mine", _caller);
        await _groupService.CreateGroupAsync("Theirs", otherCaller);

        var results = await _groupService.ListGroupsAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Mine", results[0].Name);
    }

    [TestMethod]
    public async Task RenameGroup_ChangesName()
    {
        var group = await _groupService.CreateGroupAsync("Old Name", _caller);

        var renamed = await _groupService.RenameGroupAsync(group.Id, "New Name", _caller);

        Assert.AreEqual("New Name", renamed.Name);
    }

    [TestMethod]
    public async Task DeleteGroup_SoftDeletes()
    {
        var group = await _groupService.CreateGroupAsync("Delete Me", _caller);

        await _groupService.DeleteGroupAsync(group.Id, _caller);

        var results = await _groupService.ListGroupsAsync(_caller);
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task AddMember_ContactAddedToGroup()
    {
        var group = await _groupService.CreateGroupAsync("Test Group", _caller);
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Member" }, _caller);

        await _groupService.AddContactToGroupAsync(group.Id, contact.Id, _caller);

        var members = await _groupService.ListGroupMembersAsync(group.Id, _caller);
        Assert.AreEqual(1, members.Count);
        Assert.AreEqual("Member", members[0].DisplayName);
    }

    [TestMethod]
    public async Task RemoveMember_ContactRemovedFromGroup()
    {
        var group = await _groupService.CreateGroupAsync("Test Group", _caller);
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Member" }, _caller);
        await _groupService.AddContactToGroupAsync(group.Id, contact.Id, _caller);

        await _groupService.RemoveContactFromGroupAsync(group.Id, contact.Id, _caller);

        var members = await _groupService.ListGroupMembersAsync(group.Id, _caller);
        Assert.AreEqual(0, members.Count);
    }
}
