using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Services.ModuleApis;
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

        Assert.AreEqual("Group name cannot be empty.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_WhitespaceChannelId_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync("   "));

        Assert.AreEqual("Group name cannot be empty.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_InvalidGuidFormat_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync("not-a-guid"));

        Assert.AreEqual("Invalid group name format.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_NonMember_ThrowsHubException()
    {
        var hub = CreateHubWithMembershipCheck(isMember: false);
        var channelId = Guid.CreateVersion7().ToString();

        var ex = await Assert.ThrowsExactlyAsync<HubException>(
            () => hub.JoinGroupAsync(channelId));

        Assert.AreEqual("You are not a member of this channel.", ex.Message);
    }

    [TestMethod]
    public async Task JoinGroupAsync_Member_AddsToGroup()
    {
        var groups = new StubGroupManager();
        var hub = CreateHubWithMembershipCheck(isMember: true, groups: groups);
        var channelId = Guid.CreateVersion7();
        var groupName = $"chat-channel-{channelId}";

        await hub.JoinGroupAsync(groupName);

        Assert.IsTrue(groups.Operations.Any(o =>
            o.GroupName == groupName && o.Action == "Add"));
    }

    [TestMethod]
    public async Task JoinGroupAsync_IsChannelMember_AddsToGroup()
    {
        // When the gRPC IsChannelMemberAsync returns true, user is added to the group.
        var groups = new StubGroupManager();
        var chatApiClientMock = new Mock<IChatApiClient>();
        chatApiClientMock
            .Setup(s => s.IsChannelMemberAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hub = CreateHub(
            userId: Guid.CreateVersion7(),
            chatApiClientMock: chatApiClientMock,
            groups: groups);
        var channelId = Guid.CreateVersion7();
        var groupName = $"chat-channel-{channelId}";

        await hub.JoinGroupAsync(groupName);

        Assert.IsTrue(groups.Operations.Any(o =>
            o.GroupName == groupName && o.Action == "Add"));
    }

    // ──── Helpers ─────────────────────────────────────────────────────────────

    private static CoreHub CreateHubWithMembershipCheck(
        bool isMember,
        StubGroupManager? groups = null)
    {
        var userId = Guid.CreateVersion7();
        var chatApiClientMock = new Mock<IChatApiClient>();
        chatApiClientMock
            .Setup(s => s.IsChannelMemberAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isMember);

        return CreateHub(userId, chatApiClientMock, groups);
    }

    private static CoreHub CreateHub(
        Guid userId,
        Mock<IChatApiClient>? chatApiClientMock = null,
        StubGroupManager? groups = null)
    {
        var tracker = new UserConnectionTracker();
        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var hub = new CoreHub(
            tracker,
            presence,
            chatApiClientMock?.Object ?? Mock.Of<IChatApiClient>(),
            Mock.Of<IRealtimeBroadcaster>(),
            NullLogger<CoreHub>.Instance);
        hub.Context = new TestHubCallerContext(userId, "conn-security-test");
        hub.Groups = groups ?? new StubGroupManager();
        return hub;
    }
}
