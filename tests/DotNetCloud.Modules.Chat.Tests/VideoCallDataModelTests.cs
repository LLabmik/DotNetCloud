using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="VideoCall"/> entity model defaults and properties.
/// </summary>
[TestClass]
public class VideoCallModelTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var call = new VideoCall();
        Assert.AreNotEqual(Guid.Empty, call.Id);
    }

    [TestMethod]
    public void WhenCreatedThenEachInstanceGetsUniqueId()
    {
        var call1 = new VideoCall();
        var call2 = new VideoCall();
        Assert.AreNotEqual(call1.Id, call2.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultStateIsRinging()
    {
        var call = new VideoCall();
        Assert.AreEqual(VideoCallState.Ringing, call.State);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultMediaTypeIsVideo()
    {
        var call = new VideoCall();
        Assert.AreEqual(CallMediaType.Video, call.MediaType);
    }

    [TestMethod]
    public void WhenCreatedThenStartedAtUtcIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.StartedAtUtc);
    }

    [TestMethod]
    public void WhenCreatedThenEndedAtUtcIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.EndedAtUtc);
    }

    [TestMethod]
    public void WhenCreatedThenEndReasonIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.EndReason);
    }

    [TestMethod]
    public void WhenCreatedThenMaxParticipantsIsZero()
    {
        var call = new VideoCall();
        Assert.AreEqual(0, call.MaxParticipants);
    }

    [TestMethod]
    public void WhenCreatedThenIsGroupCallIsFalse()
    {
        var call = new VideoCall();
        Assert.IsFalse(call.IsGroupCall);
    }

    [TestMethod]
    public void WhenCreatedThenLiveKitRoomIdIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.LiveKitRoomId);
    }

    [TestMethod]
    public void WhenCreatedThenCreatedAtUtcIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var call = new VideoCall();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(call.CreatedAtUtc >= before && call.CreatedAtUtc <= after);
    }

    [TestMethod]
    public void WhenCreatedThenIsDeletedIsFalse()
    {
        var call = new VideoCall();
        Assert.IsFalse(call.IsDeleted);
    }

    [TestMethod]
    public void WhenCreatedThenDeletedAtIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.DeletedAt);
    }

    [TestMethod]
    public void WhenCreatedThenParticipantsCollectionIsEmpty()
    {
        var call = new VideoCall();
        Assert.AreEqual(0, call.Participants.Count);
    }

    [TestMethod]
    public void WhenCreatedThenChannelIsNull()
    {
        var call = new VideoCall();
        Assert.IsNull(call.Channel);
    }

    [TestMethod]
    public void WhenCreatedThenChannelIdIsEmpty()
    {
        var call = new VideoCall();
        Assert.AreEqual(Guid.Empty, call.ChannelId);
    }

    [TestMethod]
    public void WhenCreatedThenInitiatorUserIdIsEmpty()
    {
        var call = new VideoCall();
        Assert.AreEqual(Guid.Empty, call.InitiatorUserId);
    }

    [TestMethod]
    public void WhenPropertiesSetThenValuesArePersisted()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var call = new VideoCall
        {
            ChannelId = channelId,
            InitiatorUserId = userId,
            State = VideoCallState.Active,
            MediaType = CallMediaType.Audio,
            StartedAtUtc = now,
            MaxParticipants = 3,
            IsGroupCall = true,
            LiveKitRoomId = "room-123"
        };

        Assert.AreEqual(channelId, call.ChannelId);
        Assert.AreEqual(userId, call.InitiatorUserId);
        Assert.AreEqual(VideoCallState.Active, call.State);
        Assert.AreEqual(CallMediaType.Audio, call.MediaType);
        Assert.AreEqual(now, call.StartedAtUtc);
        Assert.AreEqual(3, call.MaxParticipants);
        Assert.IsTrue(call.IsGroupCall);
        Assert.AreEqual("room-123", call.LiveKitRoomId);
    }

    [TestMethod]
    public void WhenSoftDeletedThenIsDeletedAndDeletedAtAreSet()
    {
        var call = new VideoCall();
        var deleteTime = DateTime.UtcNow;

        call.IsDeleted = true;
        call.DeletedAt = deleteTime;

        Assert.IsTrue(call.IsDeleted);
        Assert.AreEqual(deleteTime, call.DeletedAt);
    }

    [TestMethod]
    public void WhenEndedThenEndReasonAndTimestampAreSet()
    {
        var call = new VideoCall();
        var endTime = DateTime.UtcNow;

        call.State = VideoCallState.Ended;
        call.EndReason = VideoCallEndReason.Normal;
        call.EndedAtUtc = endTime;

        Assert.AreEqual(VideoCallState.Ended, call.State);
        Assert.AreEqual(VideoCallEndReason.Normal, call.EndReason);
        Assert.AreEqual(endTime, call.EndedAtUtc);
    }
}

