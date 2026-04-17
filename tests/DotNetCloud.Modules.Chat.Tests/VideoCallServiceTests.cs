using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="VideoCallService"/>.
/// Uses in-memory EF Core provider and mocked dependencies.
/// </summary>
[TestClass]
public class VideoCallServiceTests
{
    private ChatDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChatMessageNotifier> _messageNotifierMock = null!;
    private Mock<ILiveKitService> _liveKitMock = null!;
    private VideoCallService _service = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;

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

        _service = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object);

        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
        _channelId = Guid.NewGuid();

        // Seed a channel with two members
        _db.Channels.Add(new Channel
        {
            Id = _channelId,
            Name = "Test Channel",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = _caller.UserId
        });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = _caller.UserId, Role = ChannelMemberRole.Owner });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = Guid.NewGuid(), Role = ChannelMemberRole.Member });
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private CallerContext CreateCaller() =>
        new(Guid.NewGuid(), ["user"], CallerType.User);

    private void SeedGroupChannel(Guid channelId, params Guid[] memberIds)
    {
        _db.Channels.Add(new Channel
        {
            Id = channelId,
            Name = "Group Channel",
            Type = ChannelType.Group,
            CreatedByUserId = memberIds[0]
        });
        foreach (var mid in memberIds)
        {
            _db.ChannelMembers.Add(new ChannelMember { ChannelId = channelId, UserId = mid, Role = ChannelMemberRole.Member });
        }
        _db.SaveChanges();
    }

    // ══════════════════════════════════════════════════════════════
    //  InitiateCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task InitiateCallAsync_ValidRequest_CreatesCallInRingingState()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        Assert.AreEqual("Ringing", result.State);
        Assert.AreEqual(_channelId, result.ChannelId);
        Assert.AreEqual(_caller.UserId, result.InitiatorUserId);
        Assert.AreEqual("Video", result.MediaType);
    }

    [TestMethod]
    public async Task InitiateCallAsync_AudioCall_SetsMediaTypeToAudio()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Audio" }, _caller);
        Assert.AreEqual("Audio", result.MediaType);
    }

    [TestMethod]
    public async Task InitiateCallAsync_AddsInitiatorAsParticipant()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        Assert.AreEqual(1, result.Participants.Count);
        var participant = result.Participants[0];
        Assert.AreEqual(_caller.UserId, participant.UserId);
        Assert.AreEqual("Initiator", participant.Role);
        Assert.IsTrue(participant.HasAudio);
        Assert.IsTrue(participant.HasVideo); // Video call → video enabled
    }

    [TestMethod]
    public async Task InitiateCallAsync_AudioCall_InitiatorHasNoVideo()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Audio" }, _caller);

        var participant = result.Participants[0];
        Assert.IsTrue(participant.HasAudio);
        Assert.IsFalse(participant.HasVideo);
    }

    [TestMethod]
    public async Task InitiateCallAsync_SetsMaxParticipantsToOne()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.AreEqual(1, result.MaxParticipants);
    }

    [TestMethod]
    public async Task InitiateCallAsync_TwoMemberChannel_IsGroupCallFalse()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.IsFalse(result.IsGroupCall);
    }

    [TestMethod]
    public async Task InitiateCallAsync_ThreeOrMoreMembers_IsGroupCallTrue()
    {
        var groupChannelId = Guid.NewGuid();
        SeedGroupChannel(groupChannelId, _caller.UserId, Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.IsTrue(result.IsGroupCall);
    }

    [TestMethod]
    public async Task InitiateCallAsync_PersistsCallToDatabase()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(result.Id);
        Assert.IsNotNull(dbCall);
        Assert.AreEqual(VideoCallState.Ringing, dbCall.State);
    }

    [TestMethod]
    public async Task InitiateCallAsync_PublishesVideoCallInitiatedEvent()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallInitiatedEvent>(e =>
                e.ChannelId == _channelId &&
                e.InitiatorUserId == _caller.UserId &&
                e.MediaType == "Video"),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateCallAsync_NotifiesCallRingingViaMessageNotifier()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        _messageNotifierMock.Verify(
            notifier => notifier.NotifyCallRinging(It.Is<CallRingingNotification>(n =>
                n.CallId == result.Id
                && n.ChannelId == _channelId
                && n.InitiatorUserId == _caller.UserId
                && n.MediaType == "Video")),
            Times.Once);
    }

    [TestMethod]
    public async Task InitiateCallAsync_InvalidMediaType_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Invalid" }, _caller));
    }

    [TestMethod]
    public async Task InitiateCallAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _service.InitiateCallAsync(_channelId, null!, _caller));
    }

    [TestMethod]
    public async Task InitiateCallAsync_NullCaller_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, null!));
    }

    [TestMethod]
    public async Task InitiateCallAsync_ActiveCallAlreadyExists_ThrowsInvalidOperationException()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller));
    }

    [TestMethod]
    public async Task InitiateCallAsync_PreviousCallEnded_AllowsNewCall()
    {
        var first = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.EndCallAsync(first.Id, _caller);

        var second = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual("Ringing", second.State);
    }

    [TestMethod]
    public async Task InitiateCallAsync_CaseInsensitiveMediaType_Works()
    {
        var result = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "video" }, _caller);
        Assert.AreEqual("Video", result.MediaType);
    }

    // ══════════════════════════════════════════════════════════════
    //  JoinCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task JoinCallAsync_RingingCall_TransitionsToActive()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();

        var result = await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true, WithVideo = true }, joiner);

        Assert.AreEqual("Active", result.State);
    }

    [TestMethod]
    public async Task JoinCallAsync_AddsParticipantToCall()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();

        var result = await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true, WithVideo = false }, joiner);

        Assert.AreEqual(2, result.Participants.Count);
        var newParticipant = result.Participants.First(p => p.UserId == joiner.UserId);
        Assert.AreEqual("Participant", newParticipant.Role);
        Assert.IsTrue(newParticipant.HasAudio);
        Assert.IsFalse(newParticipant.HasVideo);
    }

    [TestMethod]
    public async Task JoinCallAsync_FirstAnswer_PublishesVideoCallAnsweredEvent()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();

        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallAnsweredEvent>(e =>
                e.CallId == call.Id &&
                e.AnsweredByUserId == joiner.UserId),
                joiner,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task JoinCallAsync_AlwaysPublishesParticipantJoinedEvent()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();

        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<ParticipantJoinedCallEvent>(e =>
                e.CallId == call.Id &&
                e.UserId == joiner.UserId),
                joiner,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task JoinCallAsync_SetsStartedAtUtcOnFirstAnswer()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.IsNull(call.StartedAtUtc);

        var joiner = CreateCaller();
        var result = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        Assert.IsNotNull(result.StartedAtUtc);
    }

    [TestMethod]
    public async Task JoinCallAsync_SecondJoiner_DoesNotPublishAnsweredEventAgain()
    {
        var groupChannelId = Guid.NewGuid();
        var user1 = _caller;
        var user2 = CreateCaller();
        var user3 = CreateCaller();
        SeedGroupChannel(groupChannelId, user1.UserId, user2.UserId, user3.UserId);

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, user1);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user2);

        _eventBusMock.Invocations.Clear();

        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user3);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.IsAny<VideoCallAnsweredEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task JoinCallAsync_UpdatesMaxParticipants()
    {
        var groupChannelId = Guid.NewGuid();
        var user1 = _caller;
        var user2 = CreateCaller();
        var user3 = CreateCaller();
        SeedGroupChannel(groupChannelId, user1.UserId, user2.UserId, user3.UserId);

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, user1);
        Assert.AreEqual(1, call.MaxParticipants);

        var after2 = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user2);
        Assert.AreEqual(2, after2.MaxParticipants);

        var after3 = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user3);
        Assert.AreEqual(3, after3.MaxParticipants);
    }

    [TestMethod]
    public async Task JoinCallAsync_DuplicateJoin_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner));
    }

    [TestMethod]
    public async Task JoinCallAsync_EndedCall_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.EndCallAsync(call.Id, _caller);

        var joiner = CreateCaller();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner));
    }

    [TestMethod]
    public async Task JoinCallAsync_NonexistentCall_ThrowsInvalidOperationException()
    {
        var joiner = CreateCaller();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.JoinCallAsync(Guid.NewGuid(), new JoinCallRequest(), joiner));
    }

    [TestMethod]
    public async Task JoinCallAsync_CanRejoinAfterLeaving()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();

        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        await _service.LeaveCallAsync(call.Id, joiner);

        // Should be able to rejoin (the old participant record has LeftAtUtc set)
        var result = await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        var participants = result.Participants.Where(p => p.UserId == joiner.UserId).ToList();
        Assert.AreEqual(2, participants.Count); // Original (left) + rejoined
    }

    // ══════════════════════════════════════════════════════════════
    //  LeaveCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task LeaveCallAsync_SetsLeftAtUtc()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.LeaveCallAsync(call.Id, joiner);

        var participant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == joiner.UserId && cp.LeftAtUtc != null);
        Assert.IsNotNull(participant.LeftAtUtc);
    }

    [TestMethod]
    public async Task LeaveCallAsync_PublishesParticipantLeftCallEvent()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.LeaveCallAsync(call.Id, joiner);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<ParticipantLeftCallEvent>(e =>
                e.CallId == call.Id &&
                e.UserId == joiner.UserId),
                joiner,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LeaveCallAsync_LastParticipantLeaves_AutoEndsCall()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.LeaveCallAsync(call.Id, joiner);
        await _service.LeaveCallAsync(call.Id, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
        Assert.AreEqual(VideoCallEndReason.Normal, dbCall.EndReason);
    }

    [TestMethod]
    public async Task LeaveCallAsync_NotLastParticipant_CallRemainsActive()
    {
        var groupChannelId = Guid.NewGuid();
        var user1 = _caller;
        var user2 = CreateCaller();
        var user3 = CreateCaller();
        SeedGroupChannel(groupChannelId, user1.UserId, user2.UserId, user3.UserId);

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, user1);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), user3);

        await _service.LeaveCallAsync(call.Id, user3);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Active, dbCall!.State);
    }

    [TestMethod]
    public async Task LeaveCallAsync_UserNotInCall_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var stranger = CreateCaller();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.LeaveCallAsync(call.Id, stranger));
    }

    [TestMethod]
    public async Task LeaveCallAsync_NonexistentCall_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.LeaveCallAsync(Guid.NewGuid(), _caller));
    }

    // ══════════════════════════════════════════════════════════════
    //  EndCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task EndCallAsync_ActiveCall_TransitionsToEnded()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.EndCallAsync(call.Id, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
        Assert.AreEqual(VideoCallEndReason.Normal, dbCall.EndReason);
        Assert.IsNotNull(dbCall.EndedAtUtc);
    }

    [TestMethod]
    public async Task EndCallAsync_RingingCall_SetsEndReasonToCancelled()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        await _service.EndCallAsync(call.Id, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallEndReason.Cancelled, dbCall!.EndReason);
    }

    [TestMethod]
    public async Task EndCallAsync_MarksAllActiveParticipantsAsLeft()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.EndCallAsync(call.Id, _caller);

        var allLeft = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == call.Id)
            .AllAsync(cp => cp.LeftAtUtc != null);
        Assert.IsTrue(allLeft);
    }

    [TestMethod]
    public async Task EndCallAsync_PublishesVideoCallEndedEvent()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.EndCallAsync(call.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallEndedEvent>(e =>
                e.CallId == call.Id &&
                e.ChannelId == _channelId &&
                e.EndReason == "Normal"),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task EndCallAsync_AlreadyEnded_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.EndCallAsync(call.Id, _caller);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.EndCallAsync(call.Id, _caller));
    }

    [TestMethod]
    public async Task EndCallAsync_NonexistentCall_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.EndCallAsync(Guid.NewGuid(), _caller));
    }

    [TestMethod]
    public async Task EndCallAsync_CalculatesDurationForActiveCall()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        await _service.EndCallAsync(call.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallEndedEvent>(e =>
                e.DurationSeconds != null && e.DurationSeconds >= 0),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════
    //  RejectCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task RejectCallAsync_OneToOneCall_TransitionsToRejected()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var callee = CreateCaller();

        await _service.RejectCallAsync(call.Id, callee);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Rejected, dbCall!.State);
        Assert.AreEqual(VideoCallEndReason.Rejected, dbCall.EndReason);
        Assert.IsNotNull(dbCall.EndedAtUtc);
    }

    [TestMethod]
    public async Task RejectCallAsync_OneToOneCall_PublishesEndedEventWithRejectedReason()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var callee = CreateCaller();

        await _service.RejectCallAsync(call.Id, callee);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallEndedEvent>(e =>
                e.CallId == call.Id &&
                e.EndReason == "Rejected"),
                callee,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RejectCallAsync_OneToOneCall_MarksParticipantsAsLeft()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var callee = CreateCaller();

        await _service.RejectCallAsync(call.Id, callee);

        var allLeft = await _db.CallParticipants
            .Where(cp => cp.VideoCallId == call.Id)
            .AllAsync(cp => cp.LeftAtUtc != null);
        Assert.IsTrue(allLeft);
    }

    [TestMethod]
    public async Task RejectCallAsync_GroupCall_DoesNotEndCall()
    {
        var groupChannelId = Guid.NewGuid();
        var user1 = _caller;
        var user2 = CreateCaller();
        var user3 = CreateCaller();
        SeedGroupChannel(groupChannelId, user1.UserId, user2.UserId, user3.UserId);

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, user1);

        await _service.RejectCallAsync(call.Id, user2);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ringing, dbCall!.State); // Still ringing
    }

    [TestMethod]
    public async Task RejectCallAsync_NonRingingCall_ThrowsInvalidOperationException()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        // Call is now Active
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.RejectCallAsync(call.Id, CreateCaller()));
    }

    [TestMethod]
    public async Task RejectCallAsync_NonexistentCall_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.RejectCallAsync(Guid.NewGuid(), _caller));
    }

    // ══════════════════════════════════════════════════════════════
    //  GetCallHistoryAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetCallHistoryAsync_ReturnsCallsForChannel()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);

        Assert.AreEqual(1, history.Count);
        Assert.AreEqual(_channelId, history[0].ChannelId);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_OrdersByCreatedAtDescending()
    {
        var call1 = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.EndCallAsync(call1.Id, _caller);

        var call2 = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Audio" }, _caller);
        await _service.EndCallAsync(call2.Id, _caller);

        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);

        Assert.AreEqual(2, history.Count);
        Assert.IsTrue(history[0].CreatedAtUtc >= history[1].CreatedAtUtc);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_SupportsPagination()
    {
        for (int i = 0; i < 5; i++)
        {
            var c = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
            await _service.EndCallAsync(c.Id, _caller);
        }

        var page1 = await _service.GetCallHistoryAsync(_channelId, 0, 2, _caller);
        var page2 = await _service.GetCallHistoryAsync(_channelId, 2, 2, _caller);

        Assert.AreEqual(2, page1.Count);
        Assert.AreEqual(2, page2.Count);
        Assert.AreNotEqual(page1[0].Id, page2[0].Id);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_EmptyChannel_ReturnsEmptyList()
    {
        var history = await _service.GetCallHistoryAsync(Guid.NewGuid(), 0, 10, _caller);
        Assert.AreEqual(0, history.Count);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_NegativeSkip_DefaultsToZero()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var history = await _service.GetCallHistoryAsync(_channelId, -5, 10, _caller);
        Assert.AreEqual(1, history.Count);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_ZeroTake_DefaultsToTwenty()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var history = await _service.GetCallHistoryAsync(_channelId, 0, 0, _caller);
        Assert.AreEqual(1, history.Count); // Default take = 20, and we have 1 call
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_TakeExceedsMax_CapsAtHundred()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var history = await _service.GetCallHistoryAsync(_channelId, 0, 200, _caller);
        Assert.AreEqual(1, history.Count);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_IncludesParticipantCount()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        await _service.EndCallAsync(call.Id, _caller);

        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);
        Assert.AreEqual(2, history[0].ParticipantCount);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_EndedCall_IncludesDurationSeconds()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);
        await _service.EndCallAsync(call.Id, _caller);

        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);
        Assert.IsNotNull(history[0].DurationSeconds);
        Assert.IsTrue(history[0].DurationSeconds >= 0);
    }

    [TestMethod]
    public async Task GetCallHistoryAsync_MissedCall_DurationIsNull()
    {
        // Manually create a missed call (the timeout handler sets this)
        var call = new VideoCall
        {
            ChannelId = _channelId,
            InitiatorUserId = _caller.UserId,
            State = VideoCallState.Missed,
            EndReason = VideoCallEndReason.Missed,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndedAtUtc = DateTime.UtcNow.AddMinutes(-4)
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);
        var missedEntry = history.First(h => h.Id == call.Id);
        Assert.IsNull(missedEntry.DurationSeconds); // StartedAtUtc is null for missed calls
    }

    // ══════════════════════════════════════════════════════════════
    //  GetActiveCallAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetActiveCallAsync_RingingCall_ReturnsCall()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var active = await _service.GetActiveCallAsync(_channelId, _caller);

        Assert.IsNotNull(active);
        Assert.AreEqual(call.Id, active.Id);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_ActiveCall_ReturnsCall()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        var active = await _service.GetActiveCallAsync(_channelId, _caller);

        Assert.IsNotNull(active);
        Assert.AreEqual("Active", active.State);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_NoActiveCall_ReturnsNull()
    {
        var result = await _service.GetActiveCallAsync(_channelId, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_EndedCall_ReturnsNull()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.EndCallAsync(call.Id, _caller);

        var result = await _service.GetActiveCallAsync(_channelId, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetActiveCallAsync_IncludesParticipants()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        var active = await _service.GetActiveCallAsync(_channelId, _caller);

        Assert.IsNotNull(active);
        Assert.AreEqual(2, active.Participants.Count);
    }

    // ══════════════════════════════════════════════════════════════
    //  HandleRingTimeoutsAsync Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_TimedOutCall_TransitionsToMissed()
    {
        // Create a call that was started more than 30 seconds ago
        var call = new VideoCall
        {
            ChannelId = _channelId,
            InitiatorUserId = _caller.UserId,
            State = VideoCallState.Ringing,
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(-35) // Older than timeout
        };
        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = _caller.UserId,
            Role = CallParticipantRole.Initiator
        };
        _db.VideoCalls.Add(call);
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        var count = await _service.HandleRingTimeoutsAsync();

        Assert.AreEqual(1, count);
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Missed, dbCall!.State);
        Assert.AreEqual(VideoCallEndReason.Missed, dbCall.EndReason);
        Assert.IsNotNull(dbCall.EndedAtUtc);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_RecentCall_DoesNotTimeout()
    {
        await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        var count = await _service.HandleRingTimeoutsAsync();

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_PublishesMissedEvent()
    {
        var call = new VideoCall
        {
            ChannelId = _channelId,
            InitiatorUserId = _caller.UserId,
            State = VideoCallState.Ringing,
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(-35)
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        await _service.HandleRingTimeoutsAsync();

        _eventBusMock.Verify(
            eb => eb.PublishAsync(It.Is<VideoCallMissedEvent>(e =>
                e.CallId == call.Id &&
                e.ChannelId == _channelId &&
                e.InitiatorUserId == _caller.UserId),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_MarksParticipantsAsLeft()
    {
        var call = new VideoCall
        {
            ChannelId = _channelId,
            InitiatorUserId = _caller.UserId,
            State = VideoCallState.Ringing,
            CreatedAtUtc = DateTime.UtcNow.AddSeconds(-35)
        };
        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = _caller.UserId,
            Role = CallParticipantRole.Initiator
        };
        _db.VideoCalls.Add(call);
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        await _service.HandleRingTimeoutsAsync();

        var dbParticipant = await _db.CallParticipants.FindAsync(participant.Id);
        Assert.IsNotNull(dbParticipant!.LeftAtUtc);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_MultipleTimedOutCalls_TransitionsAll()
    {
        for (int i = 0; i < 3; i++)
        {
            _db.VideoCalls.Add(new VideoCall
            {
                ChannelId = Guid.NewGuid(),
                InitiatorUserId = Guid.NewGuid(),
                State = VideoCallState.Ringing,
                CreatedAtUtc = DateTime.UtcNow.AddSeconds(-40)
            });
        }
        await _db.SaveChangesAsync();

        var count = await _service.HandleRingTimeoutsAsync();
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_ActiveCall_NotAffected()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var joiner = CreateCaller();
        await _service.JoinCallAsync(call.Id, new JoinCallRequest(), joiner);

        // Manually set CreatedAtUtc to simulate old call
        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        dbCall!.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await _db.SaveChangesAsync();

        var count = await _service.HandleRingTimeoutsAsync();

        Assert.AreEqual(0, count); // Active calls are not affected
        var reloadedCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Active, reloadedCall!.State);
    }

    [TestMethod]
    public async Task HandleRingTimeoutsAsync_NoCalls_ReturnsZero()
    {
        var count = await _service.HandleRingTimeoutsAsync();
        Assert.AreEqual(0, count);
    }

    // ══════════════════════════════════════════════════════════════
    //  Integration / Edge Cases
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task FullCallLifecycle_Initiate_Join_Leave_End()
    {
        // Initiate
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        Assert.AreEqual("Ringing", call.State);

        // Join
        var joiner = CreateCaller();
        var joined = await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true, WithVideo = true }, joiner);
        Assert.AreEqual("Active", joined.State);

        // Leave (one participant)
        await _service.LeaveCallAsync(call.Id, joiner);
        var activeCall = await _service.GetActiveCallAsync(_channelId, _caller);
        Assert.IsNotNull(activeCall); // Still active (initiator is still in)

        // End
        await _service.EndCallAsync(call.Id, _caller);
        var endedCall = await _service.GetActiveCallAsync(_channelId, _caller);
        Assert.IsNull(endedCall);

        // Verify history
        var history = await _service.GetCallHistoryAsync(_channelId, 0, 10, _caller);
        Assert.AreEqual(1, history.Count);
        Assert.AreEqual("Ended", history[0].State);
    }

    [TestMethod]
    public async Task FullCallLifecycle_Initiate_Reject_OneToOne()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Audio" }, _caller);
        var callee = CreateCaller();

        await _service.RejectCallAsync(call.Id, callee);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Rejected, dbCall!.State);

        var active = await _service.GetActiveCallAsync(_channelId, _caller);
        Assert.IsNull(active);
    }

    [TestMethod]
    public async Task FullCallLifecycle_GroupCall_ThreeParticipants()
    {
        var groupChannelId = Guid.NewGuid();
        var user1 = _caller;
        var user2 = CreateCaller();
        var user3 = CreateCaller();
        SeedGroupChannel(groupChannelId, user1.UserId, user2.UserId, user3.UserId);

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, user1);
        Assert.IsTrue(call.IsGroupCall);

        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true, WithVideo = true }, user2);
        var afterSecondJoin = await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true, WithVideo = true }, user3);

        Assert.AreEqual(3, afterSecondJoin.MaxParticipants);
        Assert.AreEqual(3, afterSecondJoin.Participants.Count);

        await _service.LeaveCallAsync(call.Id, user3);
        await _service.LeaveCallAsync(call.Id, user2);
        await _service.LeaveCallAsync(call.Id, user1);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }

    [TestMethod]
    public async Task ConcurrentCallsInDifferentChannels_Allowed()
    {
        var channel2Id = Guid.NewGuid();
        _db.Channels.Add(new Channel { Id = channel2Id, Name = "Channel 2", Type = ChannelType.Public, CreatedByUserId = _caller.UserId });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel2Id, UserId = _caller.UserId });
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel2Id, UserId = Guid.NewGuid() });
        await _db.SaveChangesAsync();

        var call1 = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var call2 = await _service.InitiateCallAsync(channel2Id, new StartCallRequest { MediaType = "Video" }, _caller);

        Assert.AreNotEqual(call1.Id, call2.Id);
        Assert.AreEqual("Ringing", call1.State);
        Assert.AreEqual("Ringing", call2.State);
    }

    [TestMethod]
    public async Task DtoMapping_AllFieldsPopulatedCorrectly()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);

        Assert.AreNotEqual(Guid.Empty, call.Id);
        Assert.AreEqual(_channelId, call.ChannelId);
        Assert.AreEqual(_caller.UserId, call.InitiatorUserId);
        Assert.AreEqual("Ringing", call.State);
        Assert.AreEqual("Video", call.MediaType);
        Assert.IsFalse(call.IsGroupCall);
        Assert.IsNull(call.StartedAtUtc);
        Assert.IsNull(call.EndedAtUtc);
        Assert.IsNull(call.EndReason);
        Assert.AreEqual(1, call.MaxParticipants);
        Assert.IsTrue(call.CreatedAtUtc > DateTime.MinValue);
    }

    // ══════════════════════════════════════════════════════════════
    //  LiveKit Auto-Escalation Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task JoinCallAsync_ThirdParticipant_NoEscalation_NullLiveKitRoom()
    {
        // P2P limit = 3, so joining as 3rd participant should NOT escalate
        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        var result = await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNull(dbCall!.LiveKitRoomId, "3 participants should stay in P2P mesh without LiveKit.");
    }

    [TestMethod]
    public async Task JoinCallAsync_FourthParticipant_LiveKitNotAvailable_ThrowsInvalidOperation()
    {
        // P2P limit = 3, LiveKit NOT available → 4th should fail
        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4));

        Assert.IsTrue(ex.Message.Contains("LiveKit"), "Error should mention LiveKit.");
        Assert.IsTrue(ex.Message.Contains("3"), "Error should mention the P2P limit.");
    }

    [TestMethod]
    public async Task JoinCallAsync_FourthParticipant_LiveKitAvailable_EscalatesToSFU()
    {
        // P2P limit = 3, LiveKit IS available → 4th should trigger escalation
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid callId, int _, CancellationToken _) => $"call-{callId}");

        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(dbCall!.LiveKitRoomId, "4th participant should trigger LiveKit room creation.");
        Assert.IsTrue(dbCall.LiveKitRoomId.StartsWith("call-"), "Room name should follow call-{id} pattern.");
    }

    [TestMethod]
    public async Task JoinCallAsync_EscalationOnlyHappensOnce()
    {
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid callId, int _, CancellationToken _) => $"call-{callId}");

        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        var caller5 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, caller5.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller5);

        _liveKitMock.Verify(
            x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "CreateRoomAsync should only be called once (on first escalation).");
    }

    [TestMethod]
    public async Task EndCallAsync_WithLiveKitRoom_DeletesRoom()
    {
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("call-room-123");
        _liveKitMock.Setup(x => x.DeleteRoomAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4);

        await _service.EndCallAsync(call.Id, _caller);

        _liveKitMock.Verify(
            x => x.DeleteRoomAsync("call-room-123", It.IsAny<CancellationToken>()),
            Times.Once,
            "EndCallAsync should clean up the LiveKit room.");
    }

    [TestMethod]
    public async Task EndCallAsync_WithoutLiveKitRoom_DoesNotCallDeleteRoom()
    {
        var call = await _service.InitiateCallAsync(_channelId, new StartCallRequest { MediaType = "Video" }, _caller);
        var caller2 = CreateCaller();
        _db.ChannelMembers.Add(new ChannelMember { ChannelId = _channelId, UserId = caller2.UserId, Role = ChannelMemberRole.Member });
        await _db.SaveChangesAsync();

        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.EndCallAsync(call.Id, _caller);

        _liveKitMock.Verify(
            x => x.DeleteRoomAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not attempt to delete a LiveKit room when none was created.");
    }

    [TestMethod]
    public async Task LeaveCallAsync_LastParticipant_WithLiveKitRoom_CleansUpRoom()
    {
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("call-room-leave");
        _liveKitMock.Setup(x => x.DeleteRoomAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4);

        // All participants leave → triggers auto-end → should clean up LiveKit room
        await _service.LeaveCallAsync(call.Id, _caller);
        await _service.LeaveCallAsync(call.Id, caller2);
        await _service.LeaveCallAsync(call.Id, caller3);
        await _service.LeaveCallAsync(call.Id, caller4);

        _liveKitMock.Verify(
            x => x.DeleteRoomAsync("call-room-leave", It.IsAny<CancellationToken>()),
            Times.Once,
            "Auto-end from last participant leaving should clean up LiveKit room.");
    }

    [TestMethod]
    public async Task EndCallAsync_LiveKitDeleteFails_DoesNotThrow()
    {
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("call-room-fail");
        _liveKitMock.Setup(x => x.DeleteRoomAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LiveKit server unreachable"));

        var groupChannelId = Guid.NewGuid();
        var caller2 = CreateCaller();
        var caller3 = CreateCaller();
        var caller4 = CreateCaller();
        SeedGroupChannel(groupChannelId, _caller.UserId, caller2.UserId, caller3.UserId, caller4.UserId, Guid.NewGuid());

        var call = await _service.InitiateCallAsync(groupChannelId, new StartCallRequest { MediaType = "Video" }, _caller);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller2);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller3);
        await _service.JoinCallAsync(call.Id, new JoinCallRequest { WithAudio = true }, caller4);

        // Should not throw even though LiveKit delete fails
        await _service.EndCallAsync(call.Id, _caller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State, "Call should still end even if LiveKit cleanup fails.");
    }
}
