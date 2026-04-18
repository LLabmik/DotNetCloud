using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Phase G2 — Integration / E2E test scenarios that exercise the full
/// lifecycle of Direct Messaging, Host management, and Mid-Call invites
/// using real service implementations wired against an in-memory database.
/// </summary>
[TestClass]
public class DirectMessagingFullLifecycleTests
{
    private ChatDbContext _db = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private Mock<IChatRealtimeService> _realtimeMock = null!;
    private Mock<IChatMessageNotifier> _messageNotifierMock = null!;
    private Mock<ILiveKitService> _liveKitMock = null!;
    private Mock<IChannelMemberService> _channelMemberServiceMock = null!;
    private Mock<IChannelService> _channelServiceMock = null!;
    private VideoCallService _videoCallService = null!;
    private ChannelMemberService _channelMemberService = null!;

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
        _liveKitMock.Setup(x => x.IsAvailable).Returns(true);
        _liveKitMock.Setup(x => x.MaxP2PParticipants).Returns(10);
        _liveKitMock
            .Setup(x => x.CreateRoomAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-room");
        _channelMemberServiceMock = new Mock<IChannelMemberService>();
        _channelServiceMock = new Mock<IChannelService>();

        _videoCallService = new VideoCallService(
            _db,
            _eventBusMock.Object,
            NullLogger<VideoCallService>.Instance,
            _liveKitMock.Object,
            _realtimeMock.Object,
            _messageNotifierMock.Object,
            channelMemberService: _channelMemberServiceMock.Object,
            channelService: _channelServiceMock.Object);

        _channelMemberService = new ChannelMemberService(
            _db,
            _eventBusMock.Object,
            NullLogger<ChannelMemberService>.Instance,
            _realtimeMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private CallerContext CreateCaller() =>
        new(Guid.NewGuid(), ["user"], CallerType.User);

    private Guid SeedDmChannel(Guid owner, Guid member)
    {
        var channel = new Channel
        {
            Name = "DM",
            Type = ChannelType.DirectMessage,
            CreatedByUserId = owner
        };
        _db.Channels.Add(channel);
        _db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = owner, Role = ChannelMemberRole.Owner },
            new ChannelMember { ChannelId = channel.Id, UserId = member, Role = ChannelMemberRole.Member });
        _db.SaveChanges();
        return channel.Id;
    }

    // ══════════════════════════════════════════════════════════════
    //  G2 Scenario 1 — Mid-Call Invite Flow
    //  Caller invites user mid-call → target receives invite → joins → sees participants
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task MidCallInviteFlow_InviteReceived_ThenAccepted_ThenSeesParticipants()
    {
        var hostCaller = CreateCaller();
        var joinerCaller = CreateCaller();
        var invitedUserId = Guid.NewGuid();

        var channelId = SeedDmChannel(hostCaller.UserId, joinerCaller.UserId);

        // Step 1: Host initiates call
        var call = await _videoCallService.InitiateCallAsync(
            channelId,
            new StartCallRequest { MediaType = "Video" },
            hostCaller);

        Assert.AreEqual("Ringing", call.State);
        Assert.AreEqual(hostCaller.UserId, call.HostUserId);

        // Step 2: Joiner joins → call becomes Active
        call = await _videoCallService.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            joinerCaller);

        Assert.AreEqual("Active", call.State);
        Assert.AreEqual(2, call.Participants.Count);

        // Step 3: Host invites a 3rd user mid-call
        // The channel member service mock handles auto-add of non-member
        await _videoCallService.InviteToCallAsync(call.Id, invitedUserId, hostCaller);