/// <summary>
/// Tests for <see cref="CallParticipant"/> entity model defaults and properties.
/// </summary>
[TestClass]
public class CallParticipantModelTests
{
    [TestMethod]
    public void WhenCreatedThenIdIsNotEmpty()
    {
        var participant = new CallParticipant();
        Assert.AreNotEqual(Guid.Empty, participant.Id);
    }

    [TestMethod]
    public void WhenCreatedThenEachInstanceGetsUniqueId()
    {
        var p1 = new CallParticipant();
        var p2 = new CallParticipant();
        Assert.AreNotEqual(p1.Id, p2.Id);
    }

    [TestMethod]
    public void WhenCreatedThenDefaultRoleIsParticipant()
    {
        var participant = new CallParticipant();
        Assert.AreEqual(CallParticipantRole.Participant, participant.Role);
    }

    [TestMethod]
    public void WhenCreatedThenJoinedAtUtcIsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var participant = new CallParticipant();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(participant.JoinedAtUtc >= before && participant.JoinedAtUtc <= after);
    }

    [TestMethod]
    public void WhenCreatedThenLeftAtUtcIsNull()
    {
        var participant = new CallParticipant();
        Assert.IsNull(participant.LeftAtUtc);
    }

    [TestMethod]
    public void WhenCreatedThenHasAudioIsTrue()
    {
        var participant = new CallParticipant();
        Assert.IsTrue(participant.HasAudio);
    }

    [TestMethod]
    public void WhenCreatedThenHasVideoIsFalse()
    {
        var participant = new CallParticipant();
        Assert.IsFalse(participant.HasVideo);
    }

    [TestMethod]
    public void WhenCreatedThenHasScreenShareIsFalse()
    {
        var participant = new CallParticipant();
        Assert.IsFalse(participant.HasScreenShare);
    }

    [TestMethod]
    public void WhenCreatedThenVideoCallIsNull()
    {
        var participant = new CallParticipant();
        Assert.IsNull(participant.VideoCall);
    }

    [TestMethod]
    public void WhenCreatedThenVideoCallIdIsEmpty()
    {
        var participant = new CallParticipant();
        Assert.AreEqual(Guid.Empty, participant.VideoCallId);
    }

    [TestMethod]
    public void WhenCreatedThenUserIdIsEmpty()
    {
        var participant = new CallParticipant();
        Assert.AreEqual(Guid.Empty, participant.UserId);
    }

    [TestMethod]
    public void WhenPropertiesSetThenValuesArePersisted()
    {
        var callId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var participant = new CallParticipant
        {
            VideoCallId = callId,
            UserId = userId,
            Role = CallParticipantRole.Host,
            JoinedAtUtc = now,
            HasAudio = true,
            HasVideo = true,
            HasScreenShare = true
        };

        Assert.AreEqual(callId, participant.VideoCallId);
        Assert.AreEqual(userId, participant.UserId);
        Assert.AreEqual(CallParticipantRole.Host, participant.Role);
        Assert.AreEqual(now, participant.JoinedAtUtc);
        Assert.IsTrue(participant.HasAudio);
        Assert.IsTrue(participant.HasVideo);
        Assert.IsTrue(participant.HasScreenShare);
    }

    [TestMethod]
    public void WhenLeftThenLeftAtUtcIsSet()
    {
        var participant = new CallParticipant();
        var leaveTime = DateTime.UtcNow;

        participant.LeftAtUtc = leaveTime;

        Assert.AreEqual(leaveTime, participant.LeftAtUtc);
    }
}

