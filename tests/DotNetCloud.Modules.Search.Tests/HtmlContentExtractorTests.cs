using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="HtmlContentExtractor"/>.
/// </summary>
[TestClass]
public class HtmlContentExtractorTests
{
    private HtmlContentExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new HtmlContentExtractor();
    }

    [TestMethod]
    public void CanExtract_TextHtml_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("text/html"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("TEXT/HTML"));
    }

    [TestMethod]
    public void CanExtract_TextPlain_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("text/plain"));
    }

    [TestMethod]
    public async Task ExtractAsync_SimpleHtml_StripsTagsKeepsText()
    {
        var html = "<html><body><h1>Title</h1><p>Hello <strong>world</strong>.</p></body></html>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));

        var result = await _extractor.ExtractAsync(stream, "text/html");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Title"));
        Assert.IsTrue(result.Text.Contains("Hello"));
        Assert.IsTrue(result.Text.Contains("world"));
        Assert.IsFalse(result.Text.Contains("<h1>"));
        Assert.IsFalse(result.Text.Contains("<strong>"));
    }

    [TestMethod]
    public async Task ExtractAsync_RemovesScriptAndStyle()
    {
        var html = "<html><head><style>body { color: red; }</style></head><body><script>alert('xss');</script><p>Text</p></body></html>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));

        var result = await _extractor.ExtractAsync(stream, "text/html");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Text"));
        Assert.IsFalse(result.Text.Contains("alert"));
        Assert.IsFalse(result.Text.Contains("color: red"));
    }

    [TestMethod]
    public async Task ExtractAsync_DecodesHtmlEntities()
    {
        var html = "<p>5 &gt; 3 &amp; 2 &lt; 4</p>";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(html));

        var result = await _extractor.ExtractAsync(stream, "text/html");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("5 > 3 & 2 < 4"));
    }

    [TestMethod]
    public void StripHtml_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, HtmlContentExtractor.StripHtml(string.Empty));
    }

    [TestMethod]
    public void StripHtml_RemovesComments()
    {
        var html = "<p>Before</p><!-- comment --><p>After</p>";
        var result = HtmlContentExtractor.StripHtml(html);
        Assert.IsFalse(result.Contains("comment"));
        Assert.IsTrue(result.Contains("Before"));
        Assert.IsTrue(result.Contains("After"));
    }
}
