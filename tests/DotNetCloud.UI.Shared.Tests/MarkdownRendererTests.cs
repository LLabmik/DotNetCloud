using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Unit tests for <see cref="MarkdownRenderer"/>.
/// </summary>
[TestClass]
public class MarkdownRendererTests
{
    private MarkdownRenderer _renderer = null!;

    [TestInitialize]
    public void Setup()
    {
        _renderer = new MarkdownRenderer();
    }

    // ─── RenderToHtml ────────────────────────────────────────────────

    [TestMethod]
    public void RenderToHtml_NullInput_ReturnsEmpty()
    {
        var result = _renderer.RenderToHtml(null!);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void RenderToHtml_EmptyInput_ReturnsEmpty()
    {
        var result = _renderer.RenderToHtml("");
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void RenderToHtml_PlainText_WrapsInParagraph()
    {
        var result = _renderer.RenderToHtml("Hello world");
        Assert.IsTrue(result.Contains("<p>Hello world</p>"));
    }

    [TestMethod]
    public void RenderToHtml_Bold_RendersStrong()
    {
        var result = _renderer.RenderToHtml("This is **bold** text");
        Assert.IsTrue(result.Contains("<strong>bold</strong>"));
    }

    [TestMethod]
    public void RenderToHtml_Italic_RendersEm()
    {
        var result = _renderer.RenderToHtml("This is *italic* text");
        Assert.IsTrue(result.Contains("<em>italic</em>"));
    }

    [TestMethod]
    public void RenderToHtml_Heading_RendersH1()
    {
        var result = _renderer.RenderToHtml("# Heading");
        Assert.IsTrue(result.Contains("<h1"));
        Assert.IsTrue(result.Contains("Heading"));
        Assert.IsTrue(result.Contains("</h1>"));
    }

    [TestMethod]
    public void RenderToHtml_CodeBlock_RendersPreCode()
    {
        var result = _renderer.RenderToHtml("```\nvar x = 1;\n```");
        Assert.IsTrue(result.Contains("<pre>"));
        Assert.IsTrue(result.Contains("<code>"));
    }

    [TestMethod]
    public void RenderToHtml_InlineCode_RendersCode()
    {
        var result = _renderer.RenderToHtml("Use `dotnet build` command");
        Assert.IsTrue(result.Contains("<code>dotnet build</code>"));
    }

    [TestMethod]
    public void RenderToHtml_Link_RendersAnchor()
    {
        var result = _renderer.RenderToHtml("[DotNetCloud](https://example.com)");
        Assert.IsTrue(result.Contains("<a"));
        Assert.IsTrue(result.Contains("href=\"https://example.com\""));
        Assert.IsTrue(result.Contains("DotNetCloud"));
    }

    [TestMethod]
    public void RenderToHtml_UnorderedList_RendersUlLi()
    {
        var result = _renderer.RenderToHtml("- Item 1\n- Item 2\n- Item 3");
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("<li>Item 1</li>"));
        Assert.IsTrue(result.Contains("<li>Item 2</li>"));
        Assert.IsTrue(result.Contains("<li>Item 3</li>"));
    }

    [TestMethod]
    public void RenderToHtml_OrderedList_RendersOlLi()
    {
        var result = _renderer.RenderToHtml("1. First\n2. Second");
        Assert.IsTrue(result.Contains("<ol>"));
        Assert.IsTrue(result.Contains("<li>First</li>"));
    }

    [TestMethod]
    public void RenderToHtml_Blockquote_RendersBlockquote()
    {
        var result = _renderer.RenderToHtml("> Quoted text");
        Assert.IsTrue(result.Contains("<blockquote>"));
    }

    [TestMethod]
    public void RenderToHtml_TaskList_RendersCheckboxes()
    {
        var result = _renderer.RenderToHtml("- [x] Done\n- [ ] Todo");
        Assert.IsTrue(result.Contains("<input"));
        Assert.IsTrue(result.Contains("checked"));
    }

    [TestMethod]
    public void RenderToHtml_Table_RendersTableElements()
    {
        var md = "| Col1 | Col2 |\n|------|------|\n| A    | B    |";
        var result = _renderer.RenderToHtml(md);
        Assert.IsTrue(result.Contains("<table>"));
        Assert.IsTrue(result.Contains("<th>"));
        Assert.IsTrue(result.Contains("<td>"));
    }

    // ─── XSS sanitization ───────────────────────────────────────────

    [TestMethod]
    public void RenderToHtml_ScriptTag_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<script>alert('xss')</script>");
        Assert.IsFalse(result.Contains("<script>"));
        Assert.IsFalse(result.Contains("alert("));
    }