/// <summary>
/// Tests for video call EF Core entity configurations, relationships, indexes, and soft-delete behavior.
/// </summary>
[TestClass]
public class VideoCallDbTests
{
    private ChatDbContext _db = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private Channel CreateTestChannel()
    {
        var channel = new Channel
        {
            Name = "test-channel",
            Type = ChannelType.Public,
            CreatedByUserId = Guid.NewGuid()
        };
        _db.Channels.Add(channel);
        _db.SaveChanges();
        return channel;
    }

    private VideoCall CreateTestVideoCall(Guid channelId, Guid? initiatorId = null)
    {
        var userId = initiatorId ?? Guid.NewGuid();
        var call = new VideoCall
        {
            ChannelId = channelId,
            InitiatorUserId = userId,
            HostUserId = userId,
            State = VideoCallState.Ringing,
            MediaType = CallMediaType.Video
        };
        _db.VideoCalls.Add(call);
        _db.SaveChanges();
        return call;
    }

    [TestMethod]
    public async Task WhenVideoCallSavedThenCanBeRetrieved()
    {
        var channel = CreateTestChannel();
        var userId = Guid.NewGuid();

        var call = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = userId,
            State = VideoCallState.Ringing,
            MediaType = CallMediaType.Video
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(channel.Id, retrieved.ChannelId);
        Assert.AreEqual(userId, retrieved.InitiatorUserId);
        Assert.AreEqual(VideoCallState.Ringing, retrieved.State);
        Assert.AreEqual(CallMediaType.Video, retrieved.MediaType);
    }

