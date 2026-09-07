using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactService.GetRecentContactsAsync"/>.
/// </summary>
[TestClass]
public class ContactServiceGetRecentContactsTests
{
    private ContactsDbContext _db = null!;
    private ContactService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _service = new ContactService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<DotNetCloud.Core.Capabilities.IAuditLogger>(),
            NullLogger<ContactService>.Instance);
        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<Contact> SeedContactAsync(Guid ownerId, string displayName, DateTime createdAt)
    {
        var contact = new Contact
        {
            OwnerId = ownerId,
            DisplayName = displayName,
            CreatedAt = createdAt
        };
        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync();
        return contact;
    }

    private async Task SeedShareAsync(Guid contactId, Guid sharedByUserId, Guid sharedWithUserId)
    {
        _db.ContactShares.Add(new ContactShare
        {
            ContactId = contactId,
            SharedByUserId = sharedByUserId,
            SharedWithUserId = sharedWithUserId,
            Permission = ContactSharePermission.ReadOnly
        });
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetRecentContacts_NewestFirst_ReturnsContactsOrderedByCreatedAtDescending()
    {
        var now = DateTime.UtcNow;

        await SeedContactAsync(_caller.UserId, "Oldest", now.AddDays(-3));
        await SeedContactAsync(_caller.UserId, "Middle", now.AddDays(-2));
        await SeedContactAsync(_caller.UserId, "Newest", now.AddDays(-1));

        var result = await _service.GetRecentContactsAsync(_caller);

        CollectionAssert.AreEqual(
            new[] { "Newest", "Middle", "Oldest" },
            result.Select(c => c.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task GetRecentContacts_RespectsCount_ReturnsOnlyRequestedNumberOfNewest()
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
            await SeedContactAsync(_caller.UserId, $"Contact{i}", now.AddMinutes(i));

        var result = await _service.GetRecentContactsAsync(_caller, count: 2);

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEqual(
            new[] { "Contact4", "Contact3" },
            result.Select(c => c.DisplayName).ToArray());
    }

    [TestMethod]
    public async Task GetRecentContacts_OwnedOrShared_IncludesOwnedAndSharedButNotUnshared()
    {
        var now = DateTime.UtcNow;
        var otherOwner = Guid.CreateVersion7();

        await SeedContactAsync(_caller.UserId, "Mine", now);
        var shared = await SeedContactAsync(otherOwner, "Shared With Me", now.AddDays(-1));
        await SeedShareAsync(shared.Id, otherOwner, _caller.UserId);
        await SeedContactAsync(otherOwner, "Not Shared", now.AddDays(-2));

        var result = await _service.GetRecentContactsAsync(_caller);

        CollectionAssert.AreEquivalent(
            new[] { "Mine", "Shared With Me" },
            result.Select(c => c.DisplayName).ToArray());
        Assert.IsFalse(result.Any(c => c.DisplayName == "Not Shared"));
    }

    [TestMethod]
    public async Task GetRecentContacts_NoContacts_ReturnsEmptyList()
    {
        var result = await _service.GetRecentContactsAsync(_caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
