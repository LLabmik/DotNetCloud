using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="MarkdownContentExtractor"/>.
/// </summary>
[TestClass]
public class MarkdownContentExtractorTests
{
    private MarkdownContentExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new MarkdownContentExtractor();
    }

    [TestMethod]
    public void CanExtract_TextMarkdown_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("text/markdown"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("TEXT/MARKDOWN"));
    }

    [TestMethod]
    public void CanExtract_TextPlain_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("text/plain"));
    }

    [TestMethod]
    public void CanExtract_Pdf_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("application/pdf"));
    }

    [TestMethod]
    public async Task ExtractAsync_SimpleMarkdown_StripsHeadings()
    {
        var markdown = "# Title\n\nSome paragraph text.\n\n## Subtitle\n\nMore text.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Text.Contains("#"));
        Assert.IsTrue(result.Text.Contains("Title"));
        Assert.IsTrue(result.Text.Contains("Some paragraph text."));
        Assert.IsTrue(result.Text.Contains("Subtitle"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsBoldAndItalic()
    {
        var markdown = "This is **bold** and *italic* and ***both***.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Text.Contains("**"));
        Assert.IsTrue(result.Text.Contains("bold"));
        Assert.IsTrue(result.Text.Contains("italic"));
        Assert.IsTrue(result.Text.Contains("both"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsLinks()
    {
        var markdown = "Visit [Google](https://google.com) for info.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Google"));
        Assert.IsFalse(result.Text.Contains("https://google.com"));
        Assert.IsFalse(result.Text.Contains("]("));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsImages()
    {
        var markdown = "![Alt text](image.png) Some text.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Alt text"));
        Assert.IsFalse(result.Text.Contains("image.png"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsCodeBlocks()
    {
        var markdown = "Before code\n```csharp\nvar x = 1;\n```\nAfter code";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Before code"));
        Assert.IsTrue(result.Text.Contains("After code"));
        Assert.IsFalse(result.Text.Contains("```"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsInlineCode()
    {
        var markdown = "Use the `Console.WriteLine` method.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Console.WriteLine"));
        Assert.IsFalse(result.Text.Contains("`"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsBlockquotes()
    {
        var markdown = "> This is a quote\n> With multiple lines";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("This is a quote"));
        Assert.IsFalse(result.Text.StartsWith(">"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsUnorderedListMarkers()
    {
        var markdown = "- Item 1\n- Item 2\n* Item 3";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Item 1"));
        Assert.IsTrue(result.Text.Contains("Item 2"));
        Assert.IsTrue(result.Text.Contains("Item 3"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsOrderedListMarkers()
    {
        var markdown = "1. First\n2. Second\n3. Third";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("First"));
        Assert.IsTrue(result.Text.Contains("Second"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsStrikethrough()
    {
        var markdown = "This is ~~deleted~~ text.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("deleted"));
        Assert.IsFalse(result.Text.Contains("~~"));
    }

    [TestMethod]
    public async Task ExtractAsync_StripsHorizontalRules()
    {
        var markdown = "Above\n---\nBelow";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Above"));
        Assert.IsTrue(result.Text.Contains("Below"));
    }

    [TestMethod]
    public async Task ExtractAsync_HasMimeTypeMetadata()
    {
        var markdown = "# Hello";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));

        var result = await _extractor.ExtractAsync(stream, "text/markdown");

        Assert.IsNotNull(result);
        Assert.AreEqual("text/markdown", result.Metadata["mimeType"]);
    }

    [TestMethod]
    public void StripMarkdown_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownContentExtractor.StripMarkdown(string.Empty);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void StripMarkdown_NullString_ReturnsEmpty()
    {
        var result = MarkdownContentExtractor.StripMarkdown(null!);
        Assert.AreEqual(string.Empty, result);
    }
}