    [TestMethod]
    public async Task WhenVideoCallSavedThenStateIsStoredAsString()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(VideoCallState.Ringing, retrieved.State);
    }

    [TestMethod]
    public async Task WhenVideoCallSavedThenMediaTypeIsStoredAsString()
    {
        var channel = CreateTestChannel();

        var call = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            MediaType = CallMediaType.Audio
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(CallMediaType.Audio, retrieved.MediaType);
    }

    [TestMethod]
    public async Task WhenVideoCallSoftDeletedThenFilteredFromQueries()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        call.IsDeleted = true;
        call.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var results = await _db.VideoCalls.ToListAsync();
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task WhenVideoCallSoftDeletedThenVisibleWithIgnoreQueryFilters()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        call.IsDeleted = true;
        call.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var results = await _db.VideoCalls.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].IsDeleted);
    }

    [TestMethod]
    public async Task WhenMultipleCallsThenOnlySoftDeletedAreFiltered()
    {
        var channel = CreateTestChannel();
        var call1 = CreateTestVideoCall(channel.Id);
        var call2 = CreateTestVideoCall(channel.Id);

        call1.IsDeleted = true;
        call1.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var results = await _db.VideoCalls.ToListAsync();
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(call2.Id, results[0].Id);
    }

    [TestMethod]
    public async Task WhenParticipantAddedThenCanBeRetrieved()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);
        var userId = Guid.NewGuid();

        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = userId,
            Role = CallParticipantRole.Host,
            HasAudio = true,
            HasVideo = true
        };
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        var retrieved = await _db.CallParticipants.FindAsync(participant.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(call.Id, retrieved.VideoCallId);
        Assert.AreEqual(userId, retrieved.UserId);
        Assert.AreEqual(CallParticipantRole.Host, retrieved.Role);
        Assert.IsTrue(retrieved.HasAudio);
        Assert.IsTrue(retrieved.HasVideo);
    }

    [TestMethod]
    public async Task WhenMultipleParticipantsAddedThenAllCanBeRetrieved()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var p1 = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Host,
            HasAudio = true,
            HasVideo = true
        };
        var p2 = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Participant,
            HasAudio = true,
            HasVideo = false
        };
        _db.CallParticipants.AddRange(p1, p2);
        await _db.SaveChangesAsync();

        var participants = await _db.CallParticipants
            .Where(p => p.VideoCallId == call.Id)
            .ToListAsync();
        Assert.AreEqual(2, participants.Count);
    }

    [TestMethod]
    public async Task WhenVideoCallLoadedWithParticipantsThenNavigationPopulated()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Host
        });
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Participant
        });
        await _db.SaveChangesAsync();

        var loaded = await _db.VideoCalls
            .Include(v => v.Participants)
            .FirstAsync(v => v.Id == call.Id);

        Assert.AreEqual(2, loaded.Participants.Count);
        Assert.IsTrue(loaded.Participants.Any(p => p.Role == CallParticipantRole.Host));
        Assert.IsTrue(loaded.Participants.Any(p => p.Role == CallParticipantRole.Participant));
    }

    [TestMethod]
    public async Task WhenParticipantLoadedThenVideoCallNavigationPopulated()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Host
        };
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        var loaded = await _db.CallParticipants
            .Include(p => p.VideoCall)
            .FirstAsync(p => p.Id == participant.Id);

        Assert.IsNotNull(loaded.VideoCall);
        Assert.AreEqual(call.Id, loaded.VideoCall.Id);
    }

    [TestMethod]
    public async Task WhenVideoCallDeletedThenParticipantsAreCascadeDeleted()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Host
        });
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Participant
        });
        await _db.SaveChangesAsync();

        // Hard delete the call (bypassing soft-delete for cascade test)
        _db.VideoCalls.Remove(call);
        await _db.SaveChangesAsync();

        var remainingParticipants = await _db.CallParticipants.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(0, remainingParticipants.Count);
    }

    [TestMethod]
    public async Task WhenChannelDeletedThenVideoCallsAreCascadeDeleted()
    {
        var channel = CreateTestChannel();
        CreateTestVideoCall(channel.Id);
        CreateTestVideoCall(channel.Id);

        _db.Channels.Remove(channel);
        await _db.SaveChangesAsync();

        var remainingCalls = await _db.VideoCalls.IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(0, remainingCalls.Count);
    }

    [TestMethod]
    public async Task WhenVideoCallQueriedByChannelAndStateThenResultsFiltered()
    {
        var channel = CreateTestChannel();
        var otherChannel = CreateTestChannel();

        var activeCall = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Active
        };
        var endedCall = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Ended,
            EndReason = VideoCallEndReason.Normal,
            EndedAtUtc = DateTime.UtcNow
        };
        var otherChannelCall = new VideoCall
        {
            ChannelId = otherChannel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Active
        };

        _db.VideoCalls.AddRange(activeCall, endedCall, otherChannelCall);
        await _db.SaveChangesAsync();

        var activeCalls = await _db.VideoCalls
            .Where(v => v.ChannelId == channel.Id && v.State == VideoCallState.Active)
            .ToListAsync();

        Assert.AreEqual(1, activeCalls.Count);
        Assert.AreEqual(activeCall.Id, activeCalls[0].Id);
    }

    [TestMethod]
    public async Task WhenVideoCallsOrderedByCreatedAtThenCorrectOrder()
    {
        var channel = CreateTestChannel();

        var call1 = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        var call2 = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        var call3 = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.VideoCalls.AddRange(call1, call2, call3);
        await _db.SaveChangesAsync();

        var history = await _db.VideoCalls
            .Where(v => v.ChannelId == channel.Id)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync();

        Assert.AreEqual(3, history.Count);
        Assert.AreEqual(call3.Id, history[0].Id);
        Assert.AreEqual(call2.Id, history[1].Id);
        Assert.AreEqual(call1.Id, history[2].Id);
    }

    [TestMethod]
    public async Task WhenParticipantQueriedByUserAndJoinedThenResultsFiltered()
    {
        var channel = CreateTestChannel();
        var call1 = CreateTestVideoCall(channel.Id);
        var call2 = CreateTestVideoCall(channel.Id);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _db.CallParticipants.AddRange(
            new CallParticipant { VideoCallId = call1.Id, UserId = userId, Role = CallParticipantRole.Host },
            new CallParticipant { VideoCallId = call2.Id, UserId = userId, Role = CallParticipantRole.Participant },
            new CallParticipant { VideoCallId = call1.Id, UserId = otherUserId, Role = CallParticipantRole.Participant }
        );
        await _db.SaveChangesAsync();

        var userParticipations = await _db.CallParticipants
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.JoinedAtUtc)
            .ToListAsync();

        Assert.AreEqual(2, userParticipations.Count);
    }

    [TestMethod]
    public async Task WhenVideoCallWithAllEndReasonsThenAllPersistedCorrectly()
    {
        var channel = CreateTestChannel();

        foreach (var reason in Enum.GetValues<VideoCallEndReason>())
        {
            var call = new VideoCall
            {
                ChannelId = channel.Id,
                InitiatorUserId = Guid.NewGuid(),
                State = VideoCallState.Ended,
                EndReason = reason,
                EndedAtUtc = DateTime.UtcNow
            };
            _db.VideoCalls.Add(call);
        }
        await _db.SaveChangesAsync();

        var calls = await _db.VideoCalls.ToListAsync();
        Assert.AreEqual(Enum.GetValues<VideoCallEndReason>().Length, calls.Count);

        foreach (var reason in Enum.GetValues<VideoCallEndReason>())
        {
            Assert.IsTrue(calls.Any(c => c.EndReason == reason), $"EndReason {reason} not found");
        }
    }

    [TestMethod]
    public async Task WhenVideoCallWithAllStatesThenAllPersistedCorrectly()
    {
        var channel = CreateTestChannel();

        foreach (var state in Enum.GetValues<VideoCallState>())
        {
            var call = new VideoCall
            {
                ChannelId = channel.Id,
                InitiatorUserId = Guid.NewGuid(),
                State = state
            };
            _db.VideoCalls.Add(call);
        }
        await _db.SaveChangesAsync();

        // Soft-delete filter hides Ended/Missed/Rejected/Failed only if IsDeleted=true
        // All states should be visible since none are soft-deleted
        var calls = await _db.VideoCalls.ToListAsync();
        Assert.AreEqual(Enum.GetValues<VideoCallState>().Length, calls.Count);
    }

    [TestMethod]
    public async Task WhenVideoCallWithAllMediaTypesThenAllPersistedCorrectly()
    {
        var channel = CreateTestChannel();

        foreach (var mediaType in Enum.GetValues<CallMediaType>())
        {
            var call = new VideoCall
            {
                ChannelId = channel.Id,
                InitiatorUserId = Guid.NewGuid(),
                MediaType = mediaType
            };
            _db.VideoCalls.Add(call);
        }
        await _db.SaveChangesAsync();

        var calls = await _db.VideoCalls.ToListAsync();
        Assert.AreEqual(Enum.GetValues<CallMediaType>().Length, calls.Count);

        foreach (var mediaType in Enum.GetValues<CallMediaType>())
        {
            Assert.IsTrue(calls.Any(c => c.MediaType == mediaType), $"MediaType {mediaType} not found");
        }
    }

    [TestMethod]
    public async Task WhenParticipantRolesThenAllPersistedCorrectly()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        foreach (var role in Enum.GetValues<CallParticipantRole>())
        {
            _db.CallParticipants.Add(new CallParticipant
            {
                VideoCallId = call.Id,
                UserId = Guid.NewGuid(),
                Role = role
            });
        }
        await _db.SaveChangesAsync();

        var participants = await _db.CallParticipants.ToListAsync();
        Assert.AreEqual(Enum.GetValues<CallParticipantRole>().Length, participants.Count);

        foreach (var role in Enum.GetValues<CallParticipantRole>())
        {
            Assert.IsTrue(participants.Any(p => p.Role == role), $"Role {role} not found");
        }
    }

    [TestMethod]
    public async Task WhenVideoCallWithLiveKitRoomIdThenPersistedCorrectly()
    {
        var channel = CreateTestChannel();

        var call = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Active,
            IsGroupCall = true,
            LiveKitRoomId = "livekit-room-abc-123"
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("livekit-room-abc-123", retrieved.LiveKitRoomId);
    }

    [TestMethod]
    public async Task WhenParticipantMediaFlagsToggledThenPersistedCorrectly()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Host,
            HasAudio = true,
            HasVideo = true,
            HasScreenShare = false
        };
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        // Toggle media flags
        participant.HasAudio = false;
        participant.HasVideo = false;
        participant.HasScreenShare = true;
        await _db.SaveChangesAsync();

        var retrieved = await _db.CallParticipants.FindAsync(participant.Id);
        Assert.IsNotNull(retrieved);
        Assert.IsFalse(retrieved.HasAudio);
        Assert.IsFalse(retrieved.HasVideo);
        Assert.IsTrue(retrieved.HasScreenShare);
    }

    [TestMethod]
    public async Task WhenParticipantLeavesCallThenLeftAtUtcIsSet()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var participant = new CallParticipant
        {
            VideoCallId = call.Id,
            UserId = Guid.NewGuid(),
            Role = CallParticipantRole.Participant
        };
        _db.CallParticipants.Add(participant);
        await _db.SaveChangesAsync();

        Assert.IsNull(participant.LeftAtUtc);

        var leaveTime = DateTime.UtcNow;
        participant.LeftAtUtc = leaveTime;
        await _db.SaveChangesAsync();

        var retrieved = await _db.CallParticipants.FindAsync(participant.Id);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(leaveTime, retrieved.LeftAtUtc);
    }

    [TestMethod]
    public async Task WhenVideoCallStateTransitionsThenAllPersisted()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        // Ringing → Connecting
        call.State = VideoCallState.Connecting;
        await _db.SaveChangesAsync();
        Assert.AreEqual(VideoCallState.Connecting, (await _db.VideoCalls.FindAsync(call.Id))!.State);

        // Connecting → Active
        call.State = VideoCallState.Active;
        call.StartedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        Assert.AreEqual(VideoCallState.Active, (await _db.VideoCalls.FindAsync(call.Id))!.State);

        // Active → Ended
        call.State = VideoCallState.Ended;
        call.EndReason = VideoCallEndReason.Normal;
        call.EndedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var ended = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(ended);
        Assert.AreEqual(VideoCallState.Ended, ended.State);
        Assert.AreEqual(VideoCallEndReason.Normal, ended.EndReason);
        Assert.IsNotNull(ended.EndedAtUtc);
    }

    [TestMethod]
    public async Task WhenDbSetVideoCallsThenExposedOnContext()
    {
        Assert.IsNotNull(_db.VideoCalls);
    }

    [TestMethod]
    public async Task WhenDbSetCallParticipantsThenExposedOnContext()
    {
        Assert.IsNotNull(_db.CallParticipants);
    }

    [TestMethod]
    public async Task WhenVideoCallQueryByInitiatorThenFiltered()
    {
        var channel = CreateTestChannel();
        var userId = Guid.NewGuid();

        var myCall = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = userId,
            State = VideoCallState.Ringing
        };
        var otherCall = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Ringing
        };
        _db.VideoCalls.AddRange(myCall, otherCall);
        await _db.SaveChangesAsync();

        var myCalls = await _db.VideoCalls
            .Where(v => v.InitiatorUserId == userId)
            .ToListAsync();

        Assert.AreEqual(1, myCalls.Count);
        Assert.AreEqual(myCall.Id, myCalls[0].Id);
    }

    [TestMethod]
    public async Task WhenVideoCallWithNullEndReasonThenPersistedCorrectly()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.IsNull(retrieved.EndReason);
    }

    [TestMethod]
    public async Task WhenVideoCallChannelNavigationIncludedThenPopulated()
    {
        var channel = CreateTestChannel();
        var call = CreateTestVideoCall(channel.Id);

        var loaded = await _db.VideoCalls
            .Include(v => v.Channel)
            .FirstAsync(v => v.Id == call.Id);

        Assert.IsNotNull(loaded.Channel);
        Assert.AreEqual(channel.Id, loaded.Channel.Id);
        Assert.AreEqual("test-channel", loaded.Channel.Name);
    }

    [TestMethod]
    public async Task WhenGroupCallWithMaxParticipantsThenPersistedCorrectly()
    {
        var channel = CreateTestChannel();

        var call = new VideoCall
        {
            ChannelId = channel.Id,
            InitiatorUserId = Guid.NewGuid(),
            State = VideoCallState.Active,
            IsGroupCall = true,
            MaxParticipants = 5,
            StartedAtUtc = DateTime.UtcNow
        };
        _db.VideoCalls.Add(call);
        await _db.SaveChangesAsync();

        var retrieved = await _db.VideoCalls.FindAsync(call.Id);
        Assert.IsNotNull(retrieved);
        Assert.IsTrue(retrieved.IsGroupCall);
        Assert.AreEqual(5, retrieved.MaxParticipants);
    }
}
