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
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Member
        });

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
    public async Task WhenPinMessageAsNonMemberThenThrowsUnauthorizedAccessException()
    {
        var nonMember = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.PinMessageAsync(_channelId, _messageId, nonMember));
    }

    [TestMethod]
    public async Task WhenPinMessageFromDifferentChannelThenThrowsInvalidOperationException()
    {
        var otherChannel = new Channel { Name = "other", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(otherChannel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = otherChannel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Member
        });

        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.PinMessageAsync(otherChannel.Id, _messageId, _caller));
    }

    [TestMethod]
    public async Task WhenGetPinnedMessagesThenLatestPinIsReturnedFirst()
    {
        var secondMessage = new Message
        {
            ChannelId = _channelId,
            SenderUserId = _caller.UserId,
            Content = "newest pin"
        };

        _db.Messages.Add(secondMessage);
        await _db.SaveChangesAsync();

        await _service.PinMessageAsync(_channelId, _messageId, _caller);
        await Task.Delay(10);
        await _service.PinMessageAsync(_channelId, secondMessage.Id, _caller);

        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(2, pins.Count);
        Assert.AreEqual(secondMessage.Id, pins[0].Id);
        Assert.AreEqual(_messageId, pins[1].Id);
    }

    [TestMethod]
    public async Task WhenNoPinnedMessagesThenEmptyListIsReturned()
    {
        var pins = await _service.GetPinnedMessagesAsync(_channelId, _caller);

        Assert.AreEqual(0, pins.Count);
    }
}
