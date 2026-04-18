using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests that <see cref="MessageService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on send, edit, and delete operations.
/// </summary>
[TestClass]
public class MessageServiceSearchIndexTests
{
    private ChatDbContext _db = null!;
    private MessageService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new MessageService(_db, _eventBusMock.Object, NullLogger<MessageService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        // Seed channel with caller as member
        var channel = new Channel { Name = "test-channel", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Owner
        });
        await _db.SaveChangesAsync();
        _channelId = channel.Id;
        _eventBusMock.Invocations.Clear();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task SendMessage_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var result = await _service.SendMessageAsync(
            _channelId, new SendMessageDto { Content = "Hello world" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "chat" &&
                    e.EntityId == result.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EditMessage_PublishesSearchIndexRequestEvent_WithIndexAction()
    {
        var sent = await _service.SendMessageAsync(
            _channelId, new SendMessageDto { Content = "Original" }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.EditMessageAsync(
            sent.Id, new EditMessageDto { Content = "Edited" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "chat" &&
                    e.EntityId == sent.Id.ToString() &&
                    e.Action == SearchIndexAction.Index),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteMessage_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var sent = await _service.SendMessageAsync(
            _channelId, new SendMessageDto { Content = "To delete" }, _caller);
        _eventBusMock.Invocations.Clear();

        await _service.DeleteMessageAsync(sent.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "chat" &&
                    e.EntityId == sent.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
