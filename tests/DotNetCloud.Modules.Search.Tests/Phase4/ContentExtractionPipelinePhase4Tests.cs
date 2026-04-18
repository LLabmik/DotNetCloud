using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Extractors;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase4;

/// <summary>
/// Phase 4 tests for the content extraction pipeline integration —
/// verifies end-to-end content extraction with real extractors
/// and the <see cref="ContentExtractionService"/> orchestration.
/// </summary>
[TestClass]
public class ContentExtractionPipelinePhase4Tests
{
    // --- PlainTextExtractor Tests ---

    [TestMethod]
    public async Task PlainTextExtractor_ExtractsFullContent()
    {
        var extractor = new PlainTextExtractor();
        using var stream = new MemoryStream("Hello world, this is test content."u8.ToArray());

        var result = await extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello world, this is test content.", result.Text);
    }

    [TestMethod]
    public async Task PlainTextExtractor_SupportsCsv()
    {
        var extractor = new PlainTextExtractor();
        var csv = "Name,Age,City\nAlice,30,NYC\nBob,25,LA";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await extractor.ExtractAsync(stream, "text/csv");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Alice"));
        Assert.IsTrue(result.Text.Contains("Bob"));
    }

    [TestMethod]
    public void PlainTextExtractor_CanExtract_SupportedTypes()
    {
        var extractor = new PlainTextExtractor();

        Assert.IsTrue(extractor.CanExtract("text/plain"));
        Assert.IsTrue(extractor.CanExtract("text/csv"));
        Assert.IsTrue(extractor.CanExtract("TEXT/PLAIN")); // case insensitive
        Assert.IsFalse(extractor.CanExtract("application/pdf"));
        Assert.IsFalse(extractor.CanExtract("text/html"));
    }

