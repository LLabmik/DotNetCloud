using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Host.Protos;
using DotNetCloud.Modules.Chat.Host.Services;
using DotNetCloud.Modules.Chat.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for video call gRPC service methods in <see cref="ChatGrpcService"/>.
/// </summary>
[TestClass]
public class VideoCallGrpcServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IChannelService> _channelService = null!;
    private Mock<IVideoCallService> _videoCallService = null!;
    private ChatGrpcService _service = null!;
    private ServerCallContext _context = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _channelService = new Mock<IChannelService>();
        _videoCallService = new Mock<IVideoCallService>();

        _service = new ChatGrpcService(
            _db,
            _channelService.Object,
            _videoCallService.Object,
            NullLogger<ChatGrpcService>.Instance);

        _context = new MockServerCallContext();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
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
            Participants =
            [
                new CallParticipantDto
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    Role = "Initiator",
                    JoinedAtUtc = DateTime.UtcNow,
                    HasAudio = true,
                    HasVideo = true,
                    HasScreenShare = false
                }
            ]
        };
    }

    // ── InitiateVideoCall Tests ─────────────────────────────────

    [TestMethod]
    public async Task InitiateVideoCall_ValidRequest_ReturnsSuccess()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callDto = CreateTestCallDto(channelId: channelId);

        _videoCallService
            .Setup(s => s.InitiateCallAsync(
                channelId,
                It.IsAny<StartCallRequest>(),
                It.Is<CallerContext>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Call);
        Assert.AreEqual(callDto.Id.ToString(), result.Call.Id);
        Assert.AreEqual("Video", result.Call.MediaType);
    }

    [TestMethod]
    public async Task InitiateVideoCall_InvalidChannelId_ReturnsError()
    {
        var request = new InitiateVideoCallRequest
        {
            ChannelId = "not-a-guid",
            UserId = Guid.NewGuid().ToString(),
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid ID format.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task InitiateVideoCall_InvalidUserId_ReturnsError()
    {
        var request = new InitiateVideoCallRequest
        {
            ChannelId = Guid.NewGuid().ToString(),
            UserId = "bad-user-id",
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid ID format.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task InitiateVideoCall_ServiceThrowsArgument_ReturnsError()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, It.IsAny<StartCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid media type"));

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "BadType"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "Invalid media type");
    }

    [TestMethod]
    public async Task InitiateVideoCall_ServiceThrowsInvalidOperation_ReturnsError()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, It.IsAny<StartCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Active call exists"));

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "Active call exists");
    }

    [TestMethod]
    public async Task InitiateVideoCall_PassesMediaTypeToService()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callDto = CreateTestCallDto(channelId: channelId);

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, It.IsAny<StartCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "Audio"
        };

        await _service.InitiateVideoCall(request, _context);

        _videoCallService.Verify(s => s.InitiateCallAsync(
            channelId,
            It.Is<StartCallRequest>(r => r.MediaType == "Audio"),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── JoinVideoCall Tests ─────────────────────────────────────

    [TestMethod]
    public async Task JoinVideoCall_ValidRequest_ReturnsSuccess()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callDto = CreateTestCallDto(callId: callId);

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, It.IsAny<JoinCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new JoinVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            WithAudio = true,
            WithVideo = false
        };

        var result = await _service.JoinVideoCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Call);
    }

    [TestMethod]
    public async Task JoinVideoCall_InvalidCallId_ReturnsError()
    {
        var request = new JoinVideoCallRequest
        {
            CallId = "bad-id",
            UserId = Guid.NewGuid().ToString(),
            WithAudio = true
        };

        var result = await _service.JoinVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Invalid ID format.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task JoinVideoCall_PassesMediaFlagsToService()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callDto = CreateTestCallDto(callId: callId);

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, It.IsAny<JoinCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new JoinVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString(),
            WithAudio = false,
            WithVideo = true
        };

        await _service.JoinVideoCall(request, _context);

        _videoCallService.Verify(s => s.JoinCallAsync(
            callId,
            It.Is<JoinCallRequest>(r => r.WithAudio == false && r.WithVideo == true),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task JoinVideoCall_ServiceThrows_ReturnsError()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.JoinCallAsync(callId, It.IsAny<JoinCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call not active"));

        var request = new JoinVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.JoinVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    // ── LeaveVideoCall Tests ────────────────────────────────────

    [TestMethod]
    public async Task LeaveVideoCall_ValidRequest_ReturnsSuccess()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new LeaveVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.LeaveVideoCall(request, _context);

        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task LeaveVideoCall_InvalidIds_ReturnsError()
    {
        var request = new LeaveVideoCallRequest
        {
            CallId = "invalid",
            UserId = "also-invalid"
        };

        var result = await _service.LeaveVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task LeaveVideoCall_ServiceThrows_ReturnsError()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.LeaveCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Not in call"));

        var request = new LeaveVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.LeaveVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    // ── EndVideoCall Tests ──────────────────────────────────────

    [TestMethod]
    public async Task EndVideoCall_ValidRequest_ReturnsSuccess()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new EndVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.EndVideoCall(request, _context);

        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task EndVideoCall_InvalidCallId_ReturnsError()
    {
        var request = new EndVideoCallRequest
        {
            CallId = "bad",
            UserId = Guid.NewGuid().ToString()
        };

        var result = await _service.EndVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task EndVideoCall_ServiceThrowsUnauthorized_ReturnsError()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.EndCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Only initiator can end"));

        var request = new EndVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.EndVideoCall(request, _context);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "Only initiator can end");
    }

    // ── RejectVideoCall Tests ───────────────────────────────────

    [TestMethod]
    public async Task RejectVideoCall_ValidRequest_ReturnsSuccess()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new RejectVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.RejectVideoCall(request, _context);

        Assert.IsTrue(result.Success);
    }

    [TestMethod]
    public async Task RejectVideoCall_InvalidIds_ReturnsError()
    {
        var request = new RejectVideoCallRequest
        {
            CallId = "nope",
            UserId = "invalid"
        };

        var result = await _service.RejectVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task RejectVideoCall_ServiceThrows_ReturnsError()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.RejectCallAsync(callId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Call cannot be rejected"));

        var request = new RejectVideoCallRequest
        {
            CallId = callId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.RejectVideoCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    // ── GetCallHistory Tests ────────────────────────────────────

    [TestMethod]
    public async Task GetCallHistory_ValidRequest_ReturnsHistory()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
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
                DurationSeconds = 300,
                ParticipantCount = 3,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                InitiatorUserId = Guid.NewGuid(),
                State = "Missed",
                MediaType = "Audio",
                EndReason = "Missed",
                DurationSeconds = null,
                ParticipantCount = 1,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
            }
        };

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 20, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var request = new GetCallHistoryRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            Skip = 0,
            Take = 20
        };

        var result = await _service.GetCallHistory(request, _context);

        Assert.AreEqual(2, result.Calls.Count);
        Assert.AreEqual("Ended", result.Calls[0].State);
        Assert.AreEqual("Missed", result.Calls[1].State);
        Assert.AreEqual(300, result.Calls[0].DurationSeconds);
        Assert.AreEqual(0, result.Calls[1].DurationSeconds); // null → 0 in proto
    }

    [TestMethod]
    public async Task GetCallHistory_InvalidChannelId_ReturnsEmpty()
    {
        var request = new GetCallHistoryRequest
        {
            ChannelId = "invalid",
            UserId = Guid.NewGuid().ToString(),
            Skip = 0,
            Take = 20
        };

        var result = await _service.GetCallHistory(request, _context);

        Assert.AreEqual(0, result.Calls.Count);
    }

    [TestMethod]
    public async Task GetCallHistory_InvalidUserId_ReturnsEmpty()
    {
        var request = new GetCallHistoryRequest
        {
            ChannelId = Guid.NewGuid().ToString(),
            UserId = "bad-user",
            Skip = 0,
            Take = 20
        };

        var result = await _service.GetCallHistory(request, _context);

        Assert.AreEqual(0, result.Calls.Count);
    }

    // ── GetActiveCall Tests ─────────────────────────────────────

    [TestMethod]
    public async Task GetActiveCall_HasActiveCall_ReturnsCallData()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callDto = CreateTestCallDto(channelId: channelId) with { State = "Active" };

        _videoCallService
            .Setup(s => s.GetActiveCallAsync(channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new GetActiveCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.GetActiveCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Call);
        Assert.AreEqual("Active", result.Call.State);
    }

    [TestMethod]
    public async Task GetActiveCall_NoActiveCall_ReturnsSuccessWithNullCall()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _videoCallService
            .Setup(s => s.GetActiveCallAsync(channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoCallDto?)null);

        var request = new GetActiveCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString()
        };

        var result = await _service.GetActiveCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.Call);
    }

    [TestMethod]
    public async Task GetActiveCall_InvalidIds_ReturnsError()
    {
        var request = new GetActiveCallRequest
        {
            ChannelId = "invalid",
            UserId = "also-invalid"
        };

        var result = await _service.GetActiveCall(request, _context);

        Assert.IsFalse(result.Success);
    }

    // ── Message Mapping Tests ───────────────────────────────────

    [TestMethod]
    public async Task InitiateVideoCall_MapsParticipantsCorrectly()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var participantUserId = Guid.NewGuid();
        var joinedAt = DateTime.UtcNow;

        var callDto = new VideoCallDto
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            InitiatorUserId = userId,
            State = "Ringing",
            MediaType = "Video",
            IsGroupCall = true,
            MaxParticipants = 4,
            CreatedAtUtc = DateTime.UtcNow,
            Participants =
            [
                new CallParticipantDto
                {
                    Id = participantId,
                    UserId = participantUserId,
                    Role = "Initiator",
                    JoinedAtUtc = joinedAt,
                    LeftAtUtc = null,
                    HasAudio = true,
                    HasVideo = false,
                    HasScreenShare = true
                }
            ]
        };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, It.IsAny<StartCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Call.Participants.Count);

        var participant = result.Call.Participants[0];
        Assert.AreEqual(participantId.ToString(), participant.Id);
        Assert.AreEqual(participantUserId.ToString(), participant.UserId);
        Assert.AreEqual("Initiator", participant.Role);
        Assert.IsTrue(participant.HasAudio);
        Assert.IsFalse(participant.HasVideo);
        Assert.IsTrue(participant.HasScreenShare);
        Assert.AreEqual(string.Empty, participant.LeftAtUtc);
    }

    [TestMethod]
    public async Task GetCallHistory_MapsHistoryFieldsCorrectly()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var history = new List<CallHistoryDto>
        {
            new()
            {
                Id = callId,
                ChannelId = channelId,
                InitiatorUserId = initiatorId,
                State = "Ended",
                MediaType = "Audio",
                EndReason = "Normal",
                DurationSeconds = 60,
                ParticipantCount = 2,
                CreatedAtUtc = createdAt
            }
        };

        _videoCallService
            .Setup(s => s.GetCallHistoryAsync(channelId, 0, 10, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        var request = new GetCallHistoryRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            Skip = 0,
            Take = 10
        };

        var result = await _service.GetCallHistory(request, _context);

        Assert.AreEqual(1, result.Calls.Count);
        var entry = result.Calls[0];
        Assert.AreEqual(callId.ToString(), entry.Id);
        Assert.AreEqual(channelId.ToString(), entry.ChannelId);
        Assert.AreEqual(initiatorId.ToString(), entry.InitiatorUserId);
        Assert.AreEqual("Ended", entry.State);
        Assert.AreEqual("Audio", entry.MediaType);
        Assert.AreEqual("Normal", entry.EndReason);
        Assert.AreEqual(60, entry.DurationSeconds);
        Assert.AreEqual(2, entry.ParticipantCount);
    }

    [TestMethod]
    public async Task InitiateVideoCall_MapsCallFieldsCorrectly()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var callId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        var callDto = new VideoCallDto
        {
            Id = callId,
            ChannelId = channelId,
            InitiatorUserId = userId,
            State = "Active",
            MediaType = "Video",
            IsGroupCall = true,
            StartedAtUtc = startedAt,
            EndedAtUtc = null,
            EndReason = null,
            MaxParticipants = 3,
            CreatedAtUtc = DateTime.UtcNow,
            Participants = []
        };

        _videoCallService
            .Setup(s => s.InitiateCallAsync(channelId, It.IsAny<StartCallRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(callDto);

        var request = new InitiateVideoCallRequest
        {
            ChannelId = channelId.ToString(),
            UserId = userId.ToString(),
            MediaType = "Video"
        };

        var result = await _service.InitiateVideoCall(request, _context);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(callId.ToString(), result.Call.Id);
        Assert.AreEqual(channelId.ToString(), result.Call.ChannelId);
        Assert.AreEqual(userId.ToString(), result.Call.InitiatorUserId);
        Assert.AreEqual("Active", result.Call.State);
        Assert.IsTrue(result.Call.IsGroupCall);
        Assert.AreEqual(3, result.Call.MaxParticipants);
        Assert.AreNotEqual(string.Empty, result.Call.StartedAtUtc);
        Assert.AreEqual(string.Empty, result.Call.EndedAtUtc);
        Assert.AreEqual(string.Empty, result.Call.EndReason);
    }

    /// <summary>
    /// Minimal mock of <see cref="ServerCallContext"/> for unit testing gRPC services.
    /// </summary>
    private sealed class MockServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get => Status.DefaultSuccess; set { } }
        protected override WriteOptions? WriteOptionsCore { get => null; set { } }
        protected override AuthContext AuthContextCore => new("test", []);
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