    [TestMethod]
    public void RenderToHtml_OnClickHandler_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<div onclick=\"alert('xss')\">text</div>");
        Assert.IsFalse(result.Contains("onclick"));
        Assert.IsFalse(result.Contains("alert("));
    }

    [TestMethod]
    public void RenderToHtml_JavascriptUrl_IsSanitized()
    {
        var result = _renderer.RenderToHtml("[click](javascript:alert('xss'))");
        Assert.IsFalse(result.Contains("javascript:"));
    }

    [TestMethod]
    public void RenderToHtml_IframeTag_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<iframe src=\"https://evil.com\"></iframe>");
        Assert.IsFalse(result.Contains("<iframe"));
    }

    [TestMethod]
    public void RenderToHtml_ImgOnError_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<img src=\"x\" onerror=\"alert('xss')\">");
        Assert.IsFalse(result.Contains("onerror"));
    }

    [TestMethod]
    public void RenderToHtml_StyleTagWithExpression_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<style>body{background:url('javascript:alert(1)')}</style>");
        Assert.IsFalse(result.Contains("<style>"));
    }

    [TestMethod]
    public void RenderToHtml_DataUrl_IsSanitized()
    {
        var result = _renderer.RenderToHtml("<a href=\"data:text/html,<script>alert(1)</script>\">click</a>");
        Assert.IsFalse(result.Contains("data:text/html"));
    }

    [TestMethod]
    public void RenderToHtml_AllowsHttpsLinks()
    {
        var result = _renderer.RenderToHtml("[link](https://safe.example.com)");
        Assert.IsTrue(result.Contains("https://safe.example.com"));
    }

    [TestMethod]
    public void RenderToHtml_AllowsMailtoLinks()
    {
        var result = _renderer.RenderToHtml("[email](mailto:user@example.com)");
        Assert.IsTrue(result.Contains("mailto:user@example.com"));
    }

    // ─── SanitizeHtml ────────────────────────────────────────────────

    [TestMethod]
    public void SanitizeHtml_NullInput_ReturnsEmpty()
    {
        var result = _renderer.SanitizeHtml(null!);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeHtml_EmptyInput_ReturnsEmpty()
    {
        var result = _renderer.SanitizeHtml("");
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void SanitizeHtml_SafeHtml_PreservesContent()
    {
        var html = "<p>Hello <strong>world</strong></p>";
        var result = _renderer.SanitizeHtml(html);
        Assert.AreEqual(html, result);
    }

    [TestMethod]
    public void SanitizeHtml_UnsafeHtml_StripsScripts()
    {
        var html = "<p>Hello</p><script>alert('xss')</script><p>world</p>";
        var result = _renderer.SanitizeHtml(html);
        Assert.IsFalse(result.Contains("<script>"));
        Assert.IsTrue(result.Contains("<p>Hello</p>"));
        Assert.IsTrue(result.Contains("<p>world</p>"));
    }

    [TestMethod]
    public void SanitizeHtml_FormTag_IsStripped()
    {
        var result = _renderer.SanitizeHtml("<form action=\"/steal\"><input type=\"text\"></form>");
        Assert.IsFalse(result.Contains("<form"));
        Assert.IsFalse(result.Contains("action="));
    }

    // ─── GetPlainTextExcerpt ─────────────────────────────────────────

    [TestMethod]
    public void GetPlainTextExcerpt_NullInput_ReturnsEmpty()
    {
        var result = _renderer.GetPlainTextExcerpt(null!);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetPlainTextExcerpt_EmptyInput_ReturnsEmpty()
    {
        var result = _renderer.GetPlainTextExcerpt("");
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetPlainTextExcerpt_ShortText_ReturnsFullText()
    {
        var result = _renderer.GetPlainTextExcerpt("Hello world");
        Assert.AreEqual("Hello world", result);
    }

    [TestMethod]
    public void GetPlainTextExcerpt_StripsMarkdown()
    {
        var result = _renderer.GetPlainTextExcerpt("**Bold** and *italic*");
        Assert.IsFalse(result.Contains("**"));
        Assert.IsFalse(result.Contains("*"));
        Assert.IsTrue(result.Contains("Bold"));
        Assert.IsTrue(result.Contains("italic"));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_TruncatesLongText()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 100));
        var result = _renderer.GetPlainTextExcerpt(longText, maxLength: 50);

        Assert.IsTrue(result.Length <= 51); // 50 + ellipsis character
        Assert.IsTrue(result.EndsWith('…'));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_TruncatesAtWordBoundary()
    {
        var text = "The quick brown fox jumps over the lazy dog and keeps going further";
        var result = _renderer.GetPlainTextExcerpt(text, maxLength: 30);

        Assert.IsTrue(result.EndsWith('…'));
        // Should not cut in the middle of a word
        Assert.IsFalse(result.TrimEnd('…').EndsWith("an", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_CustomMaxLength_Respected()
    {
        var text = "Short text that fits within limit";
        var result = _renderer.GetPlainTextExcerpt(text, maxLength: 100);

        Assert.AreEqual("Short text that fits within limit", result);
        Assert.IsFalse(result.Contains('…'));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_StripsLinks()
    {
        var result = _renderer.GetPlainTextExcerpt("[DotNetCloud](https://example.com) is great");
        Assert.IsFalse(result.Contains("["));
        Assert.IsFalse(result.Contains("https://"));
        Assert.IsTrue(result.Contains("DotNetCloud"));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_StripsHeaders()
    {
        var result = _renderer.GetPlainTextExcerpt("# Title\n\nContent here");
        Assert.IsFalse(result.Contains("#"));
        Assert.IsTrue(result.Contains("Title"));
        Assert.IsTrue(result.Contains("Content here"));
    }
}
