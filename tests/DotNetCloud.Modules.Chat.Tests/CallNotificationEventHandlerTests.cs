using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Comprehensive tests for <see cref="CallNotificationEventHandler"/> — the event handler
/// responsible for dispatching push notifications for incoming, missed, and ended video calls.
/// </summary>
[TestClass]
public class CallNotificationEventHandlerTests
{
    private ChatDbContext _db = null!;
    private Mock<IPushNotificationService> _pushMock = null!;
    private Mock<IUserDirectory> _userDirectoryMock = null!;
    private CallNotificationEventHandler _handler = null!;
    private Guid _channelId;
    private Guid _initiatorUserId;
    private Guid _member1UserId;
    private Guid _member2UserId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);

        _pushMock = new Mock<IPushNotificationService>();
        _userDirectoryMock = new Mock<IUserDirectory>();
        _userDirectoryMock
            .Setup(ud => ud.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        // Set up IServiceScopeFactory to return our in-memory ChatDbContext
        var scopeFactory = CreateScopeFactory();

        _handler = new CallNotificationEventHandler(
            scopeFactory,
            _pushMock.Object,
            NullLogger<CallNotificationEventHandler>.Instance,
            _userDirectoryMock.Object);

        _initiatorUserId = Guid.NewGuid();
        _member1UserId = Guid.NewGuid();
        _member2UserId = Guid.NewGuid();
        _channelId = SeedChannel("test-channel", _initiatorUserId, _member1UserId, _member2UserId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private IServiceScopeFactory CreateScopeFactory()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(ChatDbContext))).Returns(_db);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private Guid SeedChannel(string name, params Guid[] memberIds)
    {
        var channel = new Channel
        {
            Name = name,
            Type = ChannelType.Group,
            CreatedByUserId = memberIds[0]
        };
        _db.Channels.Add(channel);
        foreach (var mid in memberIds)
        {
            _db.ChannelMembers.Add(new ChannelMember
            {
                ChannelId = channel.Id,
                UserId = mid,
                Role = mid == memberIds[0] ? ChannelMemberRole.Owner : ChannelMemberRole.Member
            });
        }
        _db.SaveChanges();
        return channel.Id;
    }

    private static VideoCallInitiatedEvent CreateInitiatedEvent(
        Guid channelId, Guid initiatorUserId, string mediaType = "Video", bool isGroupCall = true)
    {
        return new VideoCallInitiatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = Guid.NewGuid(),
            ChannelId = channelId,
            InitiatorUserId = initiatorUserId,
            MediaType = mediaType,
            IsGroupCall = isGroupCall
        };
    }

    private static VideoCallMissedEvent CreateMissedEvent(Guid channelId, Guid initiatorUserId)
    {
        return new VideoCallMissedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = Guid.NewGuid(),
            ChannelId = channelId,
            InitiatorUserId = initiatorUserId
        };
    }

    private static VideoCallEndedEvent CreateEndedEvent(
        Guid callId, Guid channelId, string endReason = "Normal", int? durationSeconds = null)
    {
        return new VideoCallEndedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CallId = callId,
            ChannelId = channelId,
            EndReason = endReason,
            DurationSeconds = durationSeconds
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  IncomingCall Notification Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleInitiated_VideoCall_SendsIncomingCallToAllMembersExceptInitiator()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: true);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(_member1UserId) &&
                    ids.Contains(_member2UserId) &&
                    !ids.Contains(_initiatorUserId)),
                It.Is<PushNotification>(n =>
                    n.Category == NotificationCategory.IncomingCall &&
                    n.Title == "Incoming video call"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_AudioCall_TitleContainsAudio()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Audio", isGroupCall: false);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Title == "Incoming audio call"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_GroupCall_BodyIncludesChannelName()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: true);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Body.Contains("#test-channel")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_DirectCall_BodySaysCallingYou()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: false);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Body.Contains("is calling you")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_WithUserDirectory_UsesDisplayName()
    {
        _userDirectoryMock
            .Setup(ud => ud.GetDisplayNamesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(_initiatorUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_initiatorUserId] = "Alice" });

        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: false);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Body.Contains("Alice")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_WithoutUserDirectory_UsesFallbackName()
    {
        var scopeFactory = CreateScopeFactory();
        var handler = new CallNotificationEventHandler(
            scopeFactory,
            _pushMock.Object,
            NullLogger<CallNotificationEventHandler>.Instance,
            userDirectory: null);

        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: false);

        await handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Body.Contains("Someone")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleInitiated_NoOtherMembers_DoesNotSendPush()
    {
        var soloChannel = SeedChannel("solo", _initiatorUserId);
        var evt = CreateInitiatedEvent(soloChannel, _initiatorUserId);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleInitiated_DataPayloadContainsCallMetadata()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId, "Video", isGroupCall: true);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual(evt.CallId.ToString(), captured.Data["callId"]);
        Assert.AreEqual(evt.ChannelId.ToString(), captured.Data["channelId"]);
        Assert.AreEqual(evt.InitiatorUserId.ToString(), captured.Data["initiatorUserId"]);
        Assert.AreEqual("Video", captured.Data["mediaType"]);
        Assert.AreEqual("True", captured.Data["isGroupCall"]);
        Assert.AreEqual("incoming_call", captured.Data["action"]);
    }

    [TestMethod]
    public async Task HandleInitiated_CategoryIsIncomingCall()
    {
        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotificationCategory.IncomingCall, captured.Category);
    }

    [TestMethod]
    public async Task HandleInitiated_RecipientCountMatchesNonInitiatorMembers()
    {
        IEnumerable<Guid>? capturedIds = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((ids, _, _) => capturedIds = ids);

        var evt = CreateInitiatedEvent(_channelId, _initiatorUserId);
        await _handler.HandleAsync(evt);

        Assert.IsNotNull(capturedIds);
        Assert.AreEqual(2, capturedIds.Count()); // _member1UserId and _member2UserId
    }

    // ══════════════════════════════════════════════════════════════
    //  MissedCall Notification Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleMissed_SendsMissedCallToAllMembersExceptInitiator()
    {
        var evt = CreateMissedEvent(_channelId, _initiatorUserId);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(_member1UserId) &&
                    ids.Contains(_member2UserId) &&
                    !ids.Contains(_initiatorUserId)),
                It.Is<PushNotification>(n =>
                    n.Category == NotificationCategory.MissedCall &&
                    n.Title == "Missed call"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleMissed_BodyContainsCallerName()
    {
        _userDirectoryMock
            .Setup(ud => ud.GetDisplayNamesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(_initiatorUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_initiatorUserId] = "Bob" });

        var evt = CreateMissedEvent(_channelId, _initiatorUserId);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.Is<PushNotification>(n => n.Body.Contains("Bob")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleMissed_DataPayloadContainsCallMetadata()
    {
        var evt = CreateMissedEvent(_channelId, _initiatorUserId);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual(evt.CallId.ToString(), captured.Data["callId"]);
        Assert.AreEqual(evt.ChannelId.ToString(), captured.Data["channelId"]);
        Assert.AreEqual(evt.InitiatorUserId.ToString(), captured.Data["initiatorUserId"]);
        Assert.AreEqual("missed_call", captured.Data["action"]);
    }

    [TestMethod]
    public async Task HandleMissed_NoOtherMembers_DoesNotSendPush()
    {
        var soloChannel = SeedChannel("solo", _initiatorUserId);
        var evt = CreateMissedEvent(soloChannel, _initiatorUserId);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleMissed_CategoryIsMissedCall()
    {
        var evt = CreateMissedEvent(_channelId, _initiatorUserId);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotificationCategory.MissedCall, captured.Category);
    }

    // ══════════════════════════════════════════════════════════════
    //  CallEnded Notification Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HandleEnded_WithDisconnectedParticipants_SendsCallEndedNotification()
    {
        var callId = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            Role = CallParticipantRole.Participant,
            LeftAtUtc = DateTime.UtcNow
        });
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member2UserId,
            Role = CallParticipantRole.Participant,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId, "Normal", durationSeconds: 120);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.Is<IEnumerable<Guid>>(ids =>
                    ids.Contains(_member1UserId) &&
                    ids.Contains(_member2UserId)),
                It.Is<PushNotification>(n =>
                    n.Category == NotificationCategory.CallEnded &&
                    n.Title == "Call ended"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleEnded_NoDisconnectedParticipants_DoesNotSendPush()
    {
        var callId = Guid.NewGuid();
        // Participant still in call (LeftAtUtc is null)
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            Role = CallParticipantRole.Participant,
            LeftAtUtc = null
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId);

        await _handler.HandleAsync(evt);

        _pushMock.Verify(
            p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleEnded_WithDuration_BodyContainsFormattedDuration()
    {
        var callId = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId, "Normal", durationSeconds: 90);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        StringAssert.Contains(captured.Body, "1m 30s");
    }

    [TestMethod]
    public async Task HandleEnded_WithoutDuration_BodyDoesNotContainDuration()
    {
        var callId = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId, "Normal", durationSeconds: null);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual("The call has ended", captured.Body);
    }

    [TestMethod]
    public async Task HandleEnded_DataPayloadContainsEndReason()
    {
        var callId = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId, "Failed", durationSeconds: 10);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual("Failed", captured.Data["endReason"]);
        Assert.AreEqual("call_ended", captured.Data["action"]);
        Assert.AreEqual(callId.ToString(), captured.Data["callId"]);
    }

    [TestMethod]
    public async Task HandleEnded_DuplicateParticipantUserIds_DeduplicatedInRecipientList()
    {
        var callId = Guid.NewGuid();
        // Same user left twice (rejoined and left again)
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        IEnumerable<Guid>? capturedIds = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((ids, _, _) => capturedIds = ids);

        var evt = CreateEndedEvent(callId, _channelId);
        await _handler.HandleAsync(evt);

        Assert.IsNotNull(capturedIds);
        Assert.AreEqual(1, capturedIds.Count()); // Deduplicated
    }

    [TestMethod]
    public async Task HandleEnded_CategoryIsCallEnded()
    {
        var callId = Guid.NewGuid();
        _db.CallParticipants.Add(new CallParticipant
        {
            VideoCallId = callId,
            UserId = _member1UserId,
            LeftAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = CreateEndedEvent(callId, _channelId);

        PushNotification? captured = null;
        _pushMock
            .Setup(p => p.SendToMultipleAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<PushNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Guid>, PushNotification, CancellationToken>((_, n, _) => captured = n);

        await _handler.HandleAsync(evt);

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotificationCategory.CallEnded, captured.Category);
    }

    // ══════════════════════════════════════════════════════════════
    //  FormatDuration Tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow(5, "5s")]
    [DataRow(0, "0s")]
    [DataRow(59, "59s")]
    [DataRow(60, "1m")]
    [DataRow(90, "1m 30s")]
    [DataRow(120, "2m")]
    [DataRow(3600, "1h")]
    [DataRow(3661, "1h 1m")]
    [DataRow(7200, "2h")]
    [DataRow(7320, "2h 2m")]
    public void FormatDuration_ReturnsExpectedString(int totalSeconds, string expected)
    {
        var result = CallNotificationEventHandler.FormatDuration(totalSeconds);
        Assert.AreEqual(expected, result);
    }

    // ══════════════════════════════════════════════════════════════
    //  NotificationRouter Integration — IncomingCall bypasses online suppression
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task NotificationRouter_IncomingCall_NotSuppressedWhenUserOnline()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var queue = new TestNotificationDeliveryQueue();
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.IsOnlineAsync(userId)).ReturnsAsync(true);

        var router = new NotificationRouter(
            [fcmProvider],
            new InMemoryNotificationPreferenceStore(),
            queue,
            NullLogger<NotificationRouter>.Instance,
            presence.Object);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token", Provider = PushProvider.FCM });

        await router.SendAsync(userId, new PushNotification
        {
            Title = "Incoming call",
            Body = "Someone is calling",
            Category = NotificationCategory.IncomingCall
        });

        // Should NOT be suppressed despite user being online
        Assert.AreEqual(1, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task NotificationRouter_MissedCall_SuppressedWhenUserOnline()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var queue = new TestNotificationDeliveryQueue();
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.IsOnlineAsync(userId)).ReturnsAsync(true);

        var router = new NotificationRouter(
            [fcmProvider],
            new InMemoryNotificationPreferenceStore(),
            queue,
            NullLogger<NotificationRouter>.Instance,
            presence.Object);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token", Provider = PushProvider.FCM });

        await router.SendAsync(userId, new PushNotification
        {
            Title = "Missed call",
            Body = "You missed a call",
            Category = NotificationCategory.MissedCall
        });

        // MissedCall IS suppressed (normal behavior)
        Assert.AreEqual(0, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task NotificationRouter_IncomingCall_StillRespectsDND()
    {
        var userId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var queue = new TestNotificationDeliveryQueue();
        var prefs = new InMemoryNotificationPreferenceStore();
        prefs.Update(userId, new UserNotificationPreferences { DoNotDisturb = true });

        var router = new NotificationRouter(
            [fcmProvider],
            prefs,
            queue,
            NullLogger<NotificationRouter>.Instance);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token", Provider = PushProvider.FCM });

        await router.SendAsync(userId, new PushNotification
        {
            Title = "Incoming call",
            Body = "Someone is calling",
            Category = NotificationCategory.IncomingCall
        });

        // DND should still suppress incoming calls
        Assert.AreEqual(0, fcmProvider.SendCount);
    }

    [TestMethod]
    public async Task NotificationRouter_IncomingCall_NotAffectedByChannelMute()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var fcmProvider = new TestPushProvider(PushProvider.FCM);
        var queue = new TestNotificationDeliveryQueue();
        var prefs = new InMemoryNotificationPreferenceStore();
        prefs.Update(userId, new UserNotificationPreferences
        {
            MutedChannelIds = new HashSet<Guid> { channelId }
        });

        var router = new NotificationRouter(
            [fcmProvider],
            prefs,
            queue,
            NullLogger<NotificationRouter>.Instance);

        await router.RegisterDeviceAsync(userId, new DeviceRegistration { Token = "token", Provider = PushProvider.FCM });

        await router.SendAsync(userId, new PushNotification
        {
            Title = "Incoming call",
            Body = "Someone is calling",
            Category = NotificationCategory.IncomingCall,
            Data = new Dictionary<string, string> { ["channelId"] = channelId.ToString() }
        });

        // Channel muting only applies to ChatMessage/ChatMention, not IncomingCall
        Assert.AreEqual(1, fcmProvider.SendCount);
    }

    // ══════════════════════════════════════════════════════════════
    //  ICallNotificationHandler interface tests
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public void CallNotificationEventHandler_ImplementsICallNotificationHandler()
    {
        Assert.IsInstanceOfType<ICallNotificationHandler>(_handler);
    }

    // ══════════════════════════════════════════════════════════════
    //  Test helpers
    // ══════════════════════════════════════════════════════════════

    private sealed class TestPushProvider : IPushProviderEndpoint
    {
        public TestPushProvider(PushProvider provider) => Provider = provider;
        public PushProvider Provider { get; }
        public int SendCount { get; private set; }

        public Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }

        public async Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification, CancellationToken cancellationToken = default)
        {
            foreach (var userId in userIds)
                await SendAsync(userId, notification, cancellationToken);
        }

        public Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestNotificationDeliveryQueue : INotificationDeliveryQueue
    {
        public List<QueuedPushNotification> Items { get; } = [];
        public int Count => Items.Count;

        public ValueTask EnqueueAsync(QueuedPushNotification notification, CancellationToken cancellationToken = default)
        {
            Items.Add(notification);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<QueuedPushNotification> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            foreach (var item in Items)
                yield return item;
        }
    }
}
