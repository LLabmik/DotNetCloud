using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class SubtitleServiceTests
{
    private VideoDbContext _db = null!;
    private SubtitleService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _service = new SubtitleService(_db, Mock.Of<ILogger<SubtitleService>>());
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task UploadSubtitleAsync_CreatesSubtitleWithValidSrt()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var dto = new Core.DTOs.UploadSubtitleDto
        {
            Language = "en",
            Label = "English",
            Format = "srt",
            Content = "1\n00:00:01,000 --> 00:00:04,000\nHello"
        };

        var result = await _service.UploadSubtitleAsync(video.Id, dto, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("en", result.Language);
        Assert.AreEqual("English", result.Label);
        Assert.AreEqual("srt", result.Format);
    }

    [TestMethod]
    public async Task UploadSubtitleAsync_CreatesSubtitleWithValidVtt()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var dto = new Core.DTOs.UploadSubtitleDto
        {
            Language = "en",
            Label = "English (VTT)",
            Format = "vtt",
            Content = "WEBVTT\n\n00:00:01.000 --> 00:00:04.000\nHello"
        };

        var result = await _service.UploadSubtitleAsync(video.Id, dto, caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("vtt", result.Format);
    }

    [TestMethod]
    public async Task UploadSubtitleAsync_ThrowsForInvalidFormat()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var dto = new Core.DTOs.UploadSubtitleDto
        {
            Language = "en",
            Label = "English",
            Format = "ass",
            Content = "some content"
        };

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.UploadSubtitleAsync(video.Id, dto, caller));

        Assert.AreEqual(ErrorCodes.InvalidSubtitleFormat, ex.ErrorCode);
    }

    [TestMethod]
    public async Task GetSubtitlesAsync_ReturnsSubtitlesForVideo()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        await TestHelpers.SeedSubtitleAsync(_db, video.Id, "en", ownerId: caller.UserId);
        await TestHelpers.SeedSubtitleAsync(_db, video.Id, "fr", ownerId: caller.UserId);

        var result = await _service.GetSubtitlesAsync(video.Id, caller);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetSubtitleContentAsync_ReturnsContent()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var subtitle = await TestHelpers.SeedSubtitleAsync(_db, video.Id, ownerId: caller.UserId);

        var result = await _service.GetSubtitleContentAsync(subtitle.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Value.Content.Contains("Hello World"));
        Assert.AreEqual("srt", result.Value.Format);
    }

    [TestMethod]
    public async Task GetSubtitleContentAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetSubtitleContentAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteSubtitleAsync_RemovesSubtitle()
    {
        var caller = TestHelpers.CreateCaller();
        var video = await TestHelpers.SeedVideoAsync(_db, ownerId: caller.UserId);
        var subtitle = await TestHelpers.SeedSubtitleAsync(_db, video.Id, ownerId: caller.UserId);

        await _service.DeleteSubtitleAsync(subtitle.Id, caller);

        var result = await _service.GetSubtitlesAsync(video.Id, caller);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task DeleteSubtitleAsync_ThrowsForNonExistent()
    {
        var caller = TestHelpers.CreateCaller();

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => _service.DeleteSubtitleAsync(Guid.NewGuid(), caller));

        Assert.AreEqual(ErrorCodes.SubtitleNotFound, ex.ErrorCode);
    }
}
