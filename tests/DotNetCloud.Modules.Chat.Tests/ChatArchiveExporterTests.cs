using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>Tests for <see cref="ChatArchivePathResolver"/>.</summary>
[TestClass]
public class ChatArchivePathResolverTests
{
    [TestMethod]
    public void Resolve_ConfiguredPath_ReturnsConfiguredPath()
        => Assert.AreEqual("/srv/archive", ChatArchivePathResolver.Resolve("  /srv/archive  ", "/var/lib/dotnetcloud"));

    [TestMethod]
    public void Resolve_NoConfiguredPath_UsesDataDirectoryStorageSubdirectory()
        => Assert.AreEqual(
            Path.Combine("/var/lib/dotnetcloud", "storage", "chat-archive"),
            ChatArchivePathResolver.Resolve(null, "/var/lib/dotnetcloud"));

    [TestMethod]
    public void Resolve_BlankConfiguredPath_UsesDataDirectoryStorageSubdirectory()
        => Assert.AreEqual(
            Path.Combine("/var/lib/dotnetcloud", "storage", "chat-archive"),
            ChatArchivePathResolver.Resolve("   ", "/var/lib/dotnetcloud"));

    [TestMethod]
    public void Resolve_NoConfiguredPathOrDataDirectory_FallsBackToCurrentDirectory()
        => Assert.AreEqual(
            Path.Combine(Directory.GetCurrentDirectory(), "storage", "chat-archive"),
            ChatArchivePathResolver.Resolve(null, null));
}

/// <summary>
/// Tests for <see cref="ChatArchiveExporter"/>: the on-disk layout that backs Archive mode.
/// </summary>
[TestClass]
public class ChatArchiveExporterTests
{
    private const string StoredUploadName = "abc123.png";

    private SqliteConnection _connection = null!;
    private DbContextOptions<ChatDbContext> _options = null!;
    private string _root = null!;
    private Guid _channelId;
    private ChatArchiveExporter _exporter = null!;

