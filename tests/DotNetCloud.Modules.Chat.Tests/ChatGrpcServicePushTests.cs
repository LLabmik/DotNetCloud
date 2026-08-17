using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Host.Protos;
using DotNetCloud.Modules.Chat.Host.Services;
using DotNetCloud.Modules.Chat.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatGrpcService.SendPushNotification"/>.
/// </summary>
[TestClass]
public class ChatGrpcServicePushTests
{
    private ChatDbContext _db = null!;
    private Mock<IPushNotificationService> _pushService = null!;
    private ChatGrpcService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.CreateVersion7().ToString())
            .Options;
        _db = new ChatDbContext(options);
        _pushService = new Mock<IPushNotificationService>();

        _service = new ChatGrpcService(
            _db,
            new Mock<IChannelService>().Object,
            new Mock<IChannelMemberService>().Object,
            new Mock<ICallSignalingService>().Object,
            new Mock<IVideoCallService>().Object,
            _pushService.Object,
            NullLogger<ChatGrpcService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    [TestMethod]
    public async Task SendPushNotification_InvalidUserId_ReturnsFailure()
    {
        var request = new SendPushNotificationRequest
        {
            UserId = "not-a-guid",
            Title = "Title",
            Body = "Body",
            Category = "FileShared"
        };

        var response = await _service.SendPushNotification(request, new MockServerCallContext());

        Assert.IsFalse(response.Success);
        Assert.IsFalse(string.IsNullOrEmpty(response.ErrorMessage));
        _pushService.Verify(
            p => p.SendAsync(It.IsAny<Guid>(), It.IsAny<PushNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendPushNotification_ValidRequest_ForwardsToPushService()
    {
        var userId = Guid.CreateVersion7();
        var request = new SendPushNotificationRequest
        {
            UserId = userId.ToString(),
            Title = "File shared with you",
            Body = "\"report.pdf\" has been shared with you.",
            Category = "FileShared"
        };
        request.Data["actionUrl"] = "/apps/files?node=abc";

        var response = await _service.SendPushNotification(request, new MockServerCallContext());

        Assert.IsTrue(response.Success);
        _pushService.Verify(p => p.SendAsync(
            userId,
            It.Is<PushNotification>(n =>
                n.Title == "File shared with you" &&
                n.Body == "\"report.pdf\" has been shared with you." &&
                n.Category == NotificationCategory.FileShared &&
                n.Data["actionUrl"] == "/apps/files?node=abc"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendPushNotification_UnknownCategory_FallsBackToSystem()
    {
        var userId = Guid.CreateVersion7();
        var request = new SendPushNotificationRequest
        {
            UserId = userId.ToString(),
            Title = "Title",
            Body = "Body",
            Category = "NotARealCategory"
        };

        var response = await _service.SendPushNotification(request, new MockServerCallContext());

        Assert.IsTrue(response.Success);
        _pushService.Verify(p => p.SendAsync(
            userId,
            It.Is<PushNotification>(n => n.Category == NotificationCategory.System),
            It.IsAny<CancellationToken>()), Times.Once);
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
