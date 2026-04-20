using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for inline attachment support in <see cref="MessageService.SendMessageAsync"/>.
/// </summary>
[TestClass]
public class MessageServiceInlineAttachmentTests
{
    private ChatDbContext _db = null!;
    private MessageService _service = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ChatDbContext(options);
        var eventBus = new Mock<IEventBus>();
        _service = new MessageService(_db, eventBus.Object, NullLogger<MessageService>.Instance);
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        var channel = new Channel { Name = "test", CreatedByUserId = _caller.UserId };
        _db.Channels.Add(channel);
        _db.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = _caller.UserId,
            Role = ChannelMemberRole.Owner
        });
        await _db.SaveChangesAsync();
        _channelId = channel.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ── Attachment-only messages ────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_AttachmentsOnly_Succeeds()
    {
        var dto = new SendMessageDto
        {
            Content = "",
            Attachments = new List<CreateAttachmentDto>
            {
                new()
                {
                    FileName = "photo.png",
                    MimeType = "image/png",
                    FileSize = 1024,
                    ThumbnailUrl = "/api/v1/chat/uploads/abc.png"
                }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Attachments.Count);
        Assert.AreEqual("photo.png", result.Attachments[0].FileName);
    }

    [TestMethod]
    public async Task SendMessage_WhitespaceContentWithAttachment_Succeeds()
    {
        var dto = new SendMessageDto
        {
            Content = "   ",
            Attachments = new List<CreateAttachmentDto>
            {
                new() { FileName = "img.jpg", MimeType = "image/jpeg", FileSize = 512 }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Attachments.Count);
    }

    [TestMethod]
    public async Task SendMessage_NoContentNoAttachments_ThrowsArgumentException()
    {
        var dto = new SendMessageDto { Content = "" };

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMessageAsync(_channelId, dto, _caller));
    }

    [TestMethod]
    public async Task SendMessage_NullAttachments_RequiresContent()
    {
        var dto = new SendMessageDto { Content = "", Attachments = null };

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMessageAsync(_channelId, dto, _caller));
    }

    [TestMethod]
    public async Task SendMessage_EmptyAttachmentList_RequiresContent()
    {
        var dto = new SendMessageDto { Content = "", Attachments = new List<CreateAttachmentDto>() };

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _service.SendMessageAsync(_channelId, dto, _caller));
    }

    // ── Content + Attachments combined ─────────────────────────────

    [TestMethod]
    public async Task SendMessage_ContentPlusAttachments_BothPreserved()
    {
        var dto = new SendMessageDto
        {
            Content = "Check out this photo!",
            Attachments = new List<CreateAttachmentDto>
            {
                new()
                {
                    FileName = "vacation.jpg",
                    MimeType = "image/jpeg",
                    FileSize = 2048,
                    ThumbnailUrl = "/api/v1/chat/uploads/vacation.jpg"
                }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.AreEqual("Check out this photo!", result.Content);
        Assert.AreEqual(1, result.Attachments.Count);
        Assert.AreEqual("vacation.jpg", result.Attachments[0].FileName);
        Assert.AreEqual("image/jpeg", result.Attachments[0].MimeType);
        Assert.AreEqual(2048, result.Attachments[0].FileSize);
    }

    // ── Multiple attachments ───────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_MultipleAttachments_AllPersisted()
    {
        var dto = new SendMessageDto
        {
            Content = "Album:",
            Attachments = new List<CreateAttachmentDto>
            {
                new() { FileName = "a.png", MimeType = "image/png", FileSize = 100 },
                new() { FileName = "b.jpg", MimeType = "image/jpeg", FileSize = 200 },
                new() { FileName = "c.gif", MimeType = "image/gif", FileSize = 300 }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.AreEqual(3, result.Attachments.Count);
        Assert.AreEqual("a.png", result.Attachments[0].FileName);
        Assert.AreEqual("b.jpg", result.Attachments[1].FileName);
        Assert.AreEqual("c.gif", result.Attachments[2].FileName);
    }

    [TestMethod]
    public async Task SendMessage_MultipleAttachments_SortOrderPreserved()
    {
        var dto = new SendMessageDto
        {
            Content = "Multiple files",
            Attachments = new List<CreateAttachmentDto>
            {
                new() { FileName = "first.png", MimeType = "image/png", FileSize = 100 },
                new() { FileName = "second.png", MimeType = "image/png", FileSize = 200 }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        // Verify sort order in database
        var message = await _db.Messages
            .Include(m => m.Attachments)
            .FirstAsync(m => m.Id == result.Id);

        var ordered = message.Attachments.OrderBy(a => a.SortOrder).ToList();
        Assert.AreEqual("first.png", ordered[0].FileName);
        Assert.AreEqual(0, ordered[0].SortOrder);
        Assert.AreEqual("second.png", ordered[1].FileName);
        Assert.AreEqual(1, ordered[1].SortOrder);
    }

    // ── Attachment field mapping ────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_Attachment_AllFieldsMapped()
    {
        var fileNodeId = Guid.NewGuid();
        var dto = new SendMessageDto
        {
            Content = "File attached",
            Attachments = new List<CreateAttachmentDto>
            {
                new()
                {
                    FileName = "document.png",
                    MimeType = "image/png",
                    FileSize = 4096,
                    ThumbnailUrl = "/api/v1/chat/uploads/thumb.png",
                    FileNodeId = fileNodeId
                }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        var att = result.Attachments[0];
        Assert.AreEqual("document.png", att.FileName);
        Assert.AreEqual("image/png", att.MimeType);
        Assert.AreEqual(4096, att.FileSize);
        Assert.AreEqual("/api/v1/chat/uploads/thumb.png", att.ThumbnailUrl);
        Assert.AreEqual(fileNodeId, att.FileNodeId);
    }

    [TestMethod]
    public async Task SendMessage_Attachment_NullOptionalFields_Succeeds()
    {
        var dto = new SendMessageDto
        {
            Content = "Minimal",
            Attachments = new List<CreateAttachmentDto>
            {
                new()
                {
                    FileName = "img.png",
                    MimeType = "image/png",
                    FileSize = 512,
                    ThumbnailUrl = null,
                    FileNodeId = null
                }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        var att = result.Attachments[0];
        Assert.IsNull(att.ThumbnailUrl);
        Assert.IsNull(att.FileNodeId);
    }

    // ── Persistence verification ───────────────────────────────────

    [TestMethod]
    public async Task SendMessage_WithAttachments_PersistedToDatabase()
    {
        var dto = new SendMessageDto
        {
            Content = "Persisted",
            Attachments = new List<CreateAttachmentDto>
            {
                new() { FileName = "saved.png", MimeType = "image/png", FileSize = 256 }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        var dbAttachments = await _db.MessageAttachments
            .Where(a => a.MessageId == result.Id)
            .ToListAsync();

        Assert.AreEqual(1, dbAttachments.Count);
        Assert.AreEqual("saved.png", dbAttachments[0].FileName);
        Assert.AreEqual("image/png", dbAttachments[0].MimeType);
        Assert.AreEqual(256, dbAttachments[0].FileSize);
    }

    [TestMethod]
    public async Task SendMessage_AttachmentLinkedToCorrectMessage()
    {
        var dto = new SendMessageDto
        {
            Content = "Linked",
            Attachments = new List<CreateAttachmentDto>
            {
                new() { FileName = "linked.jpg", MimeType = "image/jpeg", FileSize = 128 }
            }
        };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        var attachment = await _db.MessageAttachments.FirstAsync(a => a.MessageId == result.Id);
        Assert.AreEqual(result.Id, attachment.MessageId);
    }
}