    [TestInitialize]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var db = new ChatDbContext(_options))
        {
            db.Database.EnsureCreated();

            var channel = new Channel
            {
                Name = "archive-tests",
                Type = ChannelType.Public,
                CreatedByUserId = Guid.CreateVersion7()
            };
            db.Channels.Add(channel);
            db.SaveChanges();
            _channelId = channel.Id;
        }

        _root = Path.Combine(Path.GetTempPath(), "chat-archive-exporter-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(_root);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = _root
            })
            .Build();

        _exporter = new ChatArchiveExporter(configuration, NullLogger<ChatArchiveExporter>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection.Dispose();

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    [DataRow("/api/v1/chat/uploads/abc123.png", "abc123.png")]
    [DataRow("https://host/api/v1/chat/uploads/abc123.png", "abc123.png")]
    [DataRow("/api/v1/chat/uploads/abc123.png?width=1", "abc123.png")]
    [DataRow("/api/v1/files/nodes/1234", null)]
    [DataRow("/api/v1/chat/uploads/../secret.png", null)]
    [DataRow("/api/v1/chat/uploads/nested/secret.png", null)]
    [DataRow("/api/v1/chat/uploads/", null)]
    [DataRow("", null)]
    [DataRow(null, null)]
    public void TryGetStoredUploadName_VariousUrls_ReturnsExpected(string? url, string? expected)
        => Assert.AreEqual(expected, ChatArchiveExporter.TryGetStoredUploadName(url));

    [TestMethod]
    public async Task ExportAsync_ChatOwnedAttachment_CopiesPayloadAndWritesRecordUnderYearMonth()
    {
        var uploadsDirectory = Path.Combine(_root, "chat-uploads");
        Directory.CreateDirectory(uploadsDirectory);
        await File.WriteAllBytesAsync(Path.Combine(uploadsDirectory, StoredUploadName), [1, 2, 3]);

        var message = NewMessage(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
        message.Attachments.Add(NewAttachment(message.Id, "photo.png", thumbnailUrl: $"/api/v1/chat/uploads/{StoredUploadName}"));
        message.Attachments.Add(NewAttachment(message.Id, "doc.pdf", fileNodeId: Guid.CreateVersion7()));

        await using (var db = new ChatDbContext(_options))
        {
            db.Messages.Add(message);
            await db.SaveChangesAsync();
        }

        var settings = new ChatSettings
        {
            RetentionMode = ChatRetentionMode.Archive,
            ArchiveAttachments = true,
            ArchivePath = _root
        };

        await using (var db = new ChatDbContext(_options))
        {
            var outcome = await _exporter.ExportAsync(db, [message.Id], settings);

            Assert.AreEqual(1, outcome.MessagesExported);
            Assert.AreEqual(1, outcome.AttachmentsExported, "Only the chat-owned payload can be copied.");
            Assert.AreEqual(0, outcome.FailedMessageIds.Count);
            Assert.IsFalse(outcome.ArchiveRootUnavailable);
        }

        // Year folder, then month folder.
        var monthFolder = Path.Combine(_root, "2026", "03");
        Assert.IsTrue(Directory.Exists(monthFolder), "Records must be grouped into year/month folders.");

        var recordPath = Path.Combine(monthFolder, $"{message.Id:N}.json");
        Assert.IsTrue(File.Exists(recordPath));

        var json = await File.ReadAllTextAsync(recordPath);
        StringAssert.Contains(json, "hello archive");
        StringAssert.Contains(json, StoredUploadName);
        StringAssert.Contains(json, "doc.pdf");

        CollectionAssert.AreEqual(
            new byte[] { 1, 2, 3 },
            await File.ReadAllBytesAsync(Path.Combine(monthFolder, message.Id.ToString("N"), "photo.png")));
    }

    [TestMethod]
    public async Task ExportAsync_ArchiveAttachmentsDisabled_WritesRecordWithoutCopyingPayloads()
    {
        var uploadsDirectory = Path.Combine(_root, "chat-uploads");
        Directory.CreateDirectory(uploadsDirectory);
        await File.WriteAllBytesAsync(Path.Combine(uploadsDirectory, StoredUploadName), [9, 9]);

        var message = NewMessage(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        message.Attachments.Add(NewAttachment(message.Id, "photo.png", thumbnailUrl: $"/api/v1/chat/uploads/{StoredUploadName}"));

        await using (var db = new ChatDbContext(_options))
        {
            db.Messages.Add(message);
            await db.SaveChangesAsync();
        }

        var settings = new ChatSettings
        {
            RetentionMode = ChatRetentionMode.Archive,
            ArchiveAttachments = false,
            ArchivePath = _root
        };

        await using (var db = new ChatDbContext(_options))
        {
            var outcome = await _exporter.ExportAsync(db, [message.Id], settings);

            Assert.AreEqual(1, outcome.MessagesExported);
            Assert.AreEqual(0, outcome.AttachmentsExported);
        }

        var monthFolder = Path.Combine(_root, "2026", "01");
        Assert.IsTrue(File.Exists(Path.Combine(monthFolder, $"{message.Id:N}.json")));
        Assert.IsFalse(Directory.Exists(Path.Combine(monthFolder, message.Id.ToString("N"))));
    }

    [TestMethod]
    public async Task ExportAsync_ArchiveRootIsAFile_ReportsRootUnavailable()
    {
        var blocker = Path.Combine(_root, "blocker");
        await File.WriteAllTextAsync(blocker, "file, not a directory");

        var message = NewMessage(DateTime.UtcNow);
        await using (var db = new ChatDbContext(_options))
        {
            db.Messages.Add(message);
            await db.SaveChangesAsync();
        }

        var settings = new ChatSettings
        {
            RetentionMode = ChatRetentionMode.Archive,
            ArchivePath = Path.Combine(blocker, "archive")
        };

        await using (var db = new ChatDbContext(_options))
        {
            var outcome = await _exporter.ExportAsync(db, [message.Id], settings);

            Assert.IsTrue(outcome.ArchiveRootUnavailable);
            Assert.AreEqual(0, outcome.MessagesExported);
        }
    }

    private Message NewMessage(DateTime sentAt) => new()
    {
        ChannelId = _channelId,
        SenderUserId = Guid.CreateVersion7(),
        Content = "hello archive",
        SentAt = sentAt
    };

    private static MessageAttachment NewAttachment(Guid messageId, string fileName, string? thumbnailUrl = null, Guid? fileNodeId = null) => new()
    {
        MessageId = messageId,
        FileName = fileName,
        MimeType = "image/png",
        FileSize = 3,
        ThumbnailUrl = thumbnailUrl,
        FileNodeId = fileNodeId
    };
}
