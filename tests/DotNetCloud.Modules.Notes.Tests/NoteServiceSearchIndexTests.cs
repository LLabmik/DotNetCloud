using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Tests that <see cref="NoteService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on create, update, and delete operations.
/// </summary>
[TestClass]
public class NoteServiceSearchIndexTests
{
    private NotesDbContext _db = null!;
    private NoteService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new NotesDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new NoteService(_db, _eventBusMock.Object, NullLogger<NoteService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateNote_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var dto = new CreateNoteDto { Title = "Test Note", Content = "Some content" };

        var result = await _service.CreateNoteAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "notes" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateNote_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "Original", Content = "content" }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.UpdateNoteAsync(created.Id,
            new UpdateNoteDto { Title = "Updated" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "notes" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteNote_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var created = await _service.CreateNoteAsync(
            new CreateNoteDto { Title = "To Delete", Content = "content" }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeleteNoteAsync(created.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "notes" &&
                    e.EntityId == created.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
