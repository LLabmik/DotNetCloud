using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelService"/>.
/// </summary>
[TestClass]
public class ChannelServiceTests
{
    private ChatDbContext _db = null!;
    private ChannelService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _service = new ChannelService(_db, _eventBusMock.Object, NullLogger<ChannelService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task WhenCreateChannelThenChannelIsReturned()
    {
        var dto = new CreateChannelDto { Name = "general", Type = "Public" };

        var result = await _service.CreateChannelAsync(dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("general", result.Name);
        Assert.AreEqual("Public", result.Type);
    }

    [TestMethod]
    public async Task WhenCreateChannelThenCreatorIsOwner()
    {
        var dto = new CreateChannelDto { Name = "test", Type = "Public" };

        await _service.CreateChannelAsync(dto, _caller);

        var member = await _db.ChannelMembers
            .FirstOrDefaultAsync(m => m.UserId == _caller.UserId);
        Assert.IsNotNull(member);
        Assert.AreEqual(ChannelMemberRole.Owner, member.Role);
    }

    [TestMethod]
    public async Task WhenCreateChannelThenEventIsPublished()
    {
        var dto = new CreateChannelDto { Name = "test", Type = "Public" };

        await _service.CreateChannelAsync(dto, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<Events.ChannelCreatedEvent>(), _caller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenCreateChannelWithEmptyNameThenThrows()
    {
        var dto = new CreateChannelDto { Name = "", Type = "Public" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateChannelAsync(dto, _caller));
    }

    [TestMethod]
    public async Task WhenCreateChannelWithInvalidTypeThenThrows()
    {
        var dto = new CreateChannelDto { Name = "test", Type = "Invalid" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateChannelAsync(dto, _caller));
    }

    [TestMethod]
    public async Task WhenGetChannelThenChannelIsReturned()
    {
        var dto = new CreateChannelDto { Name = "test", Type = "Public" };
        var created = await _service.CreateChannelAsync(dto, _caller);

        var result = await _service.GetChannelAsync(created.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(created.Id, result.Id);
    }

    [TestMethod]
    public async Task WhenGetNonExistentChannelThenReturnsNull()
    {
        var result = await _service.GetChannelAsync(Guid.NewGuid(), _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WhenListChannelsThenOnlyUserChannelsAreReturned()
    {
        var dto = new CreateChannelDto { Name = "mine", Type = "Public" };
        await _service.CreateChannelAsync(dto, _caller);

        var otherCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        var otherDto = new CreateChannelDto { Name = "other", Type = "Public" };
        await _service.CreateChannelAsync(otherDto, otherCaller);

        var result = await _service.ListChannelsAsync(_caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("mine", result[0].Name);
    }

    [TestMethod]
    public async Task WhenDeleteChannelThenChannelIsSoftDeleted()
    {
        var dto = new CreateChannelDto { Name = "deleteme", Type = "Public" };
        var created = await _service.CreateChannelAsync(dto, _caller);

        await _service.DeleteChannelAsync(created.Id, _caller);

        var channel = await _db.Channels.IgnoreQueryFilters().FirstAsync(c => c.Id == created.Id);
        Assert.IsTrue(channel.IsDeleted);
        Assert.IsNotNull(channel.DeletedAt);
    }

    [TestMethod]
    public async Task WhenArchiveChannelThenChannelIsArchived()
    {
        var dto = new CreateChannelDto { Name = "archiveme", Type = "Public" };
        var created = await _service.CreateChannelAsync(dto, _caller);

        await _service.ArchiveChannelAsync(created.Id, _caller);

        var channel = await _db.Channels.FirstAsync(c => c.Id == created.Id);
        Assert.IsTrue(channel.IsArchived);
    }

    [TestMethod]
    public async Task WhenGetOrCreateDmThenDmIsCreated()
    {
        var otherUserId = Guid.NewGuid();

        var result = await _service.GetOrCreateDirectMessageAsync(otherUserId, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("DirectMessage", result.Type);
        Assert.AreEqual(2, result.MemberCount);
    }

    [TestMethod]
    public async Task WhenGetOrCreateDmTwiceThenSameChannelIsReturned()
    {
        var otherUserId = Guid.NewGuid();

        var first = await _service.GetOrCreateDirectMessageAsync(otherUserId, _caller);
        var second = await _service.GetOrCreateDirectMessageAsync(otherUserId, _caller);

        Assert.AreEqual(first.Id, second.Id);
    }

    [TestMethod]
    public async Task WhenCreateChannelWithMembersThenMembersAreAdded()
    {
        var memberId = Guid.NewGuid();
        var dto = new CreateChannelDto
        {
            Name = "withmembers",
            Type = "Private",
            MemberIds = [memberId]
        };

        var result = await _service.CreateChannelAsync(dto, _caller);

        Assert.AreEqual(2, result.MemberCount);
    }
}
