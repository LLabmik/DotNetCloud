using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests for <see cref="VCardService"/>.
/// </summary>
[TestClass]
public class VCardServiceTests
{
    private ContactsDbContext _db = null!;
    private VCardService _vcardService = null!;
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
        _contactService = new ContactService(_db, _eventBusMock.Object, NullLogger<ContactService>.Instance);
        _vcardService = new VCardService(_db, _contactService, NullLogger<VCardService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task ExportVCard_PersonContact_ContainsVCardFields()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto
            {
                ContactType = ContactType.Person,
                DisplayName = "Jane Doe",
                FirstName = "Jane",
                LastName = "Doe",
                Organization = "Acme Corp",
                JobTitle = "Engineer",
                Emails = [new ContactEmailDto { Address = "jane@acme.com", Label = "work" }],
                PhoneNumbers = [new ContactPhoneDto { Number = "+1234567890", Label = "mobile" }]
            },
            _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("BEGIN:VCARD"));
        Assert.IsTrue(vcard.Contains("END:VCARD"));
        Assert.IsTrue(vcard.Contains("FN:Jane Doe"));
        Assert.IsTrue(vcard.Contains("N:Doe;Jane"));
        Assert.IsTrue(vcard.Contains("ORG:Acme Corp"));
        Assert.IsTrue(vcard.Contains("TITLE:Engineer"));
        Assert.IsTrue(vcard.Contains("jane@acme.com"));
        Assert.IsTrue(vcard.Contains("+1234567890"));
    }

    [TestMethod]
    public async Task ImportVCards_ValidVCard_CreatesContact()
    {
        var vcardText = """
            BEGIN:VCARD
            VERSION:3.0
            FN:John Smith
            N:Smith;John;;;
            ORG:Test Corp
            EMAIL:john@test.com
            TEL:+9876543210
            END:VCARD
            """;

        var ids = await _vcardService.ImportVCardsAsync(vcardText, _caller);

        Assert.AreEqual(1, ids.Count);

        var imported = await _contactService.GetContactAsync(ids[0], _caller);
        Assert.IsNotNull(imported);
        Assert.AreEqual("John Smith", imported.DisplayName);
        Assert.AreEqual("John", imported.FirstName);
        Assert.AreEqual("Smith", imported.LastName);
        Assert.AreEqual("Test Corp", imported.Organization);
    }

    [TestMethod]
    public async Task ImportVCards_MultipleVCards_CreatesAll()
    {
        var vcardText = """
            BEGIN:VCARD
            VERSION:3.0
            FN:Contact One
            N:One;Contact;;;
            END:VCARD
            BEGIN:VCARD
            VERSION:3.0
            FN:Contact Two
            N:Two;Contact;;;
            END:VCARD
            """;

        var ids = await _vcardService.ImportVCardsAsync(vcardText, _caller);

        Assert.AreEqual(2, ids.Count);
    }

    [TestMethod]
    public async Task ExportAllVCards_ReturnsAllContacts()
    {
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "First" }, _caller);
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Second" }, _caller);

        var vcard = await _vcardService.ExportAllVCardsAsync(_caller);

        var beginCount = vcard.Split("BEGIN:VCARD").Length - 1;
        Assert.AreEqual(2, beginCount);
    }

    [TestMethod]
    public async Task ImportVCards_WithAddress_ParsesAddress()
    {
        var vcardText = """
            BEGIN:VCARD
            VERSION:3.0
            FN:Address Person
            N:Person;Address;;;
            ADR;TYPE=HOME:;;123 Main St;Springfield;IL;62704;US
            END:VCARD
            """;

        var ids = await _vcardService.ImportVCardsAsync(vcardText, _caller);
        var contact = await _contactService.GetContactAsync(ids[0], _caller);

        Assert.IsNotNull(contact);
        Assert.AreEqual(1, contact.Addresses.Count);
        Assert.AreEqual("123 Main St", contact.Addresses[0].Street);
        Assert.AreEqual("Springfield", contact.Addresses[0].City);
    }
}
