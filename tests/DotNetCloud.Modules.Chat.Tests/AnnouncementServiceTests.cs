using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="AnnouncementService"/>.
/// </summary>
[TestClass]
public class AnnouncementServiceTests
{
    private ChatDbContext _db = null!;
    private AnnouncementService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _service = new AnnouncementService(_db, NullLogger<AnnouncementService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["admin"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ── Create ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task WhenCreateAnnouncementThenAnnouncementIsReturned()
    {
        var dto = new CreateAnnouncementDto { Title = "Welcome", Content = "Hello everyone" };

        var result = await _service.CreateAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Welcome", result.Title);
        Assert.AreEqual("Hello everyone", result.Content);
        Assert.AreEqual(_caller.UserId, result.AuthorUserId);
    }

    [TestMethod]
    public async Task WhenCreateAnnouncementThenPersistedInDb()
    {
        var dto = new CreateAnnouncementDto { Title = "News", Content = "Breaking" };

        var result = await _service.CreateAsync(dto, _caller);

        var stored = await _db.Announcements.FindAsync(result.Id);
        Assert.IsNotNull(stored);
        Assert.AreEqual("News", stored.Title);
    }

    [TestMethod]
    public async Task WhenCreateAnnouncementWithPriorityThenPriorityIsSet()
    {
        var dto = new CreateAnnouncementDto { Title = "Urgent", Content = "Act now", Priority = "Urgent" };

        var result = await _service.CreateAsync(dto, _caller);

        Assert.AreEqual("Urgent", result.Priority);
    }

    [TestMethod]
    public async Task WhenCreateAnnouncementWithEmptyTitleThenThrows()
    {
        var dto = new CreateAnnouncementDto { Title = "", Content = "Content" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(dto, _caller));
    }

    [TestMethod]
    public async Task WhenCreateAnnouncementWithEmptyContentThenThrows()
    {
        var dto = new CreateAnnouncementDto { Title = "Title", Content = "" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(dto, _caller));
    }

    // ── Get / List ──────────────────────────────────────────────────

    [TestMethod]
    public async Task WhenGetAnnouncementThenAnnouncementIsReturned()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Info", Content = "Details" }, _caller);

        var result = await _service.GetAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Info", result.Title);
    }

    [TestMethod]
    public async Task WhenGetNonExistentAnnouncementThenReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenListAnnouncementsThenAllAreReturned()
    {
        await _service.CreateAsync(new CreateAnnouncementDto { Title = "A", Content = "1" }, _caller);
        await _service.CreateAsync(new CreateAnnouncementDto { Title = "B", Content = "2" }, _caller);

        var result = await _service.ListAsync(_caller);

        Assert.AreEqual(2, result.Count);
    }

    // ── Update ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task WhenUpdateAnnouncementThenFieldsAreModified()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Old", Content = "Original" }, _caller);

        await _service.UpdateAsync(created.Id,
            new UpdateAnnouncementDto { Title = "New", Content = "Updated" }, _caller);

        var updated = await _service.GetAsync(created.Id, _caller);
        Assert.IsNotNull(updated);
        Assert.AreEqual("New", updated.Title);
        Assert.AreEqual("Updated", updated.Content);
    }

    [TestMethod]
    public async Task WhenUpdateAnnouncementPriorityThenPriorityChanges()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Info", Content = "Normal priority" }, _caller);

        await _service.UpdateAsync(created.Id,
            new UpdateAnnouncementDto { Priority = "Important" }, _caller);

        var updated = await _service.GetAsync(created.Id, _caller);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Important", updated.Priority);
    }

    [TestMethod]
    public async Task WhenUpdateNonExistentAnnouncementThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(Guid.NewGuid(),
                new UpdateAnnouncementDto { Title = "X" }, _caller));
    }

    // ── Delete ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task WhenDeleteAnnouncementThenSoftDeleted()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Delete me", Content = "Bye" }, _caller);

        await _service.DeleteAsync(created.Id, _caller);

        var entity = await _db.Announcements.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == created.Id);
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.IsDeleted);
        Assert.IsNotNull(entity.DeletedAt);
    }

    [TestMethod]
    public async Task WhenDeleteNonExistentAnnouncementThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteAsync(Guid.NewGuid(), _caller));
    }

    // ── Acknowledge ─────────────────────────────────────────────────

    [TestMethod]
    public async Task WhenAcknowledgeThenAcknowledgementIsStored()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Ack me", Content = "Please ack", RequiresAcknowledgement = true }, _caller);

        await _service.AcknowledgeAsync(created.Id, _caller);

        var acks = await _service.GetAcknowledgementsAsync(created.Id, _caller);
        Assert.AreEqual(1, acks.Count);
        Assert.AreEqual(_caller.UserId, acks[0].UserId);
    }

    [TestMethod]
    public async Task WhenAcknowledgeTwiceThenNoDuplicate()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Ack", Content = "Test", RequiresAcknowledgement = true }, _caller);

        await _service.AcknowledgeAsync(created.Id, _caller);
        await _service.AcknowledgeAsync(created.Id, _caller);

        var acks = await _service.GetAcknowledgementsAsync(created.Id, _caller);
        Assert.AreEqual(1, acks.Count);
    }

    [TestMethod]
    public async Task WhenMultipleUsersAcknowledgeThenAllAreTracked()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Team ack", Content = "All ack", RequiresAcknowledgement = true }, _caller);

        var user2 = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        await _service.AcknowledgeAsync(created.Id, _caller);
        await _service.AcknowledgeAsync(created.Id, user2);

        var acks = await _service.GetAcknowledgementsAsync(created.Id, _caller);
        Assert.AreEqual(2, acks.Count);
    }

    [TestMethod]
    public async Task WhenGetAcknowledgementsCountThenReflectedInDto()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "Count", Content = "Test", RequiresAcknowledgement = true }, _caller);

        await _service.AcknowledgeAsync(created.Id, _caller);

        var result = await _service.GetAsync(created.Id, _caller);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.AcknowledgementCount);
    }

    [TestMethod]
    public async Task WhenNoAcknowledgementsThenEmptyListReturned()
    {
        var created = await _service.CreateAsync(
            new CreateAnnouncementDto { Title = "No acks", Content = "None" }, _caller);

        var acks = await _service.GetAcknowledgementsAsync(created.Id, _caller);

        Assert.AreEqual(0, acks.Count);
    }
}
