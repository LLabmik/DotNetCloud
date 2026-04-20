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
using System.Text;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for image upload and serve endpoints in <see cref="ChatController"/>.
/// </summary>
[TestClass]
public class ChatImageUploadControllerTests
{
    private Mock<IChannelMemberService> _memberService = null!;
    private Mock<IChatImageStore> _chatImageStore = null!;
    private ChatController _controller = null!;
    private Guid _userId;
    private Guid _channelId;

    [TestInitialize]
    public void Setup()
    {
        _userId = Guid.NewGuid();
        _channelId = Guid.NewGuid();

        var channelService = new Mock<IChannelService>();
        _memberService = new Mock<IChannelMemberService>();
        var messageService = new Mock<IMessageService>();
        var reactionService = new Mock<IReactionService>();
        var pinService = new Mock<IPinService>();
        var typingService = new Mock<ITypingIndicatorService>();
        var announcementService = new Mock<IAnnouncementService>();
        var inviteService = new Mock<IChannelInviteService>();
        var realtimeBroadcaster = new Mock<IRealtimeBroadcaster>();
        var chatRealtimeService = new Mock<IChatRealtimeService>();
        var chatMessageNotifier = new Mock<IChatMessageNotifier>();
        var pushNotificationService = new Mock<IPushNotificationService>();
        var notificationPreferenceStore = new Mock<INotificationPreferenceStore>();
        var iceServerService = new Mock<IIceServerService>();
        var videoCallService = new Mock<IVideoCallService>();
        var userBlockService = new Mock<IUserBlockService>();
        _chatImageStore = new Mock<IChatImageStore>();

        notificationPreferenceStore
            .Setup(s => s.Get(It.IsAny<Guid>()))
            .Returns(new UserNotificationPreferences
            {
                PushEnabled = true,
                DoNotDisturb = false,
                MutedChannelIds = new HashSet<Guid>()
            });

        _controller = new ChatController(
            channelService.Object,
            _memberService.Object,
            messageService.Object,
            reactionService.Object,
            pinService.Object,
            typingService.Object,
            announcementService.Object,
            inviteService.Object,
            realtimeBroadcaster.Object,
            chatRealtimeService.Object,
            chatMessageNotifier.Object,
            pushNotificationService.Object,
            notificationPreferenceStore.Object,
            iceServerService.Object,
            videoCallService.Object,
            userBlockService.Object,
            _chatImageStore.Object,
            NullLogger<ChatController>.Instance);

        SetupHttpContext("image/png");
    }

