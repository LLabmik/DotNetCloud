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

[TestClass]
public class ContactsImportProviderTests
{
    private ContactsDbContext _db = null!;
    private ContactService _contactService = null!;
    private VCardService _vcardService = null!;
    private ContactsImportProvider _provider = null!;
    private CallerContext _caller = null!;
    private Mock<IEventBus> _eventBusMock = null!;

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
        _provider = new ContactsImportProvider(
            _vcardService, _contactService, NullLogger<ContactsImportProvider>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public void DataType_IsContacts()
    {
        Assert.AreEqual(ImportDataType.Contacts, _provider.DataType);
    }

    [TestMethod]
    public async Task PreviewAsync_ValidVCards_ReturnsDryRunReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = TwoContactVCards,
            DryRun = true
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        Assert.AreEqual(2, report.TotalItems);
        Assert.AreEqual(2, report.SuccessCount);
        Assert.AreEqual(0, report.FailedCount);
        Assert.AreEqual(ImportDataType.Contacts, report.DataType);
    }

    [TestMethod]
    public async Task PreviewAsync_DoesNotPersistRecords()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = TwoContactVCards,
            DryRun = true
        };

        await _provider.PreviewAsync(request, _caller);

        var dbCount = await _db.Contacts.CountAsync();
        Assert.AreEqual(0, dbCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidVCards_CreatesContacts()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = TwoContactVCards
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsFalse(report.IsDryRun);
        Assert.AreEqual(2, report.TotalItems);
        Assert.AreEqual(2, report.SuccessCount);

        var dbCount = await _db.Contacts.CountAsync();
        Assert.AreEqual(2, dbCount);

        // Verify record IDs are set
        foreach (var item in report.Items)
        {
            Assert.IsNotNull(item.RecordId);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRunFlag_DoesNotPersist()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = TwoContactVCards,
            DryRun = true
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.IsDryRun);
        var dbCount = await _db.Contacts.CountAsync();
        Assert.AreEqual(0, dbCount);
    }

    [TestMethod]
    public async Task PreviewAsync_EmptyData_ReturnsEmptyReport()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = ""
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(0, report.TotalItems);
        Assert.IsTrue(report.IsDryRun);
    }

    [TestMethod]
    public async Task PreviewAsync_MissingDisplayName_ReportsFailed()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = "BEGIN:VCARD\nVERSION:3.0\nN:;;\nEND:VCARD"
        };

        var report = await _provider.PreviewAsync(request, _caller);

        Assert.AreEqual(1, report.TotalItems);
        Assert.AreEqual(1, report.FailedCount);
        Assert.AreEqual(0, report.SuccessCount);
        Assert.IsTrue(report.Items[0].Message!.Contains("display name", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ExecuteAsync_ContactWithEmail_ImportsWithEmail()
    {
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = "BEGIN:VCARD\nVERSION:3.0\nFN:Jane Doe\nEMAIL;TYPE=WORK:jane@example.com\nEND:VCARD"
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.AreEqual(1, report.SuccessCount);
        var contact = await _db.Contacts.Include(c => c.Emails).FirstAsync();
        Assert.AreEqual("Jane Doe", contact.DisplayName);
        Assert.AreEqual(1, contact.Emails.Count);
        Assert.AreEqual("jane@example.com", contact.Emails.First().Address);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportContainsTimestamps()
    {
        var before = DateTime.UtcNow;
        var request = new ImportRequest
        {
            DataType = ImportDataType.Contacts,
            Data = "BEGIN:VCARD\nVERSION:3.0\nFN:Test\nEND:VCARD"
        };

        var report = await _provider.ExecuteAsync(request, _caller);

        Assert.IsTrue(report.StartedAtUtc >= before);
        Assert.IsTrue(report.CompletedAtUtc >= report.StartedAtUtc);
    }

    [TestMethod]
    public void ParseVCards_MultipleContacts_ParsesAll()
    {
        var parsed = ContactsImportProvider.ParseVCards(TwoContactVCards);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual("John Smith", parsed[0].DisplayName);
        Assert.AreEqual("Jane Doe", parsed[1].DisplayName);
    }

    [TestMethod]
    public void ParseVCards_WithAddress_ParsesAddressFields()
    {
        var vcard = "BEGIN:VCARD\nVERSION:3.0\nFN:Test Person\nADR;TYPE=HOME:;;123 Main St;Springfield;IL;62701;US\nEND:VCARD";
        var parsed = ContactsImportProvider.ParseVCards(vcard);

        Assert.AreEqual(1, parsed.Count);
        Assert.AreEqual(1, parsed[0].Addresses.Count);
        Assert.AreEqual("123 Main St", parsed[0].Addresses[0].Street);
        Assert.AreEqual("Springfield", parsed[0].Addresses[0].City);
        Assert.AreEqual("US", parsed[0].Addresses[0].Country);
    }

    [TestMethod]
    public void ParseVCards_WithOrganization_ParsesOrgAndDept()
    {
        var vcard = "BEGIN:VCARD\nVERSION:3.0\nFN:Test\nORG:Acme Corp;Engineering\nTITLE:Developer\nEND:VCARD";
        var parsed = ContactsImportProvider.ParseVCards(vcard);

        Assert.AreEqual("Acme Corp", parsed[0].Organization);
        Assert.AreEqual("Engineering", parsed[0].Department);
        Assert.AreEqual("Developer", parsed[0].JobTitle);
    }

    private const string TwoContactVCards = """
        BEGIN:VCARD
        VERSION:3.0
        FN:John Smith
        N:Smith;John;;;
        EMAIL;TYPE=WORK:john@example.com
        TEL;TYPE=CELL:+1-555-0100
        END:VCARD
        BEGIN:VCARD
        VERSION:3.0
        FN:Jane Doe
        N:Doe;Jane;;;
        EMAIL;TYPE=HOME:jane@example.com
        END:VCARD
        """;
}
