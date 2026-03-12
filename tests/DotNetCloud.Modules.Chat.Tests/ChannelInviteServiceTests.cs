using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChannelInviteService"/>.
/// </summary>
[TestClass]
public class ChannelInviteServiceTests
{
    private ChatDbContext _db = null!;
    private ChannelInviteService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChannelMemberService> _memberServiceMock = null!;

    private CallerContext _ownerCaller = null!;
    private CallerContext _adminCaller = null!;
    private CallerContext _outsiderCaller = null!;
    private CallerContext _inviteeCaller = null!;

    private Guid _privateChannelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _memberServiceMock = new Mock<IChannelMemberService>();

        _service = new ChannelInviteService(
            _db,
            _memberServiceMock.Object,
            _eventBusMock.Object,
            NullLogger<ChannelInviteService>.Instance,
            _realtimeMock.Object);

        _ownerCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _adminCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _outsiderCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _inviteeCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        var channel = new Channel
        {
            Name = "secret-project",
            Type = ChannelType.Private,
            CreatedByUserId = _ownerCaller.UserId
        };

        _db.Channels.Add(channel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = _ownerCaller.UserId, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = channel.Id, UserId = _adminCaller.UserId, Role = ChannelMemberRole.Admin });

        await _db.SaveChangesAsync();
        _privateChannelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ── CreateInviteAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task CreateInvite_OwnerInvitesSingleUser_InviteCreated()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId, Message = "Join us!" };

        var result = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        Assert.AreEqual(_privateChannelId, result.ChannelId);
        Assert.AreEqual(_inviteeCaller.UserId, result.InvitedUserId);
        Assert.AreEqual(_ownerCaller.UserId, result.InvitedByUserId);
        Assert.AreEqual("Pending", result.Status);
        Assert.AreEqual("Join us!", result.Message);
        Assert.AreEqual("secret-project", result.ChannelName);

        // Verify real-time notification sent to invitee only
        _realtimeMock.Verify(
            r => r.SendInviteNotificationAsync(_inviteeCaller.UserId, It.IsAny<ChannelInviteDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateInvite_AdminInvitesSingleUser_InviteCreated()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        var result = await _service.CreateInviteAsync(_privateChannelId, dto, _adminCaller);

        Assert.AreEqual("Pending", result.Status);
        Assert.AreEqual(_adminCaller.UserId, result.InvitedByUserId);
    }

    [TestMethod]
    public async Task CreateInvite_NonAdminCannotInvite_ThrowsUnauthorized()
    {
        var memberCaller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = _privateChannelId,
            UserId = memberCaller.UserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateInviteAsync(_privateChannelId, dto, memberCaller));
    }

    [TestMethod]
    public async Task CreateInvite_OutsiderCannotInvite_ThrowsUnauthorized()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateInviteAsync(_privateChannelId, dto, _outsiderCaller));
    }

    [TestMethod]
    public async Task CreateInvite_PublicChannel_ThrowsInvalidOperation()
    {
        var publicChannel = new Channel
        {
            Name = "public-channel",
            Type = ChannelType.Public,
            CreatedByUserId = _ownerCaller.UserId
        };
        _db.Channels.Add(publicChannel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = publicChannel.Id,
            UserId = _ownerCaller.UserId,
            Role = ChannelMemberRole.Owner
        });
        await _db.SaveChangesAsync();

        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInviteAsync(publicChannel.Id, dto, _ownerCaller));
    }

    [TestMethod]
    public async Task CreateInvite_UserAlreadyMember_ThrowsInvalidOperation()
    {
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = _privateChannelId,
            UserId = _inviteeCaller.UserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller));
    }

    [TestMethod]
    public async Task CreateInvite_DuplicatePendingInvite_ThrowsInvalidOperation()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller));
    }

    [TestMethod]
    public async Task CreateInvite_NonexistentChannel_ThrowsInvalidOperation()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInviteAsync(Guid.NewGuid(), dto, _ownerCaller));
    }

    // ── AcceptInviteAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task AcceptInvite_InviteeAccepts_StatusAcceptedAndMemberAdded()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        var result = await _service.AcceptInviteAsync(created.Id, _inviteeCaller);

        Assert.AreEqual("Accepted", result.Status);
        Assert.IsNotNull(result.RespondedAt);

        // Verify AddMemberAsync was called with system caller
        _memberServiceMock.Verify(
            m => m.AddMemberAsync(_privateChannelId, _inviteeCaller.UserId, It.Is<CallerContext>(c => c.Type == CallerType.System), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AcceptInvite_NonInviteeCannotAccept_ThrowsUnauthorized()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.AcceptInviteAsync(created.Id, _outsiderCaller));
    }

    [TestMethod]
    public async Task AcceptInvite_AlreadyAccepted_ThrowsInvalidOperation()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);
        await _service.AcceptInviteAsync(created.Id, _inviteeCaller);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AcceptInviteAsync(created.Id, _inviteeCaller));
    }

    // ── DeclineInviteAsync ─────────────────────────────────────────

    [TestMethod]
    public async Task DeclineInvite_InviteeDeclines_StatusDeclined()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        var result = await _service.DeclineInviteAsync(created.Id, _inviteeCaller);

        Assert.AreEqual("Declined", result.Status);
        Assert.IsNotNull(result.RespondedAt);
    }

    [TestMethod]
    public async Task DeclineInvite_NonInviteeCannotDecline_ThrowsUnauthorized()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DeclineInviteAsync(created.Id, _outsiderCaller));
    }

    // ── RevokeInviteAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task RevokeInvite_InviterRevokes_StatusRevoked()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await _service.RevokeInviteAsync(created.Id, _ownerCaller);

        var invite = await _db.ChannelInvites.FindAsync(created.Id);
        Assert.AreEqual(ChannelInviteStatus.Revoked, invite!.Status);
    }

    [TestMethod]
    public async Task RevokeInvite_AdminRevokes_Succeeds()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await _service.RevokeInviteAsync(created.Id, _adminCaller);

        var invite = await _db.ChannelInvites.FindAsync(created.Id);
        Assert.AreEqual(ChannelInviteStatus.Revoked, invite!.Status);
    }

    [TestMethod]
    public async Task RevokeInvite_OutsiderCannotRevoke_ThrowsUnauthorized()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.RevokeInviteAsync(created.Id, _outsiderCaller));
    }

    // ── ListMyInvitesAsync ─────────────────────────────────────────

    [TestMethod]
    public async Task ListMyInvites_ReturnsOnlyPendingForCaller()
    {
        var otherUser = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.CreateInviteAsync(_privateChannelId, new CreateChannelInviteDto { UserId = _inviteeCaller.UserId }, _ownerCaller);
        await _service.CreateInviteAsync(_privateChannelId, new CreateChannelInviteDto { UserId = otherUser.UserId }, _ownerCaller);

        var myInvites = await _service.ListMyInvitesAsync(_inviteeCaller);

        Assert.AreEqual(1, myInvites.Count);
        Assert.AreEqual(_inviteeCaller.UserId, myInvites[0].InvitedUserId);
    }

    [TestMethod]
    public async Task ListMyInvites_ExcludesAcceptedInvites()
    {
        var dto = new CreateChannelInviteDto { UserId = _inviteeCaller.UserId };
        var created = await _service.CreateInviteAsync(_privateChannelId, dto, _ownerCaller);
        await _service.AcceptInviteAsync(created.Id, _inviteeCaller);

        var myInvites = await _service.ListMyInvitesAsync(_inviteeCaller);

        Assert.AreEqual(0, myInvites.Count);
    }

    // ── ListChannelInvitesAsync ────────────────────────────────────

    [TestMethod]
    public async Task ListChannelInvites_AdminCanList()
    {
        await _service.CreateInviteAsync(_privateChannelId, new CreateChannelInviteDto { UserId = _inviteeCaller.UserId }, _ownerCaller);

        var channelInvites = await _service.ListChannelInvitesAsync(_privateChannelId, _adminCaller);

        Assert.AreEqual(1, channelInvites.Count);
    }

    [TestMethod]
    public async Task ListChannelInvites_OutsiderCannotList_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.ListChannelInvitesAsync(_privateChannelId, _outsiderCaller));
    }
}