    [TestMethod]
    public async Task PlainTextExtractor_EmptyStream_ReturnsEmptyText()
    {
        var extractor = new PlainTextExtractor();
        using var stream = new MemoryStream();

        var result = await extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public async Task PlainTextExtractor_Metadata_ContainsMimeType()
    {
        var extractor = new PlainTextExtractor();
        using var stream = new MemoryStream("data"u8.ToArray());

        var result = await extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual("text/plain", result.Metadata["mimeType"]);
    }

    // --- MarkdownContentExtractor Tests ---

    [TestMethod]
    public async Task MarkdownExtractor_StripsHeadings()
    {
        var extractor = new MarkdownContentExtractor();
        var markdown = "# Main Title\n\n## Section\n\nContent here.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Main Title"));
        Assert.IsTrue(result.Text.Contains("Content here"));
        Assert.IsFalse(result.Text.Contains("# "));
        Assert.IsFalse(result.Text.Contains("## "));
    }

    [TestMethod]
    public async Task MarkdownExtractor_StripsLinks()
    {
        var extractor = new MarkdownContentExtractor();
        var markdown = "Check out [DotNetCloud](https://example.com) for details.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("DotNetCloud"));
        Assert.IsFalse(result.Text.Contains("]("));
    }

    [TestMethod]
    public async Task MarkdownExtractor_StripsBoldAndItalic()
    {
        var extractor = new MarkdownContentExtractor();
        var markdown = "This is **bold** and *italic* and ***both***.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("bold"));
        Assert.IsTrue(result.Text.Contains("italic"));
        Assert.IsFalse(result.Text.Contains("**"));
        Assert.IsFalse(result.Text.Contains("***"));
    }

    [TestMethod]
    public async Task MarkdownExtractor_StripsCodeBlocks()
    {
        var extractor = new MarkdownContentExtractor();
        var markdown = "Before\n```csharp\nvar x = 1;\n```\nAfter";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Before"));
        Assert.IsTrue(result.Text.Contains("After"));
        Assert.IsFalse(result.Text.Contains("```"));
    }

    [TestMethod]
    public void MarkdownExtractor_CanExtract_OnlyMarkdown()
    {
        var extractor = new MarkdownContentExtractor();

        Assert.IsTrue(extractor.CanExtract("text/markdown"));
        Assert.IsTrue(extractor.CanExtract("TEXT/MARKDOWN"));
        Assert.IsFalse(extractor.CanExtract("text/plain"));
        Assert.IsFalse(extractor.CanExtract("text/html"));
    }

    // --- ContentExtractionService Pipeline Tests ---

    [TestMethod]
    public async Task Pipeline_RoutesToCorrectExtractor()
    {
        var plainExtractor = new PlainTextExtractor();
        var mdExtractor = new MarkdownContentExtractor();
        var service = new ContentExtractionService(
            [plainExtractor, mdExtractor],
            NullLogger<ContentExtractionService>.Instance);

        using var txtStream = new MemoryStream("plain text"u8.ToArray());
        var plainResult = await service.ExtractAsync(txtStream, "text/plain");

        using var mdStream = new MemoryStream("# Heading"u8.ToArray());
        var mdResult = await service.ExtractAsync(mdStream, "text/markdown");

        Assert.IsNotNull(plainResult);
        Assert.AreEqual("plain text", plainResult.Text);

        Assert.IsNotNull(mdResult);
        Assert.IsTrue(mdResult.Text.Contains("Heading"));
        Assert.IsFalse(mdResult.Text.Contains("#"));
    }

    [TestMethod]
    public async Task Pipeline_TruncatesLargeContent()
    {
        var mockExtractor = new Mock<IContentExtractor>();
        mockExtractor.Setup(e => e.CanExtract("text/plain")).Returns(true);
        mockExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent
            {
                Text = new string('x', ContentExtractionService.MaxContentLength + 5000),
                Metadata = new Dictionary<string, string>()
            });

        var service = new ContentExtractionService(
            [mockExtractor.Object],
            NullLogger<ContentExtractionService>.Instance);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(ContentExtractionService.MaxContentLength, result.Text.Length);
    }

    [TestMethod]
    public void Pipeline_MaxContentLength_Is100KB()
    {
        Assert.AreEqual(102400, ContentExtractionService.MaxContentLength);
    }

    [TestMethod]
    public async Task Pipeline_ExtractorException_ReturnsNull()
    {
        var faultyExtractor = new Mock<IContentExtractor>();
        faultyExtractor.Setup(e => e.CanExtract("text/plain")).Returns(true);
        faultyExtractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("Corrupt document"));

        var service = new ContentExtractionService(
            [faultyExtractor.Object],
            NullLogger<ContentExtractionService>.Instance);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await service.ExtractAsync(stream, "text/plain");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Pipeline_CancellationToken_Propagated()
    {
        var extractor = new Mock<IContentExtractor>();
        extractor.Setup(e => e.CanExtract("text/plain")).Returns(true);
        extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new ContentExtractionService(
            [extractor.Object],
            NullLogger<ContentExtractionService>.Instance);

        using var stream = new MemoryStream("data"u8.ToArray());

        // OperationCanceledException should propagate (not swallowed)
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.ExtractAsync(stream, "text/plain"));
    }

    [TestMethod]
    public async Task Pipeline_MultipleExtractors_FirstMatchWins()
    {
        var extractor1 = new Mock<IContentExtractor>();
        extractor1.Setup(e => e.CanExtract("text/plain")).Returns(true);
        extractor1
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent { Text = "from extractor 1" });

        var extractor2 = new Mock<IContentExtractor>();
        extractor2.Setup(e => e.CanExtract("text/plain")).Returns(true);
        extractor2
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent { Text = "from extractor 2" });

        var service = new ContentExtractionService(
            [extractor1.Object, extractor2.Object],
            NullLogger<ContentExtractionService>.Instance);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual("from extractor 1", result.Text);
    }

    [TestMethod]
    public void Pipeline_CanExtract_AllRegisteredTypes()
    {
        var service = new ContentExtractionService(
            [new PlainTextExtractor(), new MarkdownContentExtractor()],
            NullLogger<ContentExtractionService>.Instance);

        Assert.IsTrue(service.CanExtract("text/plain"));
        Assert.IsTrue(service.CanExtract("text/csv"));
        Assert.IsTrue(service.CanExtract("text/markdown"));
        Assert.IsFalse(service.CanExtract("application/pdf")); // PdfPig not registered
        Assert.IsFalse(service.CanExtract("image/png"));
    }

    [TestMethod]
    public async Task Pipeline_PreservesMetadataFromExtractor()
    {
        var extractor = new Mock<IContentExtractor>();
        extractor.Setup(e => e.CanExtract("text/plain")).Returns(true);
        extractor
            .Setup(e => e.ExtractAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedContent
            {
                Text = "content",
                Metadata = new Dictionary<string, string>
                {
                    ["author"] = "John Doe",
                    ["pageCount"] = "5",
                    ["title"] = "My Document"
                }
            });

        var service = new ContentExtractionService(
            [extractor.Object],
            NullLogger<ContentExtractionService>.Instance);

        using var stream = new MemoryStream("data"u8.ToArray());
        var result = await service.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual("John Doe", result.Metadata["author"]);
        Assert.AreEqual("5", result.Metadata["pageCount"]);
        Assert.AreEqual("My Document", result.Metadata["title"]);
    }
}
