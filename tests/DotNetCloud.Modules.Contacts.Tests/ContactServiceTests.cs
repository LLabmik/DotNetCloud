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
/// Tests for <see cref="ContactService"/>.
/// </summary>
[TestClass]
public class ContactServiceTests
{
    private ContactsDbContext _db = null!;
    private ContactService _service = null!;
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
        _service = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task CreateContact_ValidDto_ReturnsContact()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Jane Doe",
            FirstName = "Jane",
            LastName = "Doe"
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Jane Doe", result.DisplayName);
        Assert.AreEqual("Jane", result.FirstName);
        Assert.AreEqual("Doe", result.LastName);
        Assert.AreEqual(ContactType.Person, result.ContactType);
        Assert.AreEqual(_caller.UserId, result.OwnerId);
    }

    [TestMethod]
    public async Task CreateContact_WithEmails_StoresEmails()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Test User",
            Emails =
            [
                new ContactEmailDto { Address = "test@example.com", Label = "work", IsPrimary = true },
                new ContactEmailDto { Address = "home@example.com", Label = "home" }
            ]
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        Assert.AreEqual(2, result.Emails.Count);
        Assert.IsTrue(result.Emails.Any(e => e.Address == "test@example.com" && e.IsPrimary));
    }

    [TestMethod]
    public async Task CreateContact_PublishesEvent()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Event Test"
        };

        await _service.CreateContactAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<Core.Events.ContactCreatedEvent>(e => e.DisplayName == "Event Test"),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateContact_EmptyDisplayName_StillCreates()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = ""
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("", result.DisplayName);
    }

    [TestMethod]
    public async Task GetContact_Exists_ReturnsContact()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Lookup Test"
        };
        var created = await _service.CreateContactAsync(dto, _caller);

        var result = await _service.GetContactAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Lookup Test", result.DisplayName);
    }

    [TestMethod]
    public async Task GetContact_NotFound_ReturnsNull()
    {
        var result = await _service.GetContactAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateContact_ChangesDisplayName()
    {
        var createDto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Original Name"
        };
        var created = await _service.CreateContactAsync(createDto, _caller);

        var updateDto = new UpdateContactDto { DisplayName = "Updated Name" };
        var updated = await _service.UpdateContactAsync(created.Id, updateDto, _caller);

        Assert.AreEqual("Updated Name", updated.DisplayName);
    }

    [TestMethod]
    public async Task DeleteContact_SoftDeletes()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Delete Me"
        };
        var created = await _service.CreateContactAsync(dto, _caller);

        await _service.DeleteContactAsync(created.Id, _caller);

        // Soft-deleted contacts are excluded by query filter
        var result = await _service.GetContactAsync(created.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ListContacts_ReturnsOwnContacts()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Mine" }, _caller);
        await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Theirs" }, otherCaller);

        var results = await _service.ListContactsAsync(_caller);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Mine", results[0].DisplayName);
    }

    [TestMethod]
    public async Task ListContacts_SearchFilter_MatchesDisplayName()
    {
        await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Alice Smith" }, _caller);
        await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Bob Jones" }, _caller);

        var results = await _service.ListContactsAsync(_caller, search: "Alice");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("Alice Smith", results[0].DisplayName);
    }

    [TestMethod]
    public async Task CreateContact_WithPhones_StoresPhones()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Phone Test",
            PhoneNumbers =
            [
                new ContactPhoneDto { Number = "+1234567890", Label = "mobile", IsPrimary = true }
            ]
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        Assert.AreEqual(1, result.PhoneNumbers.Count);
        Assert.AreEqual("+1234567890", result.PhoneNumbers[0].Number);
    }

    [TestMethod]
    public async Task CreateContact_WithAddresses_StoresAddresses()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Address Test",
            Addresses =
            [
                new ContactAddressDto
                {
                    Label = "home",
                    Street = "123 Main St",
                    City = "Springfield",
                    Region = "IL",
                    PostalCode = "62704",
                    Country = "US"
                }
            ]
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        Assert.AreEqual(1, result.Addresses.Count);
        Assert.AreEqual("123 Main St", result.Addresses[0].Street);
        Assert.AreEqual("Springfield", result.Addresses[0].City);
    }

    [TestMethod]
    public async Task GetContactsByIds_ReturnsMatchingContacts()
    {
        var c1 = await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Contact 1" }, _caller);
        var c2 = await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Contact 2" }, _caller);
        await _service.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Contact 3" }, _caller);

        var results = await _service.GetContactsByIdsAsync([c1.Id, c2.Id], _caller);

        Assert.AreEqual(2, results.Count);
    }
}
