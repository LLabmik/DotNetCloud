using System.Diagnostics;
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
/// Performance baseline tests for the Contacts module.
/// Establishes timing thresholds for large dataset operations.
/// </summary>
[TestClass]
public class ContactPerformanceTests
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

    [TestMethod]
    public async Task CreateContacts_500Records_CompletesWithinThreshold()
    {
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 500; i++)
        {
            await _contactService.CreateContactAsync(
                new CreateContactDto
                {
                    ContactType = ContactType.Person,
                    DisplayName = $"Contact {i}",
                    FirstName = $"First{i}",
                    LastName = $"Last{i}"
                }, _caller);
        }

        sw.Stop();
        // Baseline: 500 contact insertions should complete within 30 seconds on InMemory
        Assert.IsTrue(sw.ElapsedMilliseconds < 30_000,
            $"Creating 500 contacts took {sw.ElapsedMilliseconds}ms, expected < 30000ms");
    }

    [TestMethod]
    public async Task ListContacts_LargeList_CompletesWithinThreshold()
    {
        // Seed 200 contacts
        for (var i = 0; i < 200; i++)
        {
            await _contactService.CreateContactAsync(
                new CreateContactDto
                {
                    ContactType = ContactType.Person,
                    DisplayName = $"Contact {i}"
                }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var contacts = await _contactService.ListContactsAsync(_caller, take: 200);

        sw.Stop();
        Assert.AreEqual(200, contacts.Count);
        Assert.IsTrue(sw.ElapsedMilliseconds < 5_000,
            $"Listing 200 contacts took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [TestMethod]
    public async Task SearchContacts_LargeList_CompletesWithinThreshold()
    {
        for (var i = 0; i < 200; i++)
        {
            await _contactService.CreateContactAsync(
                new CreateContactDto
                {
                    ContactType = ContactType.Person,
                    DisplayName = i % 10 == 0 ? $"Searchable {i}" : $"Contact {i}"
                }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var results = await _contactService.ListContactsAsync(_caller, "Searchable");

        sw.Stop();
        Assert.AreEqual(20, results.Count);
        Assert.IsTrue(sw.ElapsedMilliseconds < 5_000,
            $"Searching 200 contacts took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
    }

    [TestMethod]
    public async Task ExportAllVCards_200Contacts_CompletesWithinThreshold()
    {
        for (var i = 0; i < 200; i++)
        {
            await _contactService.CreateContactAsync(
                new CreateContactDto
                {
                    ContactType = ContactType.Person,
                    DisplayName = $"Export Contact {i}",
                    FirstName = $"First{i}",
                    LastName = $"Last{i}"
                }, _caller);
        }

        var sw = Stopwatch.StartNew();

        var vcard = await _vcardService.ExportAllVCardsAsync(_caller);

        sw.Stop();
        Assert.IsTrue(vcard.Contains("BEGIN:VCARD"));
        Assert.IsTrue(sw.ElapsedMilliseconds < 10_000,
            $"Exporting 200 vCards took {sw.ElapsedMilliseconds}ms, expected < 10000ms");
    }
}