        // Verify the invited participant record was created
        var invitedParticipant = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == invitedUserId);

        Assert.IsNotNull(invitedParticipant);
        Assert.AreEqual(ParticipantState.Invited, invitedParticipant.State);
        Assert.IsNotNull(invitedParticipant.InvitedAtUtc);

        // Verify SignalR call-invite was sent to the target user
        _realtimeMock.Verify(
            rt => rt.SendCallInviteAsync(
                invitedUserId,
                call.Id,
                channelId,
                hostCaller.UserId,
                It.IsAny<string?>(),
                "Video",
                true,
                It.Is<int>(c => c >= 2),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Step 4: Invited user joins the call
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channelId,
            UserId = invitedUserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        var invitedCaller = new CallerContext(invitedUserId, ["user"], CallerType.User);
        call = await _videoCallService.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            invitedCaller);

        // Step 5: Invited user now sees all participants
        var activeParticipants = call.Participants
            .Where(p => p.LeftAtUtc == null)
            .ToList();

        Assert.IsTrue(activeParticipants.Count >= 3);
        Assert.IsTrue(activeParticipants.Any(p => p.UserId == hostCaller.UserId));
        Assert.IsTrue(activeParticipants.Any(p => p.UserId == joinerCaller.UserId));
        Assert.IsTrue(activeParticipants.Any(p => p.UserId == invitedUserId));
    }

    // ══════════════════════════════════════════════════════════════
    //  G2 Scenario 2 — Host Transfer Flow
    //  Transfer → all participants notified → new Host can invite
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task HostTransferFlow_AllParticipantsNotified_NewHostCanInvite()
    {
        var hostCaller = CreateCaller();
        var joinerCaller = CreateCaller();
        var newInviteTarget = Guid.NewGuid();

        var channelId = SeedDmChannel(hostCaller.UserId, joinerCaller.UserId);

        // Step 1: Host initiates + joiner joins
        var call = await _videoCallService.InitiateCallAsync(
            channelId, new StartCallRequest { MediaType = "Audio" }, hostCaller);
        call = await _videoCallService.JoinCallAsync(
            call.Id, new JoinCallRequest { WithAudio = true }, joinerCaller);

        Assert.AreEqual("Active", call.State);

        // Step 2: Host transfers to joiner
        await _videoCallService.TransferHostAsync(call.Id, joinerCaller.UserId, hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(joinerCaller.UserId, dbCall!.HostUserId);

        // Verify broadcast sent to all participants
        _realtimeMock.Verify(
            rs => rs.BroadcastHostTransferredAsync(
                channelId, call.Id, hostCaller.UserId, joinerCaller.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify in-process notification fired
        _messageNotifierMock.Verify(
            mn => mn.NotifyCallHostTransferred(It.Is<CallHostTransferredNotification>(n =>
                n.CallId == call.Id &&
                n.PreviousHostUserId == hostCaller.UserId &&
                n.NewHostUserId == joinerCaller.UserId)),
            Times.Once);

        // Step 3: New host (joiner) can now invite another user
        await _videoCallService.InviteToCallAsync(call.Id, newInviteTarget, joinerCaller);

        var invited = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == newInviteTarget);

        Assert.IsNotNull(invited);
        Assert.AreEqual(ParticipantState.Invited, invited.State);

        // Step 4: Original host (now non-host) cannot invite
        var anotherTarget = Guid.NewGuid();

        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _videoCallService.InviteToCallAsync(call.Id, anotherTarget, hostCaller));
    }

    // ══════════════════════════════════════════════════════════════
    //  G2 Scenario 3 — Full Lifecycle
    //  Create DM → escalate to Group → start call → invite 4th user
    //  → transfer host → original host leaves → call continues
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task FullLifecycle_DmEscalateToGroup_StartCall_InviteUser_TransferHost_OriginalHostLeaves_CallContinues()
    {
        var ownerCaller = CreateCaller();
        var member2Caller = CreateCaller();
        var member3UserId = Guid.NewGuid();
        var member4UserId = Guid.NewGuid();

        // ── Step 1: Create a 2-person DM channel ─────────────────
        var dmChannelId = SeedDmChannel(ownerCaller.UserId, member2Caller.UserId);

        var channel = await _db.Channels.FindAsync(dmChannelId);
        Assert.AreEqual(ChannelType.DirectMessage, channel!.Type);

        // ── Step 2: Add 3rd member → DM escalates to Group ───────
        // Use the real ChannelMemberService for this step
        await _channelMemberService.AddMemberAsync(dmChannelId, member3UserId, ownerCaller);

        channel = await _db.Channels.FindAsync(dmChannelId);
        Assert.AreEqual(ChannelType.Group, channel!.Type, "Channel should have escalated to Group");

        var memberCount = await _db.ChannelMembers.CountAsync(m => m.ChannelId == dmChannelId);
        Assert.AreEqual(3, memberCount, "All 3 members should be present");

        // ── Step 3: Owner starts a call on the (now-group) channel ─
        var call = await _videoCallService.InitiateCallAsync(
            dmChannelId,
            new StartCallRequest { MediaType = "Video" },
            ownerCaller);

        Assert.AreEqual("Ringing", call.State);
        Assert.AreEqual(ownerCaller.UserId, call.HostUserId);

        // ── Step 4: Members 2 and 3 join ─────────────────────────
        call = await _videoCallService.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            member2Caller);

        var member3Caller = new CallerContext(member3UserId, ["user"], CallerType.User);
        call = await _videoCallService.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = false },
            member3Caller);

        Assert.AreEqual("Active", call.State);
        Assert.AreEqual(3, call.Participants.Count(p => p.LeftAtUtc == null));

        // ── Step 5: Owner (host) invites a 4th user mid-call ─────
        await _videoCallService.InviteToCallAsync(call.Id, member4UserId, ownerCaller);

        var invited = await _db.CallParticipants
            .FirstOrDefaultAsync(cp => cp.VideoCallId == call.Id && cp.UserId == member4UserId);

        Assert.IsNotNull(invited, "4th user should have an Invited participant record");
        Assert.AreEqual(ParticipantState.Invited, invited.State);

        // 4th user joins after receiving invite
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = dmChannelId,
            UserId = member4UserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        var member4Caller = new CallerContext(member4UserId, ["user"], CallerType.User);
        call = await _videoCallService.JoinCallAsync(
            call.Id,
            new JoinCallRequest { WithAudio = true, WithVideo = true },
            member4Caller);

        Assert.AreEqual(4, call.Participants.Count(p => p.LeftAtUtc == null),
            "All 4 participants should be active");

        // ── Step 6: Owner transfers host to member2 ──────────────
        await _videoCallService.TransferHostAsync(call.Id, member2Caller.UserId, ownerCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(member2Caller.UserId, dbCall!.HostUserId, "Host should be member2 after transfer");

        // Verify host-transferred broadcast fired
        _realtimeMock.Verify(
            rs => rs.BroadcastHostTransferredAsync(
                dmChannelId, call.Id, ownerCaller.UserId, member2Caller.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // ── Step 7: Original owner (now non-host) leaves the call ─
        await _videoCallService.LeaveCallAsync(call.Id, ownerCaller);

        dbCall = await _db.VideoCalls.FindAsync(call.Id);

        // Call should still be active — 3 participants remain
        Assert.AreEqual(VideoCallState.Active, dbCall!.State,
            "Call should remain active after original host leaves");

        // Host should still be member2 (no accidental auto-transfer)
        Assert.AreEqual(member2Caller.UserId, dbCall.HostUserId,
            "Host should remain member2 after non-host leaves");

        // Original owner's participant record should be marked Left
        var ownerParticipant = await _db.CallParticipants
            .FirstAsync(cp => cp.VideoCallId == call.Id && cp.UserId == ownerCaller.UserId && cp.LeftAtUtc != null);
        Assert.IsNotNull(ownerParticipant.LeftAtUtc);

        // ── Step 8: Remaining host (member2) ends the call ───────
        await _videoCallService.EndCallAsync(call.Id, member2Caller);

        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State, "Call should be ended by new host");
    }

    // ══════════════════════════════════════════════════════════════
    //  G2 Scenario 4 — DM → Group → Start Call → Original Host
    //  Auto-Transfers (leaves without explicit transfer)
    // ══════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task FullLifecycle_HostLeavesWithoutTransfer_AutoTransfer_CallContinues()
    {
        var hostCaller = CreateCaller();
        var joiner1Caller = CreateCaller();
        var joiner2Caller = CreateCaller();

        var channelId = SeedDmChannel(hostCaller.UserId, joiner1Caller.UserId);

        // Add joiner2 to the channel
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channelId,
            UserId = joiner2Caller.UserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();

        // Start call and have all join
        var call = await _videoCallService.InitiateCallAsync(
            channelId, new StartCallRequest { MediaType = "Video" }, hostCaller);

        await _videoCallService.JoinCallAsync(call.Id, new JoinCallRequest(), joiner1Caller);
        await _videoCallService.JoinCallAsync(call.Id, new JoinCallRequest(), joiner2Caller);

        // Host leaves without transferring
        await _videoCallService.LeaveCallAsync(call.Id, hostCaller);

        var dbCall = await _db.VideoCalls.FindAsync(call.Id);

        // Call must still be active
        Assert.AreEqual(VideoCallState.Active, dbCall!.State);

        // Auto-transfer must have occurred (joiner1 joined before joiner2)
        Assert.AreEqual(joiner1Caller.UserId, dbCall.HostUserId,
            "Auto-transfer should give host to earliest-joining participant");

        // joiner1 (new host) can now end the call
        await _videoCallService.EndCallAsync(call.Id, joiner1Caller);

        dbCall = await _db.VideoCalls.FindAsync(call.Id);
        Assert.AreEqual(VideoCallState.Ended, dbCall!.State);
    }
}
