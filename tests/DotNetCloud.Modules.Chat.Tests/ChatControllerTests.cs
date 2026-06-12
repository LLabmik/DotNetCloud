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
using System.Text.Json;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for deterministic exception mapping in <see cref="ChatController"/>.
/// </summary>
[TestClass]
public class ChatControllerTests
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
    private ChatController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
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
        var iceServerService = new Mock<IIceServerService>();
        var videoCallService = new Mock<IVideoCallService>();
        var userBlockService = new Mock<IUserBlockService>();

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
            iceServerService.Object,
            videoCallService.Object,
            userBlockService.Object,
            new Mock<IChatImageStore>().Object,
            NullLogger<ChatController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString())],
                        authenticationType: "TestAuth"))
                }
            }
        };
    }

    [TestMethod]
    public async Task AddReactionAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _reactionService
            .Setup(s => s.AddReactionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.AddReactionAsync(Guid.CreateVersion7(), new AddReactionDto { Emoji = "👍" });

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task PinMessageAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _pinService
            .Setup(s => s.PinMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.PinMessageAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _memberService
            .Setup(s => s.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.RemoveMemberAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task NotifyTypingAsync_WhenInvalidArgument_ThenReturnsBadRequest()
    {
        _typingService
            .Setup(s => s.NotifyTypingAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Channel id is required."));

        var result = await _controller.NotifyTypingAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task GetPinnedMessagesAsync_WhenInvalidOperation_ThenReturnsNotFound()
    {
        _pinService
            .Setup(s => s.GetPinnedMessagesAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Channel not found."));

        var result = await _controller.GetPinnedMessagesAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AddReactionAsync_WhenSuccessful_ThenReturnsEnvelopeWithAddedFlag()
    {
        _reactionService
            .Setup(s => s.AddReactionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.AddReactionAsync(Guid.CreateVersion7(), new AddReactionDto { Emoji = "👍" });

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("data").GetProperty("added").GetBoolean());
    }

    [TestMethod]
    public async Task RemoveReactionAsync_WhenMessageMissing_ThenReturnsNotFound()
    {
        _reactionService
            .Setup(s => s.RemoveReactionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Message not found."));

        var result = await _controller.RemoveReactionAsync(Guid.CreateVersion7(), "👍");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task MarkAsReadAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _memberService
            .Setup(s => s.MarkAsReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.MarkAsReadAsync(Guid.CreateVersion7(), new MarkReadDto { MessageId = Guid.CreateVersion7() });

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task GetUnreadCountsAsync_WhenSuccessful_ThenReturnsEnvelope()
    {
        _memberService
            .Setup(s => s.GetUnreadCountsAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UnreadCountDto { ChannelId = Guid.CreateVersion7(), UnreadCount = 3, MentionCount = 1 }
            ]);

        var result = await _controller.GetUnreadCountsAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.AreEqual(1, doc.RootElement.GetProperty("data").GetArrayLength());
    }

    [TestMethod]
    public async Task CreateAnnouncementAsync_WhenSuccessful_ThenBroadcastsAndReturnsCreated()
    {
        var announcement = new AnnouncementDto
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            AuthorUserId = Guid.CreateVersion7(),
            Title = "maintenance",
            Content = "Tonight",
            Priority = "Important"
        };

        _announcementService
            .Setup(s => s.CreateAsync(It.IsAny<CreateAnnouncementDto>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);
        _announcementService
            .Setup(s => s.ListAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([announcement]);

        var result = await _controller.CreateAnnouncementAsync(
            new CreateAnnouncementDto { Title = "maintenance", Content = "Tonight", OrganizationId = announcement.OrganizationId });

        Assert.IsInstanceOfType<CreatedAtActionResult>(result);
        _realtimeBroadcaster.Verify(
            b => b.BroadcastAsync("announcements", "AnnouncementCreated", announcement, It.IsAny<CancellationToken>()),
            Times.Once);
        _realtimeBroadcaster.Verify(
            b => b.BroadcastAsync("announcements", "AnnouncementBadgeUpdated", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateAnnouncementAsync_WhenUrgent_ThenBroadcastsUrgentAnnouncement()
    {
        var announcement = new AnnouncementDto
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            AuthorUserId = Guid.CreateVersion7(),
            Title = "urgent maintenance",
            Content = "Now",
            Priority = "Urgent"
        };

        _announcementService
            .Setup(s => s.CreateAsync(It.IsAny<CreateAnnouncementDto>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(announcement);
        _announcementService
            .Setup(s => s.ListAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([announcement]);

        await _controller.CreateAnnouncementAsync(
            new CreateAnnouncementDto { Title = "urgent maintenance", Content = "Now", OrganizationId = announcement.OrganizationId, Priority = "Urgent" });

        _realtimeBroadcaster.Verify(
            b => b.BroadcastAsync("announcements", "UrgentAnnouncement", announcement, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetAnnouncementAsync_WhenMissing_ThenReturnsNotFound()
    {
        _announcementService
            .Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnnouncementDto?)null);

        var result = await _controller.GetAnnouncementAsync(Guid.CreateVersion7());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AcknowledgeAnnouncementAsync_WhenCalled_ThenReturnsSuccessEnvelope()
    {
        _announcementService
            .Setup(s => s.AcknowledgeAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.AcknowledgeAnnouncementAsync(Guid.CreateVersion7());

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("data").GetProperty("acknowledged").GetBoolean());
    }

    [TestMethod]
    public async Task RegisterPushDeviceAsync_WhenValid_ThenRegistersDevice()
    {
        var request = new RegisterDeviceRequestDto
        {
            DeviceToken = "token-123",
            Provider = "FCM"
        };

        var result = await _controller.RegisterPushDeviceAsync(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _pushNotificationService.Verify(
            p => p.RegisterDeviceAsync(
                It.IsAny<Guid>(),
                It.Is<DeviceRegistration>(d => d.Token == "token-123" && d.Provider == PushProvider.FCM),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RegisterPushDeviceAsync_WhenProviderInvalid_ThenReturnsBadRequest()
    {
        var result = await _controller.RegisterPushDeviceAsync(
            new RegisterDeviceRequestDto { DeviceToken = "token-123", Provider = "UnknownProvider" });

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UnregisterPushDeviceAsync_WhenCalled_ThenUnregistersDevice()
    {
        var result = await _controller.UnregisterPushDeviceAsync("token-123");

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _pushNotificationService.Verify(
            p => p.UnregisterDeviceAsync(It.IsAny<Guid>(), "token-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void UpdateNotificationPreferencesAsync_WhenCalled_ThenReturnsOkAndStoresPreferences()
    {
        var mutedChannel = Guid.CreateVersion7();

        var result = _controller.UpdateNotificationPreferencesAsync(
            new DotNetCloud.Modules.Chat.Host.Controllers.NotificationPreferencesDto
            {
                PushEnabled = true,
                DoNotDisturb = true,
                MutedChannelIds = [mutedChannel, mutedChannel]
            });

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _notificationPreferenceStore.Verify(
            s => s.Update(
                It.IsAny<Guid>(),
                It.Is<UserNotificationPreferences>(p =>
                    p.PushEnabled
                    && p.DoNotDisturb
                    && p.MutedChannelIds.Count == 1
                    && p.MutedChannelIds.Contains(mutedChannel))),
            Times.Once);
    }

    [TestMethod]
    public void GetNotificationPreferencesAsync_WhenCalled_ThenReturnsStoreValues()
    {
        var mutedChannel = Guid.CreateVersion7();

        _notificationPreferenceStore
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .Returns(new UserNotificationPreferences
            {
                PushEnabled = false,
                DoNotDisturb = true,
                MutedChannelIds = new HashSet<Guid> { mutedChannel }
            });

        var result = _controller.GetNotificationPreferencesAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var data = doc.RootElement.GetProperty("data");
        Assert.IsFalse(data.GetProperty("PushEnabled").GetBoolean());
        Assert.IsTrue(data.GetProperty("DoNotDisturb").GetBoolean());
        Assert.AreEqual(1, data.GetProperty("MutedChannelIds").GetArrayLength());
    }
}
