using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="PlainTextExtractor"/>.
/// </summary>
[TestClass]
public class PlainTextExtractorTests
{
    private PlainTextExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new PlainTextExtractor();
    }

    [TestMethod]
    public void CanExtract_TextPlain_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("text/plain"));
    }

    [TestMethod]
    public void CanExtract_TextCsv_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("text/csv"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("TEXT/PLAIN"));
        Assert.IsTrue(_extractor.CanExtract("Text/Csv"));
    }

    [TestMethod]
    public void CanExtract_Pdf_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("application/pdf"));
    }

    [TestMethod]
    public void CanExtract_Docx_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    [TestMethod]
    public async Task ExtractAsync_TextPlain_ReturnsContent()
    {
        var text = "Hello, world! This is a test document.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

        var result = await _extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(text, result.Text);
        Assert.AreEqual("text/plain", result.Metadata["mimeType"]);
    }

    [TestMethod]
    public async Task ExtractAsync_CsvContent_ReturnsContent()
    {
        var csv = "Name,Age,City\nAlice,30,NYC\nBob,25,LA";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await _extractor.ExtractAsync(stream, "text/csv");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Alice"));
        Assert.IsTrue(result.Text.Contains("Bob"));
        Assert.AreEqual("text/csv", result.Metadata["mimeType"]);
    }

    [TestMethod]
    public async Task ExtractAsync_EmptyFile_ReturnsEmptyText()
    {
        using var stream = new MemoryStream([]);

        var result = await _extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result.Text);
    }

    [TestMethod]
    public async Task ExtractAsync_UnicodeContent_PreservesEncoding()
    {
        var text = "日本語テスト 🎉 Ñoño";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

        var result = await _extractor.ExtractAsync(stream, "text/plain");

        Assert.IsNotNull(result);
        Assert.AreEqual(text, result.Text);
    }
}
