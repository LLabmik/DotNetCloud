using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Data.Services;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IEventBus = DotNetCloud.Core.Events.IEventBus;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for link-preview capture during message send/edit in <see cref="MessageService"/>.
/// </summary>
[TestClass]
public class MessageServiceLinkPreviewTests
{
    private static readonly Uri FirstUrl = new("https://first.example/article");
    private static readonly Uri SecondUrl = new("https://second.example/post");

    private ChatDbContext _db = null!;
    private Mock<ILinkPreviewService> _previewMock = null!;
    private MessageService _service = null!;
    private CallerContext _caller = null!;
    private Guid _channelId;

    [TestInitialize]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new ChatDbContext(options);

        _previewMock = new Mock<ILinkPreviewService>();
        _previewMock
            .Setup(p => p.FindFirstUrl(It.IsAny<string>()))
            .Returns<string?>((content) => FindUrl(content));
        _previewMock
            .Setup(p => p.FetchPreviewAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri url, CancellationToken _) => new LinkPreviewResult
            {
                Url = url.ToString(),
                Title = $"Title: {url}",
                SiteName = "Test Site",
                Description = $"Desc for {url}"
            });

        _service = new MessageService(
            _db,
            new Mock<IEventBus>().Object,
            Mock.Of<IAuditLogger>(),
            NullLogger<MessageService>.Instance,
            linkPreviewService: _previewMock.Object);

        _caller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);

        var channel = new Channel { Name = "preview-test", CreatedByUserId = _caller.UserId };
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

    private static Uri? FindUrl(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        if (content.Contains(FirstUrl.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return FirstUrl;
        }

        return content.Contains(SecondUrl.ToString(), StringComparison.OrdinalIgnoreCase) ? SecondUrl : null;
    }

    [TestMethod]
    public async Task SendMessage_WithUrl_CapturesAndPersistsPreview()
    {
        var dto = new SendMessageDto { Content = $"Read this: {FirstUrl}" };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNotNull(result.LinkPreview);
        Assert.AreEqual(FirstUrl.ToString(), result.LinkPreview!.Url);
        Assert.AreEqual($"Title: {FirstUrl}", result.LinkPreview.Title);

        var stored = await _db.MessageLinkPreviews.SingleOrDefaultAsync(p => p.MessageId == result.Id);
        Assert.IsNotNull(stored);
        Assert.AreEqual($"Title: {FirstUrl}", stored!.Title);
    }

    [TestMethod]
    public async Task SendMessage_WithoutUrl_ProducesNoPreview()
    {
        var dto = new SendMessageDto { Content = "No links in this message." };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNull(result.LinkPreview);
        Assert.AreEqual(0, await _db.MessageLinkPreviews.CountAsync());
    }

    [TestMethod]
    public async Task SendMessage_WhenFetchFails_StillSendsWithoutPreview()
    {
        _previewMock
            .Setup(p => p.FetchPreviewAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkPreviewResult?)null);

        var dto = new SendMessageDto { Content = $"Check {FirstUrl}" };

        var result = await _service.SendMessageAsync(_channelId, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual($"Check {FirstUrl}", result.Content);
        Assert.IsNull(result.LinkPreview);
        Assert.AreEqual(0, await _db.MessageLinkPreviews.CountAsync());
    }

    [TestMethod]
    public async Task EditMessage_ChangingUrl_RefreshesPreview()
    {
        var sent = await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = $"A: {FirstUrl}" }, _caller);
        Assert.IsNotNull(sent.LinkPreview);
        Assert.AreEqual(FirstUrl.ToString(), sent.LinkPreview!.Url);

        var edited = await _service.EditMessageAsync(
            sent.Id, new EditMessageDto { Content = $"B: {SecondUrl}" }, _caller);

        Assert.IsNotNull(edited.LinkPreview);
        Assert.AreEqual(SecondUrl.ToString(), edited.LinkPreview!.Url);
        Assert.AreEqual($"Title: {SecondUrl}", edited.LinkPreview.Title);

        var previews = await _db.MessageLinkPreviews.Where(p => p.MessageId == sent.Id).ToListAsync();
        Assert.AreEqual(1, previews.Count);
        Assert.AreEqual(SecondUrl.ToString(), previews[0].Url);
    }

    [TestMethod]
    public async Task EditMessage_RemovingUrl_RemovesPreview()
    {
        var sent = await _service.SendMessageAsync(_channelId, new SendMessageDto { Content = $"A: {FirstUrl}" }, _caller);
        Assert.IsNotNull(sent.LinkPreview);

        var edited = await _service.EditMessageAsync(
            sent.Id, new EditMessageDto { Content = "No more links." }, _caller);

        Assert.IsNull(edited.LinkPreview);
        Assert.AreEqual(0, await _db.MessageLinkPreviews.CountAsync(p => p.MessageId == sent.Id));
    }
}
