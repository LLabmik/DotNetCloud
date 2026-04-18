using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Host.Controllers;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

using IEventBus = DotNetCloud.Core.Events.IEventBus;
using IUserDirectory = DotNetCloud.Core.Capabilities.IUserDirectory;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for Phase B — Direct DM &amp; Call Initiation.
/// Covers <see cref="VideoCallService.InitiateDirectCallAsync"/>,
/// the <c>POST /api/v1/chat/calls/direct/{targetUserId}</c> endpoint,
/// and edge cases around DM reuse, self-call prevention, and missing dependencies.
/// </summary>
[TestClass]
public class DirectCallServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChatMessageNotifier> _messageNotifierMock = null!;
    private Mock<ILiveKitService> _liveKitMock = null!;
    private Mock<IChannelService> _channelServiceMock = null!;
    private Mock<IUserDirectory> _userDirectoryMock = null!;
    private VideoCallService _service = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _messageNotifierMock = new Mock<IChatMessageNotifier>();
        _liveKitMock = new Mock<ILiveKitService>();
        _liveKitMock.Setup(x => x.IsAvailable).Returns(false);
        _liveKitMock.Setup(x => x.MaxP2PParticipants).Returns(3);
        _channelServiceMock = new Mock<IChannelService>();
        _userDirectoryMock = new Mock<IUserDirectory>();

        _service = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object,
            _userDirectoryMock.Object,
            _channelServiceMock.Object);

        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private CallerContext CreateCaller() =>
        new(Guid.NewGuid(), ["user"], CallerType.User);

    private Guid SeedDmChannel(Guid user1Id, Guid user2Id)
    {
        var channelId = Guid.NewGuid();
        _db.Channels.Add(new Channel
        {
            Id = channelId,
            Name = "DM",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = user1Id
        });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channelId, UserId = user1Id, Role = ChannelMemberRole.Owner });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channelId, UserId = user2Id, Role = ChannelMemberRole.Member });
        _db.SaveChanges();
        return channelId;
    }

    private void SetupChannelServiceForDm(Guid targetUserId, Guid channelId)
    {
        _channelServiceMock
            .Setup(s => s.GetOrCreateDirectMessageAsync(targetUserId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelDto
            {
                Id = channelId,
                Name = "DM",
                Type = "DirectMessage",
                CreatedByUserId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                MemberCount = 2
            });
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateDirectCallAsync — Happy Path
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateDirectCallAsync_ValidRequest_CreatesCallInRingingState()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual("Ringing", result.State);
        Assert.AreEqual(channelId, result.ChannelId);
        Assert.AreEqual(_caller.UserId, result.InitiatorUserId);
        Assert.AreEqual("Video", result.MediaType);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_AudioCall_SetsMediaTypeToAudio()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Audio" },
            _caller);

        Assert.AreEqual("Audio", result.MediaType);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_AddsCallerAsHostParticipant()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual(1, result.Participants.Count);
        var participant = result.Participants[0];
        Assert.AreEqual(_caller.UserId, participant.UserId);
        Assert.AreEqual("Host", participant.Role);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_SetsHostUserIdToCaller()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual(_caller.UserId, result.HostUserId);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_PersistsCallToDatabase()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        var dbCall = await _db.VideoCalls.FindAsync(result.Id);
        Assert.IsNotNull(dbCall);
        Assert.AreEqual(VideoCallState.Ringing, dbCall.State);
        Assert.AreEqual(channelId, dbCall.ChannelId);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_PublishesVideoCallInitiatedEvent()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<VideoCallInitiatedEvent>(e =>
                    e.ChannelId == channelId &&
                    e.InitiatorUserId == _caller.UserId &&
                    e.MediaType == "Video"),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_NotifiesCallRinging()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        _messageNotifierMock.Verify(
            notifier => notifier.NotifyCallRinging(It.Is<CallRingingNotification>(n =>
                n.CallId == result.Id &&
                n.ChannelId == channelId &&
                n.InitiatorUserId == _caller.UserId &&
                n.MediaType == "Video")),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_DmChannelHasTwoMembers_IsNotGroupCall()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.IsFalse(result.IsGroupCall);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_CallsGetOrCreateDirectMessageAsync()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Audio" },
            _caller);

        _channelServiceMock.Verify(
            s => s.GetOrCreateDirectMessageAsync(targetUserId, _caller, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateDirectCallAsync — Reuse Existing DM
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateDirectCallAsync_ReusesDmChannelWhenExists()
    {
        var targetUserId = Guid.NewGuid();
        var existingChannelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, existingChannelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual(existingChannelId, result.ChannelId);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_TwoCallsToDifferentUsers_CreatesSeparateCalls()
    {
        var target1 = Guid.NewGuid();
        var target2 = Guid.NewGuid();
        var channel1 = SeedDmChannel(_caller.UserId, target1);
        var channel2 = SeedDmChannel(_caller.UserId, target2);
        SetupChannelServiceForDm(target1, channel1);
        SetupChannelServiceForDm(target2, channel2);

        var call1 = await _service.InitiateDirectCallAsync(
            target1, new StartCallRequest { MediaType = "Audio" }, _caller);
        var call2 = await _service.InitiateDirectCallAsync(
            target2, new StartCallRequest { MediaType = "Audio" }, _caller);

        Assert.AreNotEqual(call1.Id, call2.Id);
        Assert.AreEqual(channel1, call1.ChannelId);
        Assert.AreEqual(channel2, call2.ChannelId);
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateDirectCallAsync — Validation & Error Cases
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateDirectCallAsync_CallingYourself_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.InitiateDirectCallAsync(
                _caller.UserId,
                new StartCallRequest { MediaType = "Video" },
                _caller));

        Assert.IsTrue(ex.Message.Contains("Cannot initiate a direct call to yourself"));
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.InitiateDirectCallAsync(
                Guid.NewGuid(),
                null!,
                _caller));
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_NullCaller_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.InitiateDirectCallAsync(
                Guid.NewGuid(),
                new StartCallRequest { MediaType = "Video" },
                null!));
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_InvalidMediaType_ThrowsArgumentException()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.InitiateDirectCallAsync(
                targetUserId,
                new StartCallRequest { MediaType = "InvalidType" },
                _caller));
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_ActiveCallExistsOnDm_ThrowsInvalidOperationException()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        // First call succeeds
        await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        // Second call to same user should fail (active call exists)
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.InitiateDirectCallAsync(
                targetUserId,
                new StartCallRequest { MediaType = "Video" },
                _caller));
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_NoChannelService_ThrowsInvalidOperationException()
    {
        // Create service without IChannelService
        var serviceWithoutChannelService = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => serviceWithoutChannelService.InitiateDirectCallAsync(
                Guid.NewGuid(),
                new StartCallRequest { MediaType = "Video" },
                _caller));

        Assert.IsTrue(ex.Message.Contains("Channel service is not available"));
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateDirectCallAsync — Video-specific Behavior
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateDirectCallAsync_VideoCall_ParticipantHasVideo()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        var participant = result.Participants[0];
        Assert.IsTrue(participant.HasVideo);
        Assert.IsTrue(participant.HasAudio);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_AudioCall_ParticipantHasNoVideo()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Audio" },
            _caller);

        var participant = result.Participants[0];
        Assert.IsFalse(participant.HasVideo);
        Assert.IsTrue(participant.HasAudio);
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateDirectCallAsync — Broadcast & Realtime
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateDirectCallAsync_BroadcastsChannelUpdate()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        _realtimeMock.Verify(
            r => r.BroadcastChannelUpdatedAsync(
                It.Is<ChannelDto>(c => c.Id == channelId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_WithoutRealtimeService_DoesNotThrow()
    {
        var serviceNoRealtime = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            realtimeService: null,
            messageNotifier: null,
            channelService: _channelServiceMock.Object);

        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var result = await serviceNoRealtime.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Ringing", result.State);
    }

    // ══════════════════════════════════════════════════════════════
    //  Full Lifecycle: Direct Call → Join → Active
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DirectCallLifecycle_Initiate_ThenTargetJoins_CallBecomesActive()
    {
        var targetUserId = Guid.NewGuid();
        var targetCaller = new CallerContext(targetUserId, ["user"], CallerType.User);
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        // Step 1: Caller initiates direct call
        var callDto = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual("Ringing", callDto.State);
        Assert.AreEqual(1, callDto.Participants.Count);

        // Step 2: Target joins the call
        var joined = await _service.JoinCallAsync(
            callDto.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            targetCaller);

        Assert.AreEqual("Active", joined.State);
        Assert.AreEqual(2, joined.Participants.Count);
    }

    [TestMethod]
    public async Task DirectCallLifecycle_Initiate_ThenReject_CallEndsMissed()
    {
        var targetUserId = Guid.NewGuid();
        var targetCaller = new CallerContext(targetUserId, ["user"], CallerType.User);
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var callDto = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Audio" },
            _caller);

        await _service.RejectCallAsync(callDto.Id, targetCaller);

        var dbCall = await _db.VideoCalls.FindAsync(callDto.Id);
        Assert.IsNotNull(dbCall);
        Assert.AreEqual(VideoCallState.Rejected, dbCall.State);
        Assert.AreEqual(VideoCallEndReason.Rejected, dbCall.EndReason);
    }

    [TestMethod]
    public async Task DirectCallLifecycle_Initiate_ThenEndByHost_CallEnds()
    {
        var targetUserId = Guid.NewGuid();
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var callDto = await _service.InitiateDirectCallAsync(
            targetUserId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        await _service.EndCallAsync(callDto.Id, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(callDto.Id);
        Assert.IsNotNull(dbCall);
        Assert.AreEqual(VideoCallState.Ended, dbCall.State);
    }

    [TestMethod]
    public async Task DirectCallLifecycle_Initiate_Join_Leave_CallEndsWhenLastParticipantLeaves()
    {
        var targetUserId = Guid.NewGuid();
        var targetCaller = new CallerContext(targetUserId, ["user"], CallerType.User);
        var channelId = SeedDmChannel(_caller.UserId, targetUserId);
        SetupChannelServiceForDm(targetUserId, channelId);

        var callDto = await _service.InitiateDirectCallAsync(
            targetUserId, new StartCallRequest { MediaType = "Audio" }, _caller);

        // Target joins
        await _service.JoinCallAsync(callDto.Id, new JoinCallRequest { WithAudio = true }, targetCaller);

        // Caller leaves
        await _service.LeaveCallAsync(callDto.Id, _caller);

        // Target leaves — call should auto-end
        await _service.LeaveCallAsync(callDto.Id, targetCaller);

        var dbCall = await _db.VideoCalls.FindAsync(callDto.Id);
        Assert.IsNotNull(dbCall);
        Assert.AreEqual(VideoCallState.Ended, dbCall.State);
    }

    // ══════════════════════════════════════════════════════════════
    //  Backward Compatibility: InitiateDirectCallAsync doesn't
    //  break existing InitiateCallAsync
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ExistingInitiateCallAsync_StillWorksAfterNewMethod()
    {
        var channelId = SeedDmChannel(_caller.UserId, Guid.NewGuid());

        var result = await _service.InitiateCallAsync(
            channelId,
            new StartCallRequest { MediaType = "Video" },
            _caller);

        Assert.AreEqual("Ringing", result.State);
        Assert.AreEqual(channelId, result.ChannelId);
    }
}

/// <summary>
/// Tests for the <c>POST /api/v1/chat/calls/direct/{targetUserId}</c> controller endpoint.
/// </summary>
[TestClass]
public class DirectCallControllerTests
{
    private Mock<IChannelService> _channelService = null!;
    private Mock<IChannelMemberService> _memberService = null!;
    private Mock<IMessageService> _messageService = null!;
    private Mock<IReactionService> _reactionService = null!;
    private Mock<IPinService> _pinService = null!;
    private Mock<ITypingIndicatorService> _typingService = null!;
    private Mock<IAnnouncementService> _announcementService = null!;
    private Mock<IChannelInviteService> _inviteService = null!;
    private Mock<IRealtimeBroadcaster> _realtimeBroadcaster = null!;
    private Mock<IChatRealtimeService> _chatRealtimeService = null!;
    private Mock<IChatMessageNotifier> _chatMessageNotifier = null!;
    private Mock<IPushNotificationService> _pushNotificationService = null!;
    private Mock<INotificationPreferenceStore> _notificationPreferenceStore = null!;
    private Mock<IIceServerService> _iceServerService = null!;
    private Mock<IVideoCallService> _videoCallService = null!;
    private Mock<IUserBlockService> _userBlockService = null!;
    private ChatController _controller = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        _userId = Guid.NewGuid();
        _channelService = new Mock<IChannelService>();
        _memberService = new Mock<IChannelMemberService>();
        _messageService = new Mock<IMessageService>();
        _reactionService = new Mock<IReactionService>();
        _pinService = new Mock<IPinService>();
        _typingService = new Mock<ITypingIndicatorService>();
        _announcementService = new Mock<IAnnouncementService>();
        _inviteService = new Mock<IChannelInviteService>();
        _realtimeBroadcaster = new Mock<IRealtimeBroadcaster>();
        _chatRealtimeService = new Mock<IChatRealtimeService>();
        _chatMessageNotifier = new Mock<IChatMessageNotifier>();
        _pushNotificationService = new Mock<IPushNotificationService>();
        _notificationPreferenceStore = new Mock<INotificationPreferenceStore>();
        _iceServerService = new Mock<IIceServerService>();
        _videoCallService = new Mock<IVideoCallService>();
        _userBlockService = new Mock<IUserBlockService>();

        _notificationPreferenceStore
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .Returns(new UserNotificationPreferences
            {
                PushEnabled = true,
                DoNotDisturb = false,
                MutedChannelIds = new HashSet<Guid>()
            });

        _controller = new ChatController(
            _channelService.Object,
            _memberService.Object,
            _messageService.Object,
            _reactionService.Object,
            _pinService.Object,
            _typingService.Object,
            _announcementService.Object,
            _inviteService.Object,
            _realtimeBroadcaster.Object,
            _chatRealtimeService.Object,
            _chatMessageNotifier.Object,
            _pushNotificationService.Object,
            _notificationPreferenceStore.Object,
            _iceServerService.Object,
            _videoCallService.Object,
            _userBlockService.Object,
            NullLogger<ChatController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())],
                        authenticationType: "TestAuth"))
                }
            }
        };
    }

    private static VideoCallDto CreateTestCallDto(Guid? callId = null, Guid? channelId = null)
    {
        return new VideoCallDto
        {
            Id = callId ?? Guid.NewGuid(),
            ChannelId = channelId ?? Guid.NewGuid(),
            InitiatorUserId = Guid.NewGuid(),
            HostUserId = Guid.NewGuid(),
            State = "Ringing",
            MediaType = "Video",
            IsGroupCall = false,
            MaxParticipants = 2,
            CreatedAtUtc = DateTime.UtcNow,
            Participants = []
        };
    }

    // ── InitiateDirectCallAsync Endpoint Tests ──────────────────

    [TestMethod]
    public async Task InitiateDirectCallAsync_Success_ReturnsCreatedAtAction()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };
        var callDto = CreateTestCallDto();

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
        var created = (CreatedAtActionResult)result;
        Assert.AreEqual("GetCall", created.ActionName);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_Success_ReturnsCallInEnvelope()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };
        var callDto = CreateTestCallDto();

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);
        var created = (CreatedAtActionResult)result;

        Assert.IsNotNull(created.Value);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_SelfCall_ReturnsBadRequest()
    {
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(_userId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Cannot initiate a direct call to yourself."));

        var result = await _controller.InitiateDirectCallAsync(_userId, request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_InvalidMediaType_ReturnsBadRequest()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "InvalidType" };

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid media type: InvalidType"));

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_ActiveCallExists_ReturnsConflict()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("An active call already exists in this channel."));

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_Unauthorized_ReturnsForbid()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_ChannelServiceUnavailable_ReturnsConflict()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Channel service is not available. Cannot initiate direct calls."));

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_PassesCallerContextToService()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };
        var callDto = CreateTestCallDto();

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(
                targetUserId, request,
                It.Is<CallerContext>(c => c.UserId == _userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        await _controller.InitiateDirectCallAsync(targetUserId, request);

        _videoCallService.Verify(
            s => s.InitiateDirectCallAsync(
                targetUserId, request,
                It.Is<CallerContext>(c => c.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateDirectCallAsync_AudioCall_PassesCorrectMediaType()
    {
        var targetUserId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Audio" };
        var callDto = CreateTestCallDto();

        _videoCallService
            .Setup(s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateDirectCallAsync(targetUserId, request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
        _videoCallService.Verify(
            s => s.InitiateDirectCallAsync(targetUserId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Existing InitiateCallAsync endpoint still works ─────────

    [TestMethod]
    public async Task InitiateCallAsync_Endpoint_StillWorks()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };
        var callDto = CreateTestCallDto(channelId: channelId);

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
    }
}

/// <summary>
/// Tests for the <see cref="UserSearchResultViewModel"/> view model.
/// </summary>
[TestClass]
public class UserSearchResultViewModelTests
{
    [TestMethod]
    public void ViewModel_DefaultValues_AreEmpty()
    {
        var vm = new DotNetCloud.Modules.Chat.UI.UserSearchResultViewModel();

        Assert.AreEqual(Guid.Empty, vm.UserId);
        Assert.AreEqual(string.Empty, vm.DisplayName);
        Assert.AreEqual(string.Empty, vm.Email);
    }

    [TestMethod]
    public void ViewModel_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var vm = new DotNetCloud.Modules.Chat.UI.UserSearchResultViewModel
        {
            UserId = userId,
            DisplayName = "John Doe",
            Email = "john@example.com"
        };

        Assert.AreEqual(userId, vm.UserId);
        Assert.AreEqual("John Doe", vm.DisplayName);
        Assert.AreEqual("john@example.com", vm.Email);
    }
}
