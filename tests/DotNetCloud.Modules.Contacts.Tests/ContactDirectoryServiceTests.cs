using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="ContactDirectoryService"/>.
/// </summary>
[TestClass]
public class ContactDirectoryServiceTests
{
    private ContactsDbContext _db = null!;
    private ContactDirectoryService _service = null!;
    private Guid _userId;
    private Guid _otherUserId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContactsDbContext(options);
        _service = new ContactDirectoryService(_db);
        _userId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();

        // Seed: contact "Alice Johnson" with work/home emails
        var alice = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerId = _userId,
            DisplayName = "Alice Johnson",
            FirstName = "Alice",
            LastName = "Johnson",
            ContactType = Core.DTOs.ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "alice@work.com", Label = "work", IsPrimary = true, SortOrder = 0 },
                new ContactEmail { Address = "alice@home.net", Label = "home", SortOrder = 1 }
            ]
        };

        // Seed: contact "Bob Smith" with only work email
        var bob = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerId = _userId,
            DisplayName = "Bob Smith",
            FirstName = "Bob",
            LastName = "Smith",
            ContactType = Core.DTOs.ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "bob.smith@corp.com", Label = "work", IsPrimary = true, SortOrder = 0 }
            ]
        };

        // Seed: contact owned by another user (should not appear in results)
        var otherUserContact = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerId = _otherUserId,
            DisplayName = "Charlie Brown",
            FirstName = "Charlie",
            LastName = "Brown",
            ContactType = Core.DTOs.ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "charlie@other.com", Label = "work", IsPrimary = true, SortOrder = 0 }
            ]
        };

        // Seed: contact "Fonda Kimball" with display name containing last name
        var fonda = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerId = _userId,
            DisplayName = "Fonda Kimball",
            FirstName = "Fonda",
            LastName = "Kimball",
            ContactType = Core.DTOs.ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "fonda.kimball@example.com", Label = "work", IsPrimary = true, SortOrder = 0 }
            ]
        };

        // Seed: soft-deleted contact (should not appear in results)
        var deletedContact = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerId = _userId,
            DisplayName = "Deleted User",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            ContactType = Core.DTOs.ContactType.Person,
            Emails =
            [
                new ContactEmail { Address = "deleted@test.com", Label = "work", IsPrimary = true, SortOrder = 0 }
            ]
        };

        _db.Contacts.AddRange(alice, bob, fonda, otherUserContact, deletedContact);
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ── SearchContactsWithEmailsAsync ────────────────────────────

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_EmptyQuery_ReturnsEmpty()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, string.Empty);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_WhitespaceQuery_ReturnsEmpty()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "   ");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByDisplayName_ReturnsMatchingContact()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByDisplayNameSubstring_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "ohn");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByEmailAddress_ReturnsContactWithThatEmail()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "alice@work.com");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByEmailSubstring_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "home");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_CaseInsensitiveDisplayName_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_CaseInsensitiveEmail_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "ALICE@WORK.COM");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_LowercaseNameSubstring_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "kimb");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Fonda Kimball", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_MixedCaseNameSubstring_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "KIMB");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Fonda Kimball", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_ReturnsAllEmailsForContact()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(2, results[0].Emails.Count);

        var addresses = results[0].Emails.Select(e => e.Address).OrderBy(a => a).ToList();
        Assert.AreEqual("alice@home.net", addresses[0]);
        Assert.AreEqual("alice@work.com", addresses[1]);

        var labels = results[0].Emails.ToDictionary(e => e.Address, e => e.Label);
        Assert.AreEqual("work", labels["alice@work.com"]);
        Assert.AreEqual("home", labels["alice@home.net"]);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_NoMatch_ReturnsEmpty()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "zzzzz_nonexistent");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_ExcludesOtherUsersContacts()
    {
        // Search for "Charlie" — should not find other user's contact
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Charlie");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_ExcludesDeletedContacts()
    {
        // Search for "Deleted" — should not find the soft-deleted contact
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Deleted");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_RespectsMaxResults()
    {
        // Add extra contacts to exceed the default limit of 10
        for (var i = 0; i < 15; i++)
        {
            _db.Contacts.Add(new Contact
            {
                Id = Guid.NewGuid(),
                OwnerId = _userId,
                DisplayName = $"Zoe Test Contact {i:D2}",
                ContactType = Core.DTOs.ContactType.Person,
                Emails = [new ContactEmail { Address = $"zoe{i:D2}@test.com", Label = "work", IsPrimary = true, SortOrder = 0 }]
            });
        }
        _db.SaveChanges();

        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Zoe", maxResults: 5);

        Assert.AreEqual(5, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByFirstName_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Bob");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Bob Smith", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsWithEmailsAsync_SearchByLastName_ReturnsMatch()
    {
        var results = await _service.SearchContactsWithEmailsAsync(_userId, "Smith");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Bob Smith", results[0].DisplayName);
    }

    // ── Existing method regression tests ─────────────────────────

    [TestMethod]
    public async Task SearchContactsAsync_EmptyQuery_ReturnsEmpty()
    {
        var results = await _service.SearchContactsAsync(_userId, string.Empty);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchContactsAsync_SearchByDisplayName_ReturnsMatch()
    {
        var results = await _service.SearchContactsAsync(_userId, "Alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsAsync_CaseInsensitiveDisplayName_ReturnsMatch()
    {
        var results = await _service.SearchContactsAsync(_userId, "alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Johnson", results[0].DisplayName);
    }

    [TestMethod]
    public async Task SearchContactsAsync_LowercaseNameSubstring_ReturnsMatch()
    {
        var results = await _service.SearchContactsAsync(_userId, "kimb");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Fonda Kimball", results[0].DisplayName);
    }

    [TestMethod]
    public async Task GetContactDisplayNameAsync_KnownContact_ReturnsName()
    {
        var alice = await _db.Contacts.FirstAsync(c => c.DisplayName == "Alice Johnson");
        var name = await _service.GetContactDisplayNameAsync(alice.Id);

        Assert.AreEqual("Alice Johnson", name);
    }

    [TestMethod]
    public async Task GetContactDisplayNameAsync_UnknownContact_ReturnsNull()
    {
        var name = await _service.GetContactDisplayNameAsync(Guid.NewGuid());

        Assert.IsNull(name);
    }

    [TestMethod]
    public async Task GetContactDisplayNamesAsync_KnownContacts_ReturnsDictionary()
    {
        var alice = await _db.Contacts.FirstAsync(c => c.DisplayName == "Alice Johnson");
        var bob = await _db.Contacts.FirstAsync(c => c.DisplayName == "Bob Smith");

        var names = await _service.GetContactDisplayNamesAsync([alice.Id, bob.Id]);

        Assert.AreEqual(2, names.Count);
        Assert.AreEqual("Alice Johnson", names[alice.Id]);
        Assert.AreEqual("Bob Smith", names[bob.Id]);
    }
}