    private void SetupHttpContext(string contentType, byte[]? body = null, string? fileNameHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())],
            authenticationType: "TestAuth"));
        httpContext.Request.ContentType = contentType;
        httpContext.Request.Body = new MemoryStream(body ?? new byte[] { 1, 2, 3, 4 });

        if (fileNameHeader is not null)
            httpContext.Request.Headers["X-File-Name"] = fileNameHeader;

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupMembershipAllowed()
    {
        _memberService.Setup(s => s.ListMembersAsync(_channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelMemberDto>
            {
                new() { UserId = _userId, DisplayName = "Test User", Role = "Owner", JoinedAt = DateTime.UtcNow, NotificationPref = "all" }
            });
    }

    private void SetupMembershipDenied()
    {
        _memberService.Setup(s => s.ListMembersAsync(_channelId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelMemberDto>
            {
                new() { UserId = Guid.NewGuid(), DisplayName = "Other User", Role = "Member", JoinedAt = DateTime.UtcNow, NotificationPref = "all" }
            });
    }

    // ── UploadChatImageAsync ───────────────────────────────────────

    [TestMethod]
    public async Task UploadImage_ValidRequest_ReturnsOkWithUrl()
    {
        SetupMembershipAllowed();
        _chatImageStore.Setup(s => s.SaveAsync(It.IsAny<string>(), "image/png", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageUploadResult
            {
                StoredFileName = "abc123.png",
                Url = "/api/v1/chat/uploads/abc123.png",
                ContentType = "image/png",
                FileSize = 4
            });

        var result = await _controller.UploadChatImageAsync(_channelId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadImage_NotMember_ReturnsForbid()
    {
        SetupMembershipDenied();

        var result = await _controller.UploadChatImageAsync(_channelId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task UploadImage_EmptyBody_ReturnsBadRequest()
    {
        SetupMembershipAllowed();
        SetupHttpContext("image/png", body: Array.Empty<byte>());

        var result = await _controller.UploadChatImageAsync(_channelId);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadImage_StoreThrowsArgumentException_ReturnsBadRequest()
    {
        SetupMembershipAllowed();
        _chatImageStore.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Unsupported image type: text/plain"));

        var result = await _controller.UploadChatImageAsync(_channelId);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadImage_UsesContentTypeFromRequest()
    {
        SetupMembershipAllowed();
        SetupHttpContext("image/jpeg", body: new byte[] { 0xFF, 0xD8 });
        _chatImageStore.Setup(s => s.SaveAsync(It.IsAny<string>(), "image/jpeg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageUploadResult
            {
                StoredFileName = "x.jpg",
                Url = "/api/v1/chat/uploads/x.jpg",
                ContentType = "image/jpeg",
                FileSize = 2
            });

        await _controller.UploadChatImageAsync(_channelId);

        _chatImageStore.Verify(s => s.SaveAsync(It.IsAny<string>(), "image/jpeg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadImage_UsesFileNameFromHeader()
    {
        SetupMembershipAllowed();
        SetupHttpContext("image/png", body: new byte[] { 1, 2, 3 }, fileNameHeader: "my-photo.png");
        _chatImageStore.Setup(s => s.SaveAsync("my-photo.png", It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageUploadResult
            {
                StoredFileName = "x.png",
                Url = "/api/v1/chat/uploads/x.png",
                ContentType = "image/png",
                FileSize = 3
            });

        await _controller.UploadChatImageAsync(_channelId);

        _chatImageStore.Verify(s => s.SaveAsync("my-photo.png", It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UploadImage_NoContentType_DefaultsToPng()
    {
        SetupMembershipAllowed();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())],
            authenticationType: "TestAuth"));
        httpContext.Request.Body = new MemoryStream(new byte[] { 1, 2, 3 });
        // No content type set
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _chatImageStore.Setup(s => s.SaveAsync(It.IsAny<string>(), "image/png", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageUploadResult
            {
                StoredFileName = "x.png",
                Url = "/api/v1/chat/uploads/x.png",
                ContentType = "image/png",
                FileSize = 3
            });

        await _controller.UploadChatImageAsync(_channelId);

        _chatImageStore.Verify(s => s.SaveAsync(It.IsAny<string>(), "image/png", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetChatUploadAsync ─────────────────────────────────────────

    [TestMethod]
    public async Task GetUpload_ExistingFile_ReturnsFileContent()
    {
        var imageData = new byte[] { 10, 20, 30 };
        _chatImageStore.Setup(s => s.GetAsync("abc.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageFile { Data = imageData, ContentType = "image/png" });

        var result = await _controller.GetChatUploadAsync("abc.png");

        Assert.IsInstanceOfType<FileContentResult>(result);
        var fileResult = (FileContentResult)result;
        CollectionAssert.AreEqual(imageData, fileResult.FileContents);
        Assert.AreEqual("image/png", fileResult.ContentType);
    }

    [TestMethod]
    public async Task GetUpload_NonExistentFile_ReturnsNotFound()
    {
        _chatImageStore.Setup(s => s.GetAsync("nonexistent.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatImageFile?)null);

        var result = await _controller.GetChatUploadAsync("nonexistent.png");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task GetUpload_JpegFile_ReturnsCorrectContentType()
    {
        _chatImageStore.Setup(s => s.GetAsync("photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatImageFile { Data = new byte[] { 0xFF, 0xD8 }, ContentType = "image/jpeg" });

        var result = await _controller.GetChatUploadAsync("photo.jpg");

        Assert.IsInstanceOfType<FileContentResult>(result);
        Assert.AreEqual("image/jpeg", ((FileContentResult)result).ContentType);
    }
}
