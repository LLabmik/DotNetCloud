using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class CommentServiceTests
{
    private TracksDbContext _db = null!;
    private CommentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new CommentService(_db);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task CreateCommentAsync_CreatesComment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var dto = new AddWorkItemCommentDto { Content = "Hello world" };

        var result = await _service.CreateCommentAsync(item.Id, userId, dto, CancellationToken.None);

        Assert.AreEqual("Hello world", result.Content);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(item.Id, result.WorkItemId);
    }

    [TestMethod]
    public async Task GetCommentsByWorkItemAsync_ReturnsComments()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "First" }, CancellationToken.None);
        await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "Second" }, CancellationToken.None);

        var result = await _service.GetCommentsByWorkItemAsync(item.Id, 0, 50, CancellationToken.None);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetCommentsByWorkItemAsync_SupportsPagination()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "A" }, CancellationToken.None);
        await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "B" }, CancellationToken.None);
        await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "C" }, CancellationToken.None);

        var page1 = await _service.GetCommentsByWorkItemAsync(item.Id, 0, 2, CancellationToken.None);
        var page2 = await _service.GetCommentsByWorkItemAsync(item.Id, 2, 2, CancellationToken.None);

        Assert.AreEqual(2, page1.Count);
        Assert.AreEqual(1, page2.Count);
    }

    [TestMethod]
    public async Task UpdateCommentAsync_UpdatesContent()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "Original" }, CancellationToken.None);

        var result = await _service.UpdateCommentAsync(comment.Id, userId, new UpdateWorkItemCommentDto { Content = "Updated" }, CancellationToken.None);

        Assert.AreEqual("Updated", result.Content);
    }

    [TestMethod]
    public async Task DeleteCommentAsync_RemovesComment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "To delete" }, CancellationToken.None);

        await _service.DeleteCommentAsync(comment.Id, userId, CancellationToken.None);

        var result = await _service.GetCommentsByWorkItemAsync(item.Id, 0, 50, CancellationToken.None);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task AddReactionAsync_AddsReactionToComment()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "Nice!" }, CancellationToken.None);

        var result = await _service.AddReactionAsync(comment.Id, userId, "👍", CancellationToken.None);

        Assert.AreEqual("👍", result.Emoji);
        Assert.AreEqual(userId, result.UserId);
    }

    [TestMethod]
    public async Task RemoveReactionAsync_RemovesReaction()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "Nice!" }, CancellationToken.None);
        await _service.AddReactionAsync(comment.Id, userId, "👍", CancellationToken.None);

        await _service.RemoveReactionAsync(comment.Id, userId, "👍", CancellationToken.None);

        var reactions = await _service.GetReactionsAsync(comment.Id, null, CancellationToken.None);
        Assert.AreEqual(0, reactions.Count);
    }

    [TestMethod]
    public async Task GetReactionsAsync_ReturnsReactionSummaries()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.NewGuid(), Guid.NewGuid());
        var swimlane = await TestHelpers.SeedSwimlaneAsync(_db, product.Id);
        var item = await TestHelpers.SeedWorkItemAsync(_db, product.Id, swimlane.Id, Guid.NewGuid());
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(item.Id, userId, new AddWorkItemCommentDto { Content = "Great!" }, CancellationToken.None);
        await _service.AddReactionAsync(comment.Id, userId, "👍", CancellationToken.None);
        await _service.AddReactionAsync(comment.Id, Guid.NewGuid(), "👍", CancellationToken.None);

        var result = await _service.GetReactionsAsync(comment.Id, null, CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0].Count);
    }
}
