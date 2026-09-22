using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatRetentionService"/>, exercising the archiving and purging sweep
/// against a real SQLite database so the bulk delete/update statements actually run.
/// </summary>
[TestClass]
public class ChatRetentionServiceTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<ChatDbContext> _options = null!;
    private RecordingEventBus _eventBus = null!;

    [TestInitialize]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDb();
        db.Database.EnsureCreated();

        _eventBus = new RecordingEventBus();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();
    }

    // ── Guard rails ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SweepAsync_WhenRetentionDisabled_DoesNothing()
    {
        var channelId = await SeedChannelAsync(messageCount: 10, messageAgeDays: 400);
        var service = CreateService(new ChatSettings { RetentionEnabled = false, MessageLifetimeDays = 30 });

        var result = await service.SweepAsync();

        Assert.IsTrue(result.Skipped);
        Assert.AreEqual(0, result.MessagesArchived);
        Assert.AreEqual(0, result.MessagesPurged);
        Assert.AreEqual(10, await CountLiveMessagesAsync(channelId));
    }

    [TestMethod]
    public async Task SweepAsync_WhenEnabledButNoPolicyConfigured_Skips()
    {
        await SeedChannelAsync(messageCount: 5, messageAgeDays: 1000);
        var service = CreateService(new ChatSettings { RetentionEnabled = true, MessageLifetimeDays = 0 });

        var result = await service.SweepAsync();

        Assert.IsTrue(result.Skipped);
    }

    // ── Lifetime-based expiry ───────────────────────────────────────

    [TestMethod]
    public async Task SweepAsync_LifetimeArchive_HidesOnlyExpiredMessages()
    {
        var channelId = await SeedChannelAsync(messageCount: 6, messageAgeDays: 160, spacingDays: 30);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 90,
            RetentionMode = ChatRetentionMode.Archive
        });

        var result = await service.SweepAsync();

        // Ages are 160, 130, 100, 70, 40 and 10 days: the first three predate the 90-day cutoff.
        Assert.AreEqual(3, result.MessagesArchived);
        Assert.AreEqual(0, result.MessagesPurged);
        Assert.AreEqual(3, await CountLiveMessagesAsync(channelId));
        Assert.AreEqual(3, await CountArchivedMessagesAsync(channelId));
    }

    [TestMethod]
    public async Task SweepAsync_LifetimePurge_DeletesOnlyExpiredMessages()
    {
        var channelId = await SeedChannelAsync(messageCount: 6, messageAgeDays: 160, spacingDays: 30);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 90,
            RetentionMode = ChatRetentionMode.Purge
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(0, result.MessagesArchived);
        Assert.AreEqual(3, result.MessagesPurged);
        Assert.AreEqual(3, await CountAllMessagesAsync(channelId));
    }

    [TestMethod]
    public async Task SweepAsync_LifetimeArchive_PublishesSearchIndexRemovals()
    {
        await SeedChannelAsync(messageCount: 4, messageAgeDays: 200, spacingDays: 10);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 30,
            RetentionMode = ChatRetentionMode.Archive
        });

        await service.SweepAsync();

        var removals = _eventBus.Published.OfType<SearchIndexRequestEvent>().ToList();
        Assert.AreEqual(4, removals.Count);
        Assert.IsTrue(removals.All(r => r.Action == SearchIndexAction.Remove));
        Assert.IsTrue(removals.All(r => r.ModuleId == "chat"));
    }

    // ── Count-based expiry ──────────────────────────────────────────

    [TestMethod]
    public async Task SweepAsync_MessageCountLimit_KeepsTheNewestMessages()
    {
        var channelId = await SeedChannelAsync(messageCount: 10, messageAgeDays: 0, spacingDays: 1, ageMinutes: 0);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MaxMessagesPerChannel = 4,
            RetentionMode = ChatRetentionMode.Archive
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(6, result.MessagesArchived);
        Assert.AreEqual(4, await CountLiveMessagesAsync(channelId));

        // The four survivors must be the newest ones.
        await using var db = CreateDb();
        var survivingBodies = await db.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderBy(m => m.SentAt)
            .Select(m => m.Content)
            .ToListAsync();

        CollectionAssert.AreEqual(
            new[] { "msg-6", "msg-7", "msg-8", "msg-9" },
            survivingBodies);
    }

    [TestMethod]
    public async Task SweepAsync_MessageCountLimit_LeavesChannelsUnderTheLimitUntouched()
    {
        var small = await SeedChannelAsync(messageCount: 3, messageAgeDays: 0, spacingDays: 1);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MaxMessagesPerChannel = 10,
            RetentionMode = ChatRetentionMode.Archive
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(0, result.ChannelsScanned);
        Assert.AreEqual(3, await CountLiveMessagesAsync(small));
    }

    // ── Attachments ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SweepAsync_ArchiveKeepingAttachments_PreservesAttachmentRows()
    {
        var channelId = await SeedChannelAsync(messageCount: 3, messageAgeDays: 100, spacingDays: 5, attachmentsPerMessage: 2);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 30,
            RetentionMode = ChatRetentionMode.Archive,
            ArchiveAttachments = true
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(3, result.MessagesArchived);
        Assert.AreEqual(0, result.AttachmentsRemoved);
        Assert.AreEqual(6, await CountAttachmentRowsAsync(channelId));
    }

    [TestMethod]
    public async Task SweepAsync_ArchiveDroppingAttachments_RemovesOnlyArchivedAttachmentRows()
    {
        var channelId = await SeedChannelAsync(messageCount: 4, messageAgeDays: 160, spacingDays: 30, attachmentsPerMessage: 1);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 90,
            RetentionMode = ChatRetentionMode.Archive,
            ArchiveAttachments = false
        });

        var result = await service.SweepAsync();

        // Three of the four messages (160/130/100 days old) expire; the 70-day-old one survives.
        Assert.AreEqual(3, result.MessagesArchived);
        Assert.AreEqual(3, result.AttachmentsRemoved);
        Assert.AreEqual(1, await CountAttachmentRowsAsync(channelId));
    }

    [TestMethod]
    public async Task SweepAsync_Purge_RemovesAttachmentRowsWithTheirMessages()
    {
        var channelId = await SeedChannelAsync(messageCount: 4, messageAgeDays: 160, spacingDays: 30, attachmentsPerMessage: 2);
        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 90,
            RetentionMode = ChatRetentionMode.Purge
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(3, result.MessagesPurged);
        Assert.AreEqual(6, result.AttachmentsRemoved);
        Assert.AreEqual(2, await CountAttachmentRowsAsync(channelId));
    }

    // ── Pins and replies ────────────────────────────────────────────

    [TestMethod]
    public async Task SweepAsync_ExpiredPinnedMessage_RemovesThePin()
    {
        var channelId = await SeedChannelAsync(messageCount: 1, messageAgeDays: 200, spacingDays: 1);
        await using (var db = CreateDb())
        {
            var message = await db.Messages.SingleAsync(m => m.ChannelId == channelId);
            db.PinnedMessages.Add(new PinnedMessage
            {
                ChannelId = channelId,
                MessageId = message.Id,
                PinnedByUserId = message.SenderUserId
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 30,
            RetentionMode = ChatRetentionMode.Archive
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(1, result.PinsRemoved);

        await using var verify = CreateDb();
        Assert.AreEqual(0, await verify.PinnedMessages.CountAsync());
    }

    [TestMethod]
    public async Task SweepAsync_PurgingAReplyTarget_DetachesTheReplyInsteadOfFailing()
    {
        var channelId = await SeedChannelAsync(messageCount: 0, messageAgeDays: 0);

        Guid parentId;
        await using (var db = CreateDb())
        {
            var parent = NewMessage(channelId, "parent", DateTime.UtcNow.AddDays(-200));
            db.Messages.Add(parent);
            await db.SaveChangesAsync();

            var reply = NewMessage(channelId, "reply", DateTime.UtcNow.AddDays(-1));
            reply.ReplyToMessageId = parent.Id;
            db.Messages.Add(reply);
            await db.SaveChangesAsync();

            parentId = parent.Id;
        }

        var service = CreateService(new ChatSettings
        {
            RetentionEnabled = true,
            MessageLifetimeDays = 30,
            RetentionMode = ChatRetentionMode.Purge
        });

        var result = await service.SweepAsync();

        Assert.AreEqual(1, result.MessagesPurged);

        await using var verify = CreateDb();
        var remaining = await verify.Messages.SingleAsync();
        Assert.AreEqual("reply", remaining.Content);
        Assert.IsNull(remaining.ReplyToMessageId, "A reply must survive its purged parent with the link detached.");
        Assert.AreEqual(0, await verify.Messages.CountAsync(m => m.Id == parentId));
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private ChatDbContext CreateDb() => new(_options);

    private ChatRetentionService CreateService(ChatSettings settings) => new(
        new TestChatDbContextFactory(_options),
        new FixedChatSettingsProvider(settings),
        NullLogger<ChatRetentionService>.Instance,
        _eventBus);

    private static Message NewMessage(Guid channelId, string content, DateTime sentAt) => new()
    {
        ChannelId = channelId,
        SenderUserId = SeedUserId,
        Content = content,
        SentAt = sentAt
    };

    private static readonly Guid SeedUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>
    /// Seeds one channel with <paramref name="messageCount"/> messages. Message <c>msg-0</c> is the
    /// oldest (<paramref name="messageAgeDays"/> days old) and each subsequent message is
    /// <paramref name="spacingDays"/> days younger, so <c>msg-(n-1)</c> is the newest.
    /// </summary>
    private async Task<Guid> SeedChannelAsync(
        int messageCount,
        int messageAgeDays,
        int spacingDays = 30,
        int attachmentsPerMessage = 0,
        int ageMinutes = 0)
    {
        await using var db = CreateDb();

        var channel = new Channel
        {
            Name = $"channel-{Guid.CreateVersion7():N}",
            Type = ChannelType.Public,
            CreatedByUserId = SeedUserId
        };
        db.Channels.Add(channel);

        for (var i = 0; i < messageCount; i++)
        {
            var age = TimeSpan.FromDays(messageAgeDays - (i * spacingDays)).Add(TimeSpan.FromMinutes(ageMinutes));
            var message = NewMessage(channel.Id, $"msg-{i}", DateTime.UtcNow - age);

            for (var a = 0; a < attachmentsPerMessage; a++)
            {
                message.Attachments.Add(new MessageAttachment
                {
                    MessageId = message.Id,
                    FileName = $"file-{i}-{a}.png",
                    MimeType = "image/png",
                    FileSize = 1024,
                    SortOrder = a
                });
            }

            db.Messages.Add(message);
        }

        await db.SaveChangesAsync();
        return channel.Id;
    }

    private async Task<int> CountLiveMessagesAsync(Guid channelId)
    {
        await using var db = CreateDb();
        return await db.Messages.CountAsync(m => m.ChannelId == channelId);
    }

    private async Task<int> CountArchivedMessagesAsync(Guid channelId)
    {
        await using var db = CreateDb();
        return await db.Messages.IgnoreQueryFilters()
            .CountAsync(m => m.ChannelId == channelId && m.ArchivedAt != null);
    }

    private async Task<int> CountAllMessagesAsync(Guid channelId)
    {
        await using var db = CreateDb();
        return await db.Messages.IgnoreQueryFilters().CountAsync(m => m.ChannelId == channelId);
    }

    private async Task<int> CountAttachmentRowsAsync(Guid channelId)
    {
        await using var db = CreateDb();

        // IgnoreQueryFilters so attachments belonging to archived messages are still counted.
        return await db.MessageAttachments
            .IgnoreQueryFilters()
            .CountAsync(a => a.Message!.ChannelId == channelId);
    }
}
