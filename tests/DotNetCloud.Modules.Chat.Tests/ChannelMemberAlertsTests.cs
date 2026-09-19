using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for the aggregate alert poll behind <c>GET /api/v1/chat/alerts</c>
/// (<see cref="ChannelMemberService.GetAlertsAsync"/>), which the Android background poll uses.
/// </summary>
[TestClass]
public class ChannelMemberAlertsTests
{
    private static readonly DateTime BaseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private ChatDbContext _db = null!;
    private ChannelMemberService _service = null!;
    private CallerContext _caller = null!;
    private CallerContext _otherUser = null!;
    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ChatDbContext(options);

        _service = new ChannelMemberService(
            _db,
            new Mock<IEventBus>().Object,
            NullLogger<ChannelMemberService>.Instance,
            new Mock<IChatRealtimeService>().Object);

        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);
        _otherUser = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);

        var channel = new Channel { Name = "general", CreatedByUserId = _otherUser.UserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Member
        });

        await _db.SaveChangesAsync();
        _channelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<Guid> AddChannelAsync(string name)
    {
        var channel = new Channel { Name = name, CreatedByUserId = _otherUser.UserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Member
        });
        await _db.SaveChangesAsync();
        return channel.Id;
    }

    private async Task<Guid> AddMessageAsync(Guid channelId, DateTime sentAt, Guid? senderUserId = null)
    {
        var message = new Message
        {
            ChannelId = channelId,
            SenderUserId = senderUserId ?? _otherUser.UserId,
            Content = "hello",
            SentAt = sentAt
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        return message.Id;
    }

    private async Task SetMutedAsync(Guid channelId, bool muted)
    {
        var membership = await _db.ChannelMembers
            .FirstAsync(m => m.ChannelId == channelId && m.UserId == _caller.UserId);
        membership.IsMuted = muted;
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenNoMemberships_ThenReturnsZeroAggregateWithStableToken()
    {
        var stranger = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);

        var first = await _service.GetAlertsAsync(stranger);
        var second = await _service.GetAlertsAsync(stranger, first.ETag);

        Assert.IsFalse(first.NotModified);
        Assert.IsNotNull(first.Alerts);
        Assert.AreEqual(0, first.Alerts!.Unread);
        Assert.AreEqual(0, first.Alerts.Mentions);
        Assert.AreEqual(0, first.Alerts.UnmutedUnread);
        Assert.IsNull(first.Alerts.TopChannelId);
        Assert.IsNull(first.Alerts.ChangedAt);
        Assert.AreEqual(first.ETag, second.ETag);
        Assert.IsTrue(second.NotModified);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenUnreadMessagesExist_ThenReturnsAggregateAndTopChannel()
    {
        await AddMessageAsync(_channelId, BaseTime);
        await AddMessageAsync(_channelId, BaseTime.AddMinutes(1));

        var result = await _service.GetAlertsAsync(_caller);

        Assert.IsNotNull(result.Alerts);
        Assert.AreEqual(ChatAlertsDto.CurrentVersion, result.Alerts!.V);
        Assert.AreEqual(2, result.Alerts.Unread);
        Assert.AreEqual(2, result.Alerts.UnmutedUnread);
        Assert.AreEqual(0, result.Alerts.Mentions);
        Assert.AreEqual(_channelId, result.Alerts.TopChannelId);
        Assert.AreEqual(BaseTime.AddMinutes(1), result.Alerts.ChangedAt);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenChannelMuted_ThenCountedInTotalButNotUnmuted()
    {
        await AddMessageAsync(_channelId, BaseTime);
        await SetMutedAsync(_channelId, muted: true);

        var result = await _service.GetAlertsAsync(_caller);

        Assert.IsNotNull(result.Alerts);
        Assert.AreEqual(1, result.Alerts!.Unread);
        Assert.AreEqual(0, result.Alerts.UnmutedUnread);
        Assert.IsNull(result.Alerts.TopChannelId, "a muted channel must never be a notification tap target");
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenTokenMatchesCurrentState_ThenReturnsNotModifiedWithoutAggregate()
    {
        await AddMessageAsync(_channelId, BaseTime);
        var first = await _service.GetAlertsAsync(_caller);

        var second = await _service.GetAlertsAsync(_caller, first.ETag);

        Assert.IsTrue(second.NotModified);
        Assert.IsNull(second.Alerts);
        Assert.AreEqual(first.ETag, second.ETag);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenNewMessageArrives_ThenTokenChangesAndAggregateGrows()
    {
        await AddMessageAsync(_channelId, BaseTime);
        var first = await _service.GetAlertsAsync(_caller);

        await AddMessageAsync(_channelId, BaseTime.AddMinutes(5));
        var second = await _service.GetAlertsAsync(_caller, first.ETag);

        Assert.IsFalse(second.NotModified);
        Assert.AreNotEqual(first.ETag, second.ETag);
        Assert.AreEqual(2, second.Alerts!.Unread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenChannelIsRead_ThenTokenChangesAndUnreadClears()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        var first = await _service.GetAlertsAsync(_caller);

        await _service.MarkAsReadAsync(_channelId, messageId, _caller);
        var second = await _service.GetAlertsAsync(_caller, first.ETag);

        Assert.IsFalse(second.NotModified, "a read-state change must invalidate the cached token");
        Assert.AreEqual(0, second.Alerts!.Unread);
        Assert.AreEqual(0, second.Alerts.UnmutedUnread);
        Assert.IsNull(second.Alerts.TopChannelId);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMuteStateChanges_ThenTokenChanges()
    {
        await AddMessageAsync(_channelId, BaseTime);
        var first = await _service.GetAlertsAsync(_caller);

        await SetMutedAsync(_channelId, muted: true);
        var second = await _service.GetAlertsAsync(_caller, first.ETag);

        Assert.IsFalse(second.NotModified);
        Assert.AreEqual(0, second.Alerts!.UnmutedUnread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenDirectlyMentioned_ThenMentionIsCounted()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        _db.MessageMentions.Add(new MessageMention
        {
            MessageId = messageId,
            MentionedUserId = _caller.UserId,
            Type = MentionType.User,
            StartIndex = 0,
            Length = 5
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(1, result.Alerts!.Mentions);
        Assert.AreEqual(1, result.Alerts.Unread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenChannelWideMention_ThenCountedForMember()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        _db.MessageMentions.Add(new MessageMention
        {
            MessageId = messageId,
            MentionedUserId = null,
            Type = MentionType.Channel,
            StartIndex = 0,
            Length = 8
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(1, result.Alerts!.Mentions);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMentionBelongsToAnotherUser_ThenNotCounted()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        _db.MessageMentions.Add(new MessageMention
        {
            MessageId = messageId,
            MentionedUserId = _otherUser.UserId,
            Type = MentionType.User,
            StartIndex = 0,
            Length = 5
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(0, result.Alerts!.Mentions);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMentionAlreadyRead_ThenNotCounted()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        _db.MessageMentions.Add(new MessageMention
        {
            MessageId = messageId,
            MentionedUserId = _caller.UserId,
            Type = MentionType.User,
            StartIndex = 0,
            Length = 5
        });
        await _db.SaveChangesAsync();

        await _service.MarkAsReadAsync(_channelId, messageId, _caller);
        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(0, result.Alerts!.Mentions);
        Assert.AreEqual(0, result.Alerts.Unread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenSeveralChannelsHaveUnread_ThenTopChannelIsMostRecent()
    {
        var secondChannelId = await AddChannelAsync("random");
        await AddMessageAsync(_channelId, BaseTime);
        await AddMessageAsync(secondChannelId, BaseTime.AddMinutes(10));

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(2, result.Alerts!.Unread);
        Assert.AreEqual(secondChannelId, result.Alerts.TopChannelId);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMostRecentUnreadIsMuted_ThenTopChannelFallsBackToUnmuted()
    {
        var secondChannelId = await AddChannelAsync("random");
        await AddMessageAsync(_channelId, BaseTime);
        await AddMessageAsync(secondChannelId, BaseTime.AddMinutes(10));
        await SetMutedAsync(secondChannelId, muted: true);

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(2, result.Alerts!.Unread);
        Assert.AreEqual(1, result.Alerts.UnmutedUnread);
        Assert.AreEqual(_channelId, result.Alerts.TopChannelId);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenStateIsUnchanged_ThenTokenIsStableAcrossCalls()
    {
        await AddMessageAsync(_channelId, BaseTime);

        var first = await _service.GetAlertsAsync(_caller);
        var second = await _service.GetAlertsAsync(_caller);
        var third = await _service.GetAlertsAsync(_caller, second.ETag);

        Assert.AreEqual(first.ETag, second.ETag);
        Assert.IsFalse(second.NotModified);
        Assert.IsTrue(third.NotModified);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenTokenIsUnexpected_ThenAggregateIsStillReturned()
    {
        await AddMessageAsync(_channelId, BaseTime);

        var result = await _service.GetAlertsAsync(_caller, "\"stale-token-from-another-server\"");

        Assert.IsFalse(result.NotModified);
        Assert.IsNotNull(result.Alerts);
        Assert.AreEqual(1, result.Alerts!.Unread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMentionIsInMutedChannel_ThenCountedButNotAsUnmuted()
    {
        var messageId = await AddMessageAsync(_channelId, BaseTime);
        _db.MessageMentions.Add(new MessageMention
        {
            MessageId = messageId,
            MentionedUserId = _caller.UserId,
            Type = MentionType.User,
            StartIndex = 0,
            Length = 5
        });
        await _db.SaveChangesAsync();
        await SetMutedAsync(_channelId, muted: true);

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(1, result.Alerts!.Mentions, "the raw count still includes muted channels");
        Assert.AreEqual(0, result.Alerts.UnmutedMentions, "a muted channel must never change the alert wording");
        Assert.AreEqual(0, result.Alerts.UnmutedUnread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenOnlyOwnMessagesExist_ThenTheyStillCountAsUnread()
    {
        await AddMessageAsync(_channelId, BaseTime, senderUserId: _caller.UserId);

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(1, result.Alerts!.Unread);
    }

    [TestMethod]
    public async Task GetAlertsAsync_WhenMessagesExistInChannelCallerLeft_ThenIgnored()
    {
        var otherChannel = new Channel { Name = "private-other", CreatedByUserId = _otherUser.UserId };
        _db.Channels.Add(otherChannel);
        await _db.SaveChangesAsync();
        await AddMessageAsync(otherChannel.Id, BaseTime);

        var result = await _service.GetAlertsAsync(_caller);

        Assert.AreEqual(0, result.Alerts!.Unread);
        Assert.IsNull(result.Alerts.TopChannelId);
    }
}
