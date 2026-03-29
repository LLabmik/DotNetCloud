using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class ActivityServiceTests
{
    private TracksDbContext _db;
    private ActivityService _service;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new ActivityService(_db, NullLogger<ActivityService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── Log ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task LogAsync_CreatesActivityEntry()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.LogAsync(boardId, userId, "card.created", "Card", Guid.NewGuid(), "{\"title\":\"Test\"}");

        var activities = await _service.GetBoardActivityAsync(boardId);

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual("card.created", activities[0].Action);
        Assert.AreEqual("Card", activities[0].EntityType);
        Assert.AreEqual(userId, activities[0].UserId);
    }

    // ─── Board Activity ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetBoardActivity_ReturnsNewestFirst()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await _service.LogAsync(boardId, userId, "first", "Card", Guid.NewGuid());
        await Task.Delay(10); // Ensure different timestamps
        await _service.LogAsync(boardId, userId, "second", "Card", Guid.NewGuid());

        var activities = await _service.GetBoardActivityAsync(boardId);

        Assert.AreEqual(2, activities.Count);
        Assert.AreEqual("second", activities[0].Action);
        Assert.AreEqual("first", activities[1].Action);
    }

    [TestMethod]
    public async Task GetBoardActivity_Pagination_Works()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await _service.LogAsync(boardId, userId, $"action{i}", "Card", Guid.NewGuid());
        }

        var page1 = await _service.GetBoardActivityAsync(boardId, skip: 0, take: 2);
        var page2 = await _service.GetBoardActivityAsync(boardId, skip: 2, take: 2);

        Assert.AreEqual(2, page1.Count);
        Assert.AreEqual(2, page2.Count);
    }

    // ─── Card Activity ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCardActivity_FiltersByCardId()
    {
        var boardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var otherCardId = Guid.NewGuid();

        await _service.LogAsync(boardId, userId, "card.created", "Card", cardId);
        await _service.LogAsync(boardId, userId, "card.created", "Card", otherCardId);

        var activities = await _service.GetCardActivityAsync(cardId);

        Assert.AreEqual(1, activities.Count);
        Assert.AreEqual(cardId, activities[0].EntityId);
    }
}
