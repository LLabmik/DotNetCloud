using System.Security.Claims;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Security regression tests for CoreHub covering:
///   - SignalR group join authorization bypass (CVE-equivalent)
///   - Exception information disclosure via TryConvertToHubException
/// </summary>
[TestClass]
public class CoreHubSecurityTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Vulnerability 1: SignalR Group Join Authorization Bypass
    //
    // JoinGroupAsync must verify channel membership before adding a connection
    // to the SignalR group. Without this check, any authenticated user could
    // subscribe to arbitrary channel messages by guessing/knowing channel IDs.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task JoinGroupAsync_EmptyChannelId_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync(""));

        Assert.AreEqual("Channel ID cannot be empty.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_WhitespaceChannelId_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync("   "));

        Assert.AreEqual("Channel ID cannot be empty.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_InvalidGuidFormat_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync("not-a-guid"));

        Assert.AreEqual("Invalid channel ID format.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_NonMember_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);
        var channelId = Guid.NewGuid().ToString();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync(channelId));

        Assert.AreEqual("You are not a member of this channel.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_Member_AddsToGroup()
    {
        var groups = new StubGroupManager();
        var hub = CreateHubWithMembershipCheck(isMember: true, groups: groups);
        var channelId = Guid.NewGuid().ToString();

        await hub.JoinGroupAsync(channelId);

        Assert.IsTrue(groups.Operations.Any(o =>
            o.GroupName == channelId && o.Action == "Add"));
    }

    [TestMethod]
    public async Task JoinGroupAsync_NullChannelMemberService_SkipsCheckAndAddsToGroup()
    {
        // When chat module isn't loaded, channelMemberService is null.
        // The hub should still add the connection to the group (backwards compat).
        var groups = new StubGroupManager();
        var hub = CreateHub(
            userId: Guid.NewGuid(),
            channelMemberService: null,
            groups: groups);
        var channelId = Guid.NewGuid().ToString();

        await hub.JoinGroupAsync(channelId);

        Assert.IsTrue(groups.Operations.Any(o =>
            o.GroupName == channelId && o.Action == "Add"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Vulnerability 8: Exception Information Disclosure via TryConvertToHubException
    //
    // Internal exceptions must be converted to safe, generic HubException messages.
    // The original exception message (which may contain DB fields, stack traces, etc.)
    // must NEVER be forwarded to the client.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_UnauthorizedAccessException_ReturnsSafeMessage()
    {
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.SendMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<SendMessageDto>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("User xyz has no access to table dbo.Messages"));

        var hub = CreateHubWithAllChatServices(messageService: messageService);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMessageAsync(Guid.NewGuid(), "hello"));

        // Must NOT contain the internal message about tables/users
        Assert.AreEqual("Access denied.", ex.Message);
        Assert.IsFalse(ex.Message.Contains("dbo.Messages"));
        Assert.IsFalse(ex.Message.Contains("xyz"));
    }

    [TestMethod]
    public async Task SendMessageAsync_ArgumentException_ReturnsSafeMessage()
    {
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.SendMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<SendMessageDto>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Parameter 'channelId' references nonexistent row in core.channels"));

        var hub = CreateHubWithAllChatServices(messageService: messageService);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMessageAsync(Guid.NewGuid(), "hello"));

        Assert.AreEqual("Invalid request parameters.", ex.Message);
        Assert.IsFalse(ex.Message.Contains("core.channels"));
    }

    [TestMethod]
    public async Task SendMessageAsync_InvalidOperationException_ReturnsSafeGenericMessage()
    {
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.SendMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<SendMessageDto>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection string 'DefaultConnection' leaked in stack trace"));

        var hub = CreateHubWithAllChatServices(messageService: messageService);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.SendMessageAsync(Guid.NewGuid(), "hello"));

        // Must use the safe generic message, not the original exception text
        Assert.AreEqual("The requested operation could not be completed.", ex.Message);
        Assert.IsFalse(ex.Message.Contains("Connection string"));
    }

    [TestMethod]
    public async Task SendMessageAsync_UnhandledException_IsNotCaughtByTryConvert()
    {
        var messageService = new Mock<IMessageService>();
        messageService
            .Setup(s => s.SendMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<SendMessageDto>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database query timed out after 30s"));

        var hub = CreateHubWithAllChatServices(messageService: messageService);

        // TimeoutException is NOT handled by TryConvertToHubException, so it should propagate
        await Assert.ThrowsExactlyAsync<TimeoutException>(
            () => hub.SendMessageAsync(Guid.NewGuid(), "hello"));
    }

    // ──── Helpers ─────────────────────────────────────────────────────────────

    private static CoreHub CreateHubWithMembershipCheck(
        bool isMember,
        StubGroupManager? groups = null)
    {
        var userId = Guid.NewGuid();
        var channelMemberService = new Mock<IChannelMemberService>();
        channelMemberService
            .Setup(s => s.IsMemberAsync(
                It.IsAny<Guid>(),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isMember);

        return CreateHub(userId, channelMemberService.Object, groups);
    }

    private static CoreHub CreateHubWithAllChatServices(
        Mock<IMessageService>? messageService = null)
    {
        var userId = Guid.NewGuid();
        var ms = messageService?.Object ?? new Mock<IMessageService>().Object;
        var cms = new Mock<IChannelMemberService>().Object;
        var rs = new Mock<IReactionService>().Object;
        var tis = new Mock<ITypingIndicatorService>().Object;
        var crs = new Mock<IChatRealtimeService>().Object;

        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            ms, cms, rs, tis, crs,
            NullLogger<CoreHub>.Instance);

        hub.Context = new TestHubCallerContext(userId, "conn-security-test");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = new StubGroupManager();
        return hub;
    }

    private static CoreHub CreateHub(
        Guid userId,
        IChannelMemberService? channelMemberService,
        StubGroupManager? groups = null)
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            messageService: null,
            channelMemberService: channelMemberService,
            reactionService: null,
            typingIndicatorService: null,
            chatRealtimeService: null,
            NullLogger<CoreHub>.Instance);

        hub.Context = new TestHubCallerContext(userId, "conn-security-test");
        hub.Clients = new Mock<IHubCallerClients>().Object;
        hub.Groups = groups ?? new StubGroupManager();
        return hub;
    }
}
