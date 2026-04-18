using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Host.Controllers;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for video call REST API endpoints in <see cref="ChatController"/>.
/// </summary>
[TestClass]
public class VideoCallControllerTests
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
            State = "Ringing",
            MediaType = "Video",
            IsGroupCall = false,
            MaxParticipants = 2,
            CreatedAtUtc = DateTime.UtcNow,
            Participants = []
        };
    }

    // ── InitiateCallAsync Tests ──────────────────────────────────

    [TestMethod]
    public async Task InitiateCallAsync_Success_ReturnsCreatedAtAction()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };
        var callDto = CreateTestCallDto(channelId: channelId);

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
        var created = (CreatedAtActionResult)result;
        Assert.AreEqual("GetCall", created.ActionName);
    }

    [TestMethod]
    public async Task InitiateCallAsync_InvalidMediaType_ReturnsBadRequest()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "InvalidType" };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid media type: InvalidType"));

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateCallAsync_ActiveCallExists_ReturnsConflict()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("An active call already exists in this channel."));

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateCallAsync_Unauthorized_ReturnsForbid()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Video" };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not a member"));

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task InitiateCallAsync_PassesCallerContextToService()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Audio" };
        var callDto = CreateTestCallDto(channelId: channelId);

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        await _controller.InitiateCallAsync(channelId, request);

        _videoCallService.Verify(s => s.InitiateCallAsync(
            channelId,
            request,
            It.Is<CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── JoinCallAsync Tests ─────────────────────────────────────

    [TestMethod]
    public async Task JoinCallAsync_Success_ReturnsOkWithCall()
    {
        var callId = Guid.NewGuid();
        var request = new JoinCallRequest { WithAudio = true, WithVideo = true };
        var callDto = CreateTestCallDto(callId: callId);

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.JoinCallAsync(callId, request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task JoinCallAsync_InvalidRequest_ReturnsBadRequest()
    {
        var callId = Guid.NewGuid();
        var request = new JoinCallRequest();

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid request"));

        var result = await _controller.JoinCallAsync(callId, request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task JoinCallAsync_CallNotFound_ReturnsNotFound()
    {
        var callId = Guid.NewGuid();
        var request = new JoinCallRequest();

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var result = await _controller.JoinCallAsync(callId, request);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task JoinCallAsync_Unauthorized_ReturnsForbid()
    {
        var callId = Guid.NewGuid();
        var request = new JoinCallRequest();

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not a member"));

        var result = await _controller.JoinCallAsync(callId, request);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── LeaveCallAsync Tests ────────────────────────────────────

    [TestMethod]
    public async Task LeaveCallAsync_Success_ReturnsOk()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.LeaveCallAsync(callId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task LeaveCallAsync_CallNotFound_ReturnsNotFound()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var result = await _controller.LeaveCallAsync(callId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task LeaveCallAsync_Unauthorized_ReturnsForbid()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not authorized"));

        var result = await _controller.LeaveCallAsync(callId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── EndCallAsync Tests ──────────────────────────────────────

    [TestMethod]
    public async Task EndCallAsync_Success_ReturnsOk()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.EndCallAsync(callId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task EndCallAsync_CallNotFound_ReturnsNotFound()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var result = await _controller.EndCallAsync(callId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task EndCallAsync_Unauthorized_ReturnsForbid()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not authorized"));

        var result = await _controller.EndCallAsync(callId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── RejectCallAsync Tests ───────────────────────────────────

    [TestMethod]
    public async Task RejectCallAsync_Success_ReturnsOk()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.RejectCallAsync(callId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task RejectCallAsync_CallNotFound_ReturnsNotFound()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not found"));

        var result = await _controller.RejectCallAsync(callId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task RejectCallAsync_Unauthorized_ReturnsForbid()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not authorized"));

        var result = await _controller.RejectCallAsync(callId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── GetCallHistoryAsync Tests ───────────────────────────────

    [TestMethod]
    public async Task GetCallHistoryAsync_Success_ReturnsOkWithHistory()
    {
        var channelId = Guid.NewGuid();
        var history = new List<CallHistoryDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                InitiatorUserId = Guid.NewGuid(),
                State = "Ended",
                MediaType = "Video",
                EndReason = "Normal",
                DurationSeconds = 120,
                ParticipantCount = 2,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
            }
        };

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 20, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var result = await _controller.GetCallHistoryAsync(channelId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_WithPagination_ClampsTakeValue()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 10, 100, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetCallHistoryAsync(channelId, skip: 10, take: 200);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        // Take should be clamped to 100
        _videoCallService.Verify(s => s.GetCallHistoryAsync(
            channelId, 10, 100, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_NegativeSkip_ClampsToZero()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 20, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetCallHistoryAsync(channelId, skip: -5, take: 20);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _videoCallService.Verify(s => s.GetCallHistoryAsync(
            channelId, 0, 20, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_TakeBelowMinimum_ClampsToOne()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 1, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetCallHistoryAsync(channelId, skip: 0, take: 0);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _videoCallService.Verify(s => s.GetCallHistoryAsync(
            channelId, 0, 1, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_Unauthorized_ReturnsForbid()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 20, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not a member"));

        var result = await _controller.GetCallHistoryAsync(channelId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── GetCallAsync Tests ──────────────────────────────────────

    [TestMethod]
    public async Task GetCallAsync_Success_ReturnsOkWithCall()
    {
        var callId = Guid.NewGuid();
        var callDto = CreateTestCallDto(callId: callId);

        _videoCallService
            .Setup(s => s.GetCallByIdAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.GetCallAsync(callId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetCallAsync_NotFound_ReturnsNotFound()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallByIdAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoCallDto?)null);

        var result = await _controller.GetCallAsync(callId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetCallAsync_Unauthorized_ReturnsForbid()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetCallByIdAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not authorized"));

        var result = await _controller.GetCallAsync(callId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── GetActiveCallAsync Tests ────────────────────────────────

    [TestMethod]
    public async Task GetActiveCallAsync_HasActiveCall_ReturnsOkWithCall()
    {
        var channelId = Guid.NewGuid();
        var callDto = CreateTestCallDto(channelId: channelId);
        callDto = callDto with { State = "Active" };

        _videoCallService
            .Setup(s => s.GetActiveCallAsync(channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.GetActiveCallAsync(channelId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_NoActiveCall_ReturnsOkWithNullData()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetActiveCallAsync(channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoCallDto?)null);

        var result = await _controller.GetActiveCallAsync(channelId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_Unauthorized_ReturnsForbid()
    {
        var channelId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetActiveCallAsync(channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not a member"));

        var result = await _controller.GetActiveCallAsync(channelId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // ── Audio Call Flow Tests ───────────────────────────────────

    [TestMethod]
    public async Task InitiateCallAsync_AudioCall_Success()
    {
        var channelId = Guid.NewGuid();
        var request = new StartCallRequest { MediaType = "Audio" };
        var callDto = CreateTestCallDto(channelId: channelId) with { MediaType = "Audio" };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var result = await _controller.InitiateCallAsync(channelId, request);

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
    }

    // ── Service Invocation Verification Tests ───────────────────

    [TestMethod]
    public async Task LeaveCallAsync_InvokesServiceWithCorrectCallerId()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.LeaveCallAsync(callId);

        _videoCallService.Verify(s => s.LeaveCallAsync(
            callId,
            It.Is<CallerContext>(c => c.UserId == _userId && c.Type == CallerType.User),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task EndCallAsync_InvokesServiceWithCorrectCallerId()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.EndCallAsync(callId);

        _videoCallService.Verify(s => s.EndCallAsync(
            callId,
            It.Is<CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RejectCallAsync_InvokesServiceWithCorrectCallerId()
    {
        var callId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.RejectCallAsync(callId);

        _videoCallService.Verify(s => s.RejectCallAsync(
            callId,
            It.Is<CallerContext>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task JoinCallAsync_PassesRequestParametersCorrectly()
    {
        var callId = Guid.NewGuid();
        var request = new JoinCallRequest { WithAudio = false, WithVideo = true };
        var callDto = CreateTestCallDto(callId: callId);

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, request, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        await _controller.JoinCallAsync(callId, request);

        _videoCallService.Verify(s => s.JoinCallAsync(
            callId,
            It.Is<JoinCallRequest>(r => r.WithAudio == false && r.WithVideo == true),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
