using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="PinService"/>.
/// </summary>
[TestClass]
public class PinServiceTests
{
    private ChatDbContext _db = null!;
    private PinService _service = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;
    private Guid _messageId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _service = new PinService(_db, NullLogger<PinService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        var channel = new Channel { Name = "test", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(channel);
        var message = new Message { ChannelId = channel.Id, SenderUserId = _caller.UserId, Content = "pin me" };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _channelId = channel.Id;
        _messageId = message.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task WhenPinMessageThenMessageIsPinned()
    {
        await _service.PinMessageAsync(_channelId, _messageId, _caller);

        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(1, pins.Count);
        Assert.AreEqual(_messageId, pins[0].Id);
    }

    [TestMethod]
    public async Task WhenPinMessageTwiceThenNoDuplicate()
    {
        await _service.PinMessageAsync(_channelId, _messageId, _caller);
        await _service.PinMessageAsync(_channelId, _messageId, _caller);

        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(1, pins.Count);
    }

    [TestMethod]
    public async Task WhenUnpinMessageThenMessageIsUnpinned()
    {
        await _service.PinMessageAsync(_channelId, _messageId, _caller);
        await _service.UnpinMessageAsync(_channelId, _messageId, _caller);

        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(0, pins.Count);
    }

    [TestMethod]
    public async Task WhenUnpinNonPinnedMessageThenNoError()
    {
        await _service.UnpinMessageAsync(_channelId, _messageId, _caller);
        // Should not throw
    }

    [TestMethod]
    public async Task WhenNoPinnedMessagesThenEmptyListIsReturned()
    {
        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(0, pins.Count);
    }
}
