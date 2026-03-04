using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ReactionService"/>.
/// </summary>
[TestClass]
public class ReactionServiceTests
{
    private ChatDbContext _db = null!;
    private ReactionService _service = null!;
    private CallerContext _caller = null!;
    private Guid _messageId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _service = new ReactionService(_db, new Mock<IEventBus>().Object, NullLogger<ReactionService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        var channel = new Channel { Name = "test", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(channel);
        var message = new Message { ChannelId = channel.Id, SenderUserId = _caller.UserId, Content = "test" };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _messageId = message.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task WhenAddReactionThenReactionIsStored()
    {
        await _service.AddReactionAsync(_messageId, "👍", _caller);

        var reactions = await _service.GetReactionsAsync(_messageId);

        Assert.AreEqual(1, reactions.Count);
        Assert.AreEqual("👍", reactions[0].Emoji);
        Assert.AreEqual(1, reactions[0].Count);
    }

    [TestMethod]
    public async Task WhenAddDuplicateReactionThenNoChange()
    {
        await _service.AddReactionAsync(_messageId, "👍", _caller);
        await _service.AddReactionAsync(_messageId, "👍", _caller);

        var reactions = await _service.GetReactionsAsync(_messageId);

        Assert.AreEqual(1, reactions.Count);
        Assert.AreEqual(1, reactions[0].Count);
    }

    [TestMethod]
    public async Task WhenRemoveReactionThenReactionIsRemoved()
    {
        await _service.AddReactionAsync(_messageId, "👍", _caller);
        await _service.RemoveReactionAsync(_messageId, "👍", _caller);

        var reactions = await _service.GetReactionsAsync(_messageId);

        Assert.AreEqual(0, reactions.Count);
    }

    [TestMethod]
    public async Task WhenMultipleUsersReactThenCountIsCorrect()
    {
        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.AddReactionAsync(_messageId, "👍", _caller);
        await _service.AddReactionAsync(_messageId, "👍", otherCaller);

        var reactions = await _service.GetReactionsAsync(_messageId);

        Assert.AreEqual(1, reactions.Count);
        Assert.AreEqual(2, reactions[0].Count);
    }

    [TestMethod]
    public async Task WhenDifferentEmojisThenGroupedCorrectly()
    {
        await _service.AddReactionAsync(_messageId, "👍", _caller);
        await _service.AddReactionAsync(_messageId, "❤️", _caller);

        var reactions = await _service.GetReactionsAsync(_messageId);

        Assert.AreEqual(2, reactions.Count);
    }

    [TestMethod]
    public async Task WhenAddReactionToNonExistentMessageThenThrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddReactionAsync(Guid.NewGuid(), "👍", _caller));
    }

    [TestMethod]
    public async Task WhenAddReactionWithEmptyEmojiThenThrows()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddReactionAsync(_messageId, "", _caller));
    }
}
