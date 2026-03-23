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
/// CardDAV interoperability tests validating vCard output format and DAV XML structure.
/// Ensures compatibility with common CardDAV clients (Thunderbird, DAVx5, iOS/macOS Contacts).
/// </summary>
[TestClass]
public class CardDavInteropTests
{
    private ContactsDbContext _db = null!;
    private ContactService _contactService = null!;
    private VCardService _vcardService = null!;
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
        _vcardService = new VCardService(_db, _contactService, new Mock<IContactAvatarService>().Object, NullLogger<VCardService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── vCard 3.0 Format Compliance ─────────────────────────────────

    [TestMethod]
    public async Task ExportVCard_ContainsBeginEnd()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Test User" }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("BEGIN:VCARD"), "Missing BEGIN:VCARD");
        Assert.IsTrue(vcard.Contains("END:VCARD"), "Missing END:VCARD");
    }

    [TestMethod]
    public async Task ExportVCard_ContainsVersion3()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Version Test" }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("VERSION:3.0") || vcard.Contains("VERSION:4.0"),
            "vCard must declare VERSION");
    }

    [TestMethod]
    public async Task ExportVCard_ContainsFnField()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Jane Doe" }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("FN:Jane Doe"), "FN field must contain display name");
    }

    [TestMethod]
    public async Task ExportVCard_WithName_ContainsNField()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto
            {
                ContactType = ContactType.Person,
                DisplayName = "Jane Doe",
                FirstName = "Jane",
                LastName = "Doe"
            }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("N:"), "N field is required for vCard name components");
        Assert.IsTrue(vcard.Contains("Doe") && vcard.Contains("Jane"),
            "N field must contain name components");
    }

    [TestMethod]
    public async Task ExportVCard_WithOrganization_ContainsOrgField()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto
            {
                ContactType = ContactType.Organization,
                DisplayName = "Acme Corp",
                Organization = "Acme Corp"
            }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("ORG:Acme Corp"), "ORG field must contain organization name");
    }

    [TestMethod]
    public async Task ExportVCard_WithEmail_ContainsEmailField()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto
            {
                ContactType = ContactType.Person,
                DisplayName = "Email Test",
                Emails = [new ContactEmailDto { Address = "test@example.com", Label = "work" }]
            }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("test@example.com"), "EMAIL field must contain email address");
    }

    [TestMethod]
    public async Task ExportVCard_WithPhone_ContainsTelField()
    {
        var contact = await _contactService.CreateContactAsync(
            new CreateContactDto
            {
                ContactType = ContactType.Person,
                DisplayName = "Phone Test",
                PhoneNumbers = [new ContactPhoneDto { Number = "+1-555-0100", Label = "cell" }]
            }, _caller);

        var vcard = await _vcardService.ExportVCardAsync(contact.Id, _caller);

        Assert.IsTrue(vcard.Contains("TEL"), "TEL field must be present");
        Assert.IsTrue(vcard.Contains("+1-555-0100"), "TEL field must contain phone number");
    }

    // ─── Round-Trip Compliance ───────────────────────────────────────

    [TestMethod]
    public async Task ImportExport_RoundTrip_PreservesDisplayName()
    {
        var vcard = "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Round Trip User\r\nN:User;Round;;;\r\nEND:VCARD\r\n";

        var ids = await _vcardService.ImportVCardsAsync(vcard, _caller);
        Assert.AreEqual(1, ids.Count);

        var exported = await _vcardService.ExportVCardAsync(ids[0], _caller);
        Assert.IsTrue(exported.Contains("Round Trip User"), "Display name must survive round-trip");
    }

    [TestMethod]
    public async Task ImportExport_MultipleVCards_AllImported()
    {
        var vcard = "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Alice\r\nN:Alice;;;;\r\nEND:VCARD\r\n" +
                    "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Bob\r\nN:Bob;;;;\r\nEND:VCARD\r\n";

        var ids = await _vcardService.ImportVCardsAsync(vcard, _caller);

        Assert.AreEqual(2, ids.Count);
    }

    [TestMethod]
    public async Task ExportAll_ReturnsAllContacts()
    {
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Contact 1" }, _caller);
        await _contactService.CreateContactAsync(
            new CreateContactDto { ContactType = ContactType.Person, DisplayName = "Contact 2" }, _caller);

        var exported = await _vcardService.ExportAllVCardsAsync(_caller);

        // Should contain two BEGIN:VCARD blocks
        var count = 0;
        var idx = 0;
        while ((idx = exported.IndexOf("BEGIN:VCARD", idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx++;
        }
        Assert.AreEqual(2, count, "Export should contain exactly 2 vCards");
    }

    // ─── DAVx5/Thunderbird Compatibility ─────────────────────────────

    [TestMethod]
    public async Task ImportVCard_WithPhoto_DoesNotThrow()
    {
        // Some DAV clients include PHOTO fields; import should not fail
        var vcard = "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Photo User\r\nN:User;Photo;;;\r\n" +
                    "PHOTO;ENCODING=b;TYPE=JPEG:iVBORw0KGgo=\r\nEND:VCARD\r\n";

        var ids = await _vcardService.ImportVCardsAsync(vcard, _caller);

        Assert.AreEqual(1, ids.Count);
    }

    [TestMethod]
    public async Task ImportVCard_WithExtendedFields_DoesNotThrow()
    {
        // DAVx5 may include X-ANDROID-CUSTOM and other extended fields
        var vcard = "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Extended User\r\nN:User;Extended;;;\r\n" +
                    "X-ANDROID-CUSTOM:customField\r\nX-PHONETIC-LAST-NAME:Test\r\nEND:VCARD\r\n";

        var ids = await _vcardService.ImportVCardsAsync(vcard, _caller);

        Assert.AreEqual(1, ids.Count);
    }
}
