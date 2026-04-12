using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="ContentExtractionService"/>.
/// </summary>
[TestClass]
public class ContentExtractionServiceTests
{
    private Mock<IContentExtractor> _textExtractorMock = null!;
    private Mock<IContentExtractor> _pdfExtractorMock = null!;
    private ContentExtractionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _textExtractorMock = new Mock<IContentExtractor>();
        _textExtractorMock.Setup(e => e.CanExtract("text/plain")).Returns(true);

        _pdfExtractorMock = new Mock<IContentExtractor>();
        _pdfExtractorMock.Setup(e => e.CanExtract("application/pdf")).Returns(true);

        _service = new ContentExtractionService(
            [_textExtractorMock.Object, _pdfExtractorMock.Object],
            NullLogger<ContentExtractionService>.Instance);
    }

    [TestMethod]
    public async Task ExtractAsync_SupportedMimeType_UsesCorrectExtractor()
    {
        var expected = new ExtractedContent { Text = "extracted text" };
        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        using var stream = new MemoryStream("Hello world"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual("extracted text", result.Text);
        _pdfExtractorMock.Verify(
            e => e.ExtractAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ExtractAsync_UnsupportedMimeType_ReturnsNull()
    {
        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "application/unknown");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_NullMimeType_ReturnsNull()
    {
        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "   ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_NullStream_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.ExtractAsync(null!, "text/plain"));
    }

    [TestMethod]
    public async Task ExtractAsync_ExtractorReturnsNull_ReturnsNull()
    {
        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractedContent?)null);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "text/plain");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_ExtractorThrows_ReturnsNull()
    {
        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("parse error"));

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "text/plain");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_LargeContent_TruncatesToMaxLength()
    {
        var largeText = new string('x', ContentExtractionService.MaxContentLength + 1000);
        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent { Text = largeText });

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(ContentExtractionService.MaxContentLength, result.Text.Length);
    }

    [TestMethod]
    public async Task ExtractAsync_ContentWithinLimit_NotTruncated()
    {
        var normalText = new string('x', 100);
        _textExtractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent { Text = normalText });

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await _service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(100, result.Text.Length);
    }

    [TestMethod]
    public void CanExtract_SupportedType_ReturnsTrue()
    {
        Assert.IsTrue(_service.CanExtract("text/plain"));
        Assert.IsTrue(_service.CanExtract("application/pdf"));
    }

    [TestMethod]
    public void CanExtract_UnsupportedType_ReturnsFalse()
    {
        Assert.IsFalse(_service.CanExtract("video/mp4"));
        Assert.IsFalse(_service.CanExtract("application/unknown"));
    }
}
