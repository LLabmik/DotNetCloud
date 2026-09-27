using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests that the admin-configured chat limits are enforced when messages and attachments
/// are written.
/// </summary>
[TestClass]
public class MessageServiceChatLimitTests
{
    private ChatDbContext _db = null!;
    private SqliteConnection _connection = null!;
    private Guid _channelId;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        // SQLite (not the in-memory provider): expiring the oldest attachments uses bulk
        // ExecuteDelete statements, which the in-memory provider cannot translate.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ChatDbContext(options);
        _db.Database.EnsureCreated();
        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);

        var channel = new Channel
        {
            Name = "general",
            Type = ChannelType.Public,
            CreatedByUserId = _caller.UserId
        };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Owner
        });
        _db.SaveChanges();

        _channelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Message length ──────────────────────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_WhenContentExceedsMaxLength_Throws()
    {
        var service = CreateService(new ChatSettings { MaxMessageLength = 20 });

        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SendMessageAsync(_channelId, new SendMessageDto { Content = new string('a', 21) }, _caller));

        StringAssert.Contains(ex.Message, "20");
        Assert.AreEqual(0, await _db.Messages.CountAsync());
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenContentIsExactlyMaxLength_Succeeds()
    {
        var service = CreateService(new ChatSettings { MaxMessageLength = 20 });

        var result = await service.SendMessageAsync(
            _channelId, new SendMessageDto { Content = new string('a', 20) }, _caller);

        Assert.AreEqual(20, result.Content.Length);
    }

    [TestMethod]
    public async Task SendMessageAsync_WithoutSettingsProvider_AppliesTheDefaultMaxLength()
    {
        var service = CreateService(settingsProvider: null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SendMessageAsync(
                _channelId,
                new SendMessageDto { Content = new string('a', ChatSettings.DefaultMaxMessageLength + 1) },
                _caller));

        var ok = await service.SendMessageAsync(
            _channelId,
            new SendMessageDto { Content = new string('a', ChatSettings.DefaultMaxMessageLength) },
            _caller);

        Assert.AreEqual(ChatSettings.DefaultMaxMessageLength, ok.Content.Length);
    }

    [TestMethod]
    public async Task EditMessageAsync_WhenContentExceedsMaxLength_Throws()
    {
        var service = CreateService(new ChatSettings { MaxMessageLength = 10 });
        var sent = await service.SendMessageAsync(
            _channelId, new SendMessageDto { Content = "short" }, _caller);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.EditMessageAsync(sent.Id, new EditMessageDto { Content = new string('b', 11) }, _caller));
    }

    // ── Per-message attachment limits ───────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_WhenAttachmentsExceedPerMessageLimit_Throws()
    {
        var service = CreateService(new ChatSettings { MaxAttachmentsPerMessage = 2 });

        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SendMessageAsync(_channelId, new SendMessageDto
            {
                Content = "three files",
                Attachments = [Attachment("a.png"), Attachment("b.png"), Attachment("c.png")]
            }, _caller));

        StringAssert.Contains(ex.Message, "2 attachment");
        Assert.AreEqual(0, await _db.Messages.CountAsync());
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenAttachmentsAreAtThePerMessageLimit_Succeeds()
    {
        var service = CreateService(new ChatSettings { MaxAttachmentsPerMessage = 2 });

        var result = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "two files",
            Attachments = [Attachment("a.png"), Attachment("b.png")]
        }, _caller);

        Assert.AreEqual(2, result.Attachments.Count);
    }

    [TestMethod]
    public async Task AddAttachmentAsync_WhenMessageIsAtTheAttachmentLimit_Throws()
    {
        var service = CreateService(new ChatSettings { MaxAttachmentsPerMessage = 1 });
        var sent = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "one file",
            Attachments = [Attachment("a.png")]
        }, _caller);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.AddAttachmentAsync(_channelId, sent.Id, Attachment("b.png"), _caller));
    }

    // ── Per-attachment size ─────────────────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_WhenAttachmentExceedsSizeLimit_Throws()
    {
        var service = CreateService(new ChatSettings { MaxAttachmentSizeMb = 1 });

        var ex = await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SendMessageAsync(_channelId, new SendMessageDto
            {
                Content = "big file",
                Attachments = [Attachment("big.png", fileSize: (1024L * 1024L) + 1)]
            }, _caller));

        StringAssert.Contains(ex.Message, "1 MB");
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenAttachmentIsAtTheSizeLimit_Succeeds()
    {
        var service = CreateService(new ChatSettings { MaxAttachmentSizeMb = 1 });

        var result = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "exact file",
            Attachments = [Attachment("exact.png", fileSize: 1024L * 1024L)]
        }, _caller);

        Assert.AreEqual(1, result.Attachments.Count);
    }

    // ── Per-channel attachment budget ───────────────────────────────

    [TestMethod]
    public async Task SendMessageAsync_WhenChannelAttachmentCountLimitWouldBeExceeded_ExpiresTheOldestAttachments()
    {
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            RetentionMode = ChatRetentionMode.Purge,
            MaxAttachmentsPerChannel = 2,
            MaxAttachmentsPerMessage = 10
        });

        await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "first",
            Attachments = [Attachment("a.png"), Attachment("b.png")]
        }, _caller);

        // A channel attachment ceiling is never a rejection: the oldest message with attachments
        // expires so the write can proceed.
        var result = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "second",
            Attachments = [Attachment("c.png")]
        }, _caller);

        Assert.AreEqual("second", result.Content);
        Assert.AreEqual(1, await _db.MessageAttachments.CountAsync());
        Assert.IsFalse(await _db.Messages.AnyAsync(m => m.Content == "first"),
            "The oldest message must have expired to make room.");
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenRetentionIsDisabled_KeepsAttachmentsAndAllowsTheWrite()
    {
        var service = CreateService(new ChatSettings
        {
            MaxAttachmentsPerChannel = 2,
            MaxAttachmentsPerMessage = 10
        });

        await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "first",
            Attachments = [Attachment("a.png"), Attachment("b.png")]
        }, _caller);

        await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "second",
            Attachments = [Attachment("c.png")]
        }, _caller);

        // With the retention master switch off nothing is expired, and nothing is rejected either.
        Assert.AreEqual(3, await _db.MessageAttachments.CountAsync());
        Assert.AreEqual(2, await _db.Messages.CountAsync());
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenChannelStorageLimitWouldBeExceeded_ExpiresTheOldestAttachments()
    {
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            RetentionMode = ChatRetentionMode.Purge,
            MaxAttachmentStoragePerChannelMb = 2,
            MaxAttachmentSizeMb = 2,
            MaxAttachmentsPerMessage = 10
        });

        await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "first",
            Attachments = [Attachment("a.png", fileSize: 1_500_000)]
        }, _caller);

        var result = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "second",
            Attachments = [Attachment("b.png", fileSize: 1_000_000)]
        }, _caller);

        Assert.AreEqual("second", result.Content);
        Assert.IsFalse(await _db.Messages.AnyAsync(m => m.Content == "first"),
            "The oldest message must have expired to make room for the new attachment.");
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenChannelStorageLimitIsNotReached_Succeeds()
    {
        var service = CreateService(new ChatSettings
        {
            MaxAttachmentStoragePerChannelMb = 2,
            MaxAttachmentSizeMb = 2,
            MaxAttachmentsPerMessage = 10
        });

        await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "first",
            Attachments = [Attachment("a.png", fileSize: 1_000_000)]
        }, _caller);

        var second = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = "second",
            Attachments = [Attachment("b.png", fileSize: 900_000)]
        }, _caller);

        Assert.AreEqual(1, second.Attachments.Count);
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenLimitsAreUnlimited_AllowsLargeContentAndAttachments()
    {
        var service = CreateService(new ChatSettings
        {
            MaxMessageLength = ChatSettings.HardMaxMessageLength,
            MaxMessagesPerChannel = 0,
            MaxAttachmentsPerChannel = 0,
            MaxAttachmentStoragePerChannelMb = 0,
            MaxAttachmentsPerMessage = 100,
            MaxAttachmentSizeMb = ChatSettings.HardMaxAttachmentSizeMb
        });

        var result = await service.SendMessageAsync(_channelId, new SendMessageDto
        {
            Content = new string('x', 5000),
            Attachments = [Attachment("huge.bin", fileSize: 60_000_000)]
        }, _caller);

        Assert.AreEqual(1, result.Attachments.Count);
    }

    [TestMethod]
    public async Task SendMessageAsync_WhenLimitsExceeded_DoesNotConsultSettingsMoreThanOnce()
    {
        var provider = new FixedChatSettingsProvider(new ChatSettings { MaxMessageLength = 10 });
        var service = CreateService(provider);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            service.SendMessageAsync(_channelId, new SendMessageDto { Content = new string('a', 11) }, _caller));

        Assert.AreEqual(1, provider.ReadCount);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private MessageService CreateService(ChatSettings settings)
        => CreateService(new FixedChatSettingsProvider(settings));

    private MessageService CreateService(IChatSettingsProvider? settingsProvider) => new(
        _db,
        new RecordingEventBus(),
        new RecordingAuditLogger(),
        NullLogger<MessageService>.Instance,
        userDirectory: null,
        mentionNotifier: null,
        userBlockService: null,
        linkPreviewService: null,
        settingsProvider: settingsProvider,
        expiryService: new ChatMessageExpiryService(
            new ChatArchiveExporter(
                new ConfigurationBuilder().Build(),
                NullLogger<ChatArchiveExporter>.Instance),
            NullLogger<ChatMessageExpiryService>.Instance));

    private static CreateAttachmentDto Attachment(string fileName, long fileSize = 1024) => new()
    {
        FileName = fileName,
        MimeType = "image/png",
        FileSize = fileSize
    };
}
