using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
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

    [TestMethod]
    public async Task WhenCreateChannelWithDuplicateNameInSameOrgThenThrows()
    {
        var orgId = Guid.NewGuid();
        var dto1 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        await _service.CreateChannelAsync(dto1, _caller);

        var dto2 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateChannelAsync(dto2, _caller));
    }

    [TestMethod]
    public async Task WhenCreateChannelWithSameNameInDifferentOrgsThenSucceeds()
    {
        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var dto1 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId1 };
        var dto2 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId2 };

        var result1 = await _service.CreateChannelAsync(dto1, _caller);
        var result2 = await _service.CreateChannelAsync(dto2, _caller);

        Assert.AreNotEqual(result1.Id, result2.Id);
        Assert.AreEqual("general", result1.Name);
        Assert.AreEqual("general", result2.Name);
    }

    [TestMethod]
    public async Task WhenCreateDmWithDuplicateNameThenSucceeds()
    {
        var otherUserId1 = Guid.NewGuid();
        var otherUserId2 = Guid.NewGuid();

        var result1 = await _service.GetOrCreateDirectMessageAsync(otherUserId1, _caller);
        var result2 = await _service.GetOrCreateDirectMessageAsync(otherUserId2, _caller);

        Assert.AreNotEqual(result1.Id, result2.Id);
    }

    [TestMethod]
    public async Task WhenUpdateChannelNameToDuplicateInSameOrgThenThrows()
    {
        var orgId = Guid.NewGuid();
        var dto1 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        var dto2 = new CreateChannelDto { Name = "random", Type = "Public", OrganizationId = orgId };

        await _service.CreateChannelAsync(dto1, _caller);
        var channel2 = await _service.CreateChannelAsync(dto2, _caller);

        var updateDto = new UpdateChannelDto { Name = "general" };
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateChannelAsync(channel2.Id, updateDto, _caller));
    }

    [TestMethod]
    public async Task WhenUpdateChannelNameToSameNameThenSucceeds()
    {
        var orgId = Guid.NewGuid();
        var dto = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        var created = await _service.CreateChannelAsync(dto, _caller);

        var updateDto = new UpdateChannelDto { Name = "general" };
        var result = await _service.UpdateChannelAsync(created.Id, updateDto, _caller);

        Assert.AreEqual("general", result.Name);
    }

    [TestMethod]
    public async Task WhenCreateChannelWithNullOrgThenUniquenessIsEnforcedWithinNullOrg()
    {
        var dto1 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = null };
        await _service.CreateChannelAsync(dto1, _caller);

        var dto2 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = null };
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateChannelAsync(dto2, _caller));
    }

    [TestMethod]
    public async Task WhenDuplicateNameThenValidationExceptionContainsNameField()
    {
        var orgId = Guid.NewGuid();
        var dto1 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        await _service.CreateChannelAsync(dto1, _caller);

        var dto2 = new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId };
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateChannelAsync(dto2, _caller));

        Assert.IsTrue(ex.Errors.ContainsKey("Name"));
        Assert.IsTrue(ex.Errors["Name"].Count > 0);
    }

    [TestMethod]
    public async Task WhenUpdateToDuplicateNameThenValidationExceptionContainsNameField()
    {
        var orgId = Guid.NewGuid();
        await _service.CreateChannelAsync(
            new CreateChannelDto { Name = "general", Type = "Public", OrganizationId = orgId }, _caller);
        var channel2 = await _service.CreateChannelAsync(
            new CreateChannelDto { Name = "random", Type = "Public", OrganizationId = orgId }, _caller);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateChannelAsync(channel2.Id, new UpdateChannelDto { Name = "general" }, _caller));

        Assert.IsTrue(ex.Errors.ContainsKey("Name"));
    }
}
