using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Contacts.Tests;

/// <summary>
/// Tests that <see cref="ContactService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create, update, and delete operations.
/// </summary>
[TestClass]
public class ContactServiceSearchIndexTests
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
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateContact_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Test Contact",
            FirstName = "Test",
            LastName = "Contact"
        };

        var result = await _service.CreateContactAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "contacts" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateContact_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var createDto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "Original Name",
            FirstName = "Original"
        };
        var created = await _service.CreateContactAsync(createDto, _caller);
        _eventBusMock.Invocations.Clear();

        var updateDto = new UpdateContactDto { DisplayName = "Updated Name" };
        await _service.UpdateContactAsync(created.Id, updateDto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "contacts" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteContact_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var createDto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "To Delete"
        };
        var created = await _service.CreateContactAsync(createDto, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeleteContactAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "contacts" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateContact_SearchIndexEvent_HasValidEventIdAndCreatedAt()
    {
        SearchIndexRequestEvent? capturedEvent = null;
        _eventBusMock
            .Setup(eb => eb.PublishAsync(It.IsAny<SearchIndexRequestEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<object, CallerContext, CancellationToken>((e, _, _) => capturedEvent = e as SearchIndexRequestEvent);

        var dto = new CreateContactDto
        {
            ContactType = ContactType.Person,
            DisplayName = "EventId Test"
        };

        await _service.CreateContactAsync(dto, _caller);

        Assert.IsNotNull(capturedEvent);
        Assert.AreNotEqual(Guid.Empty, capturedEvent.EventId);
        Assert.IsTrue(capturedEvent.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }
}
