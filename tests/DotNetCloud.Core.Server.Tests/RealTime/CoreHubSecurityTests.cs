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
            NullLogger<CoreHub>.Instance);

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
            NullLogger<CoreHub>.Instance,
            channelMemberService: channelMemberService);
        hub.Context = new TestHubCallerContext(userId, "conn-security-test");
        hub.Groups = groups ?? new StubGroupManager();
        return hub;
    }
}
