using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Tests for the Markdown rendering pipeline including sanitization,
/// XSS prevention, and correct Markdown-to-HTML conversion.
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

    // ─── Basic Markdown Rendering ────────────────────────────────────

    [TestMethod]
    public void RenderToHtml_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _renderer.RenderToHtml(string.Empty));
    }

    [TestMethod]
    public void RenderToHtml_NullString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _renderer.RenderToHtml(null!));
    }

    [TestMethod]
    public void RenderToHtml_PlainText_WrapsInParagraph()
    {
        var result = _renderer.RenderToHtml("Hello world");
        Assert.IsTrue(result.Contains("<p>Hello world</p>"));
    }

    [TestMethod]
    public void RenderToHtml_Headings_RendersCorrectly()
    {
        var result = _renderer.RenderToHtml("# Heading 1\n## Heading 2\n### Heading 3");
        // Markdig UseAdvancedExtensions adds id attributes to headings
        Assert.IsTrue(result.Contains("<h1") && result.Contains(">Heading 1</h1>"));
        Assert.IsTrue(result.Contains("<h2") && result.Contains(">Heading 2</h2>"));
        Assert.IsTrue(result.Contains("<h3") && result.Contains(">Heading 3</h3>"));
    }

    [TestMethod]
    public void RenderToHtml_BoldAndItalic_RendersCorrectly()
    {
        var result = _renderer.RenderToHtml("**bold** and *italic*");
        Assert.IsTrue(result.Contains("<strong>bold</strong>"));
        Assert.IsTrue(result.Contains("<em>italic</em>"));
    }

    [TestMethod]
    public void RenderToHtml_Links_RendersWithHref()
    {
        var result = _renderer.RenderToHtml("[DotNetCloud](https://dotnetcloud.net)");
        Assert.IsTrue(result.Contains("<a"));
        Assert.IsTrue(result.Contains("href=\"https://dotnetcloud.net\""));
        Assert.IsTrue(result.Contains("DotNetCloud"));
    }

    [TestMethod]
    public void RenderToHtml_Images_RendersImgTag()
    {
        var result = _renderer.RenderToHtml("![logo](https://example.com/logo.png)");
        Assert.IsTrue(result.Contains("<img"));
        Assert.IsTrue(result.Contains("src=\"https://example.com/logo.png\""));
        Assert.IsTrue(result.Contains("alt=\"logo\""));
    }

    [TestMethod]
    public void RenderToHtml_CodeBlock_RendersPreCode()
    {
        var result = _renderer.RenderToHtml("```csharp\nvar x = 1;\n```");
        Assert.IsTrue(result.Contains("<pre>"));
        Assert.IsTrue(result.Contains("<code"));
        Assert.IsTrue(result.Contains("var x = 1;"));
    }

    [TestMethod]
    public void RenderToHtml_InlineCode_RendersCodeTag()
    {
        var result = _renderer.RenderToHtml("Use `Console.WriteLine()` to print");
        Assert.IsTrue(result.Contains("<code>Console.WriteLine()</code>"));
    }

    [TestMethod]
    public void RenderToHtml_UnorderedList_RendersCorrectly()
    {
        var result = _renderer.RenderToHtml("- Item A\n- Item B\n- Item C");
        Assert.IsTrue(result.Contains("<ul>"));
        Assert.IsTrue(result.Contains("<li>Item A</li>"));
        Assert.IsTrue(result.Contains("<li>Item B</li>"));
        Assert.IsTrue(result.Contains("<li>Item C</li>"));
    }

    [TestMethod]
    public void RenderToHtml_OrderedList_RendersCorrectly()
    {
        var result = _renderer.RenderToHtml("1. First\n2. Second\n3. Third");
        Assert.IsTrue(result.Contains("<ol>"));
        Assert.IsTrue(result.Contains("<li>First</li>"));
    }

    [TestMethod]
    public void RenderToHtml_Blockquote_RendersCorrectly()
    {
        var result = _renderer.RenderToHtml("> This is quoted text");
        Assert.IsTrue(result.Contains("<blockquote>"));
        Assert.IsTrue(result.Contains("This is quoted text"));
    }

    [TestMethod]
    public void RenderToHtml_Table_RendersCorrectly()
    {
        var markdown = "| Name | Age |\n|------|-----|\n| Alice | 30 |";
        var result = _renderer.RenderToHtml(markdown);
        Assert.IsTrue(result.Contains("<table>"));
        Assert.IsTrue(result.Contains("<th>"));
        Assert.IsTrue(result.Contains("<td>"));
        Assert.IsTrue(result.Contains("Alice"));
    }

    [TestMethod]
    public void RenderToHtml_TaskList_RendersCheckboxes()
    {
        var markdown = "- [ ] Pending\n- [x] Done";
        var result = _renderer.RenderToHtml(markdown);
        Assert.IsTrue(result.Contains("<input"));
        Assert.IsTrue(result.Contains("type=\"checkbox\""));
    }

    [TestMethod]
    public void RenderToHtml_HorizontalRule_RendersHr()
    {
        var result = _renderer.RenderToHtml("---");
        Assert.IsTrue(result.Contains("<hr"));
    }

    [TestMethod]
    public void RenderToHtml_Strikethrough_RendersDelTag()
    {
        var result = _renderer.RenderToHtml("~~deleted~~");
        Assert.IsTrue(result.Contains("<del>deleted</del>"));
    }

    // ─── XSS Sanitization ───────────────────────────────────────────

    [TestMethod]
    public void RenderToHtml_ScriptTag_IsStripped()
    {
        var result = _renderer.RenderToHtml("<script>alert('xss')</script>");
        Assert.IsFalse(result.Contains("<script"), "Script tags must be stripped");
        Assert.IsFalse(result.Contains("alert("), "Script content must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_ImgOnError_EventHandlerStripped()
    {
        // When passed as raw HTML in Markdown, Markdig HTML-encodes malformed tags.
        // When passed as proper HTML, the sanitizer strips the event handler.
        var result = _renderer.SanitizeHtml("<img src=x onerror=alert('xss')>");
        Assert.IsFalse(result.Contains("onerror"), "Event handlers must be stripped from HTML");
    }

    [TestMethod]
    public void RenderToHtml_IframeTag_IsStripped()
    {
        var result = _renderer.RenderToHtml("<iframe src='https://evil.example.com'></iframe>");
        Assert.IsFalse(result.Contains("<iframe"), "Iframe tags must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_JavascriptUrl_IsStripped()
    {
        var result = _renderer.RenderToHtml("[click me](javascript:alert('xss'))");
        Assert.IsFalse(result.Contains("javascript:"), "javascript: URLs must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_OnMouseOverHandler_IsStripped()
    {
        var result = _renderer.RenderToHtml("<div onmouseover=\"alert('xss')\">hover</div>");
        Assert.IsFalse(result.Contains("onmouseover"), "Event handlers must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_OnClickHandler_IsStripped()
    {
        var result = _renderer.RenderToHtml("<a href='#' onclick=\"alert('xss')\">click</a>");
        Assert.IsFalse(result.Contains("onclick"), "onclick must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_StyleTagWithExpression_IsStripped()
    {
        var result = _renderer.RenderToHtml("<style>body { background: url('javascript:alert(1)') }</style>");
        Assert.IsFalse(result.Contains("<style"), "Style tags must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_FormTag_IsStripped()
    {
        var result = _renderer.RenderToHtml("<form action='https://evil.com'><input type='submit'></form>");
        Assert.IsFalse(result.Contains("<form"), "Form tags must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_ObjectEmbed_IsStripped()
    {
        var result = _renderer.RenderToHtml("<object data='evil.swf'></object><embed src='evil.swf'>");
        Assert.IsFalse(result.Contains("<object"), "Object tags must be stripped");
        Assert.IsFalse(result.Contains("<embed"), "Embed tags must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_SvgOnload_IsStripped()
    {
        var result = _renderer.RenderToHtml("<svg onload=\"alert('xss')\"><circle r='10'/></svg>");
        Assert.IsFalse(result.Contains("onload"), "onload event must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_DataUrl_IsStripped()
    {
        var result = _renderer.RenderToHtml("<a href=\"data:text/html,<script>alert('xss')</script>\">click</a>");
        Assert.IsFalse(result.Contains("data:text/html"), "data: URLs must be stripped from links");
    }

    [TestMethod]
    public void RenderToHtml_MetaRefresh_IsStripped()
    {
        var result = _renderer.RenderToHtml("<meta http-equiv='refresh' content='0;url=https://evil.com'>");
        Assert.IsFalse(result.Contains("<meta"), "Meta tags must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_VbscriptUrl_IsStripped()
    {
        var result = _renderer.RenderToHtml("[click](vbscript:MsgBox('xss'))");
        Assert.IsFalse(result.Contains("vbscript:"), "vbscript: URLs must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_EncodedJavascript_IsStripped()
    {
        // URL-encoded javascript:
        var result = _renderer.RenderToHtml("<a href=\"&#106;avascript:alert('xss')\">click</a>");
        Assert.IsFalse(result.Contains("javascript:"), "Encoded javascript: URLs must be stripped");
    }

    [TestMethod]
    public void RenderToHtml_NestedScriptInMarkdown_IsStripped()
    {
        var markdown = "# Safe Heading\n\nSafe paragraph.\n\n<script>document.cookie</script>\n\n**More safe text**";
        var result = _renderer.RenderToHtml(markdown);
        Assert.IsTrue(result.Contains("Safe Heading</h1>"), "Safe heading preserved");
        Assert.IsTrue(result.Contains("<strong>More safe text</strong>"), "Safe content preserved");
        Assert.IsFalse(result.Contains("<script"), "Embedded scripts stripped");
    }

    // ─── SanitizeHtml ────────────────────────────────────────────────

    [TestMethod]
    public void SanitizeHtml_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _renderer.SanitizeHtml(string.Empty));
    }

    [TestMethod]
    public void SanitizeHtml_SafeHtml_PassesThrough()
    {
        var html = "<p>Hello <strong>world</strong></p>";
        var result = _renderer.SanitizeHtml(html);
        Assert.IsTrue(result.Contains("<p>"));
        Assert.IsTrue(result.Contains("<strong>world</strong>"));
    }

    [TestMethod]
    public void SanitizeHtml_ScriptTag_IsStripped()
    {
        var result = _renderer.SanitizeHtml("<p>Safe</p><script>alert('xss')</script>");
        Assert.IsTrue(result.Contains("<p>Safe</p>"));
        Assert.IsFalse(result.Contains("<script"));
    }

    // ─── GetPlainTextExcerpt ─────────────────────────────────────────

    [TestMethod]
    public void GetPlainTextExcerpt_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, _renderer.GetPlainTextExcerpt(string.Empty));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_ShortContent_ReturnsFullText()
    {
        var result = _renderer.GetPlainTextExcerpt("# Hello\n\nThis is a test.");
        Assert.IsTrue(result.Contains("Hello"));
        Assert.IsTrue(result.Contains("This is a test."));
        Assert.IsFalse(result.Contains("#"), "Markdown syntax should be stripped");
    }

    [TestMethod]
    public void GetPlainTextExcerpt_LongContent_TruncatesAtWordBoundary()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 100));
        var result = _renderer.GetPlainTextExcerpt(longText, 50);
        Assert.IsTrue(result.Length <= 52, $"Excerpt should be truncated (got {result.Length} chars)"); // +2 for ellipsis
        Assert.IsTrue(result.EndsWith("…"), "Truncated excerpt should end with ellipsis");
    }

    [TestMethod]
    public void GetPlainTextExcerpt_StripsHtmlFromMarkdown()
    {
        var result = _renderer.GetPlainTextExcerpt("**bold** and *italic* and [link](http://example.com)");
        Assert.IsFalse(result.Contains("**"));
        Assert.IsFalse(result.Contains("*"));
        Assert.IsFalse(result.Contains("["));
        Assert.IsTrue(result.Contains("bold"));
        Assert.IsTrue(result.Contains("italic"));
        Assert.IsTrue(result.Contains("link"));
    }

    [TestMethod]
    public void GetPlainTextExcerpt_ExactMaxLength_NoEllipsis()
    {
        var text = "Short";
        var result = _renderer.GetPlainTextExcerpt(text, 200);
        Assert.AreEqual("Short", result);
        Assert.IsFalse(result.Contains("…"));
    }

    // ─── Safe content preservation ───────────────────────────────────

    [TestMethod]
    public void RenderToHtml_MixedContent_PreservesValidElements()
    {
        var markdown = @"# Project Notes

## Overview

This project uses **DotNetCloud** for hosting.

### Links
- [Homepage](https://dotnetcloud.net)
- [Docs](https://docs.example.com)

### Code Example
```csharp
public class Hello { }
```

> Important: deploy by Friday

| Feature | Status |
|---------|--------|
| Auth    | Done   |
| Files   | WIP    |

---

*Last updated: 2026-03-23*";

        var result = _renderer.RenderToHtml(markdown);

        // Verify all safe elements are present
        Assert.IsTrue(result.Contains("Project Notes</h1>"));
        Assert.IsTrue(result.Contains("Overview</h2>"));
        Assert.IsTrue(result.Contains("<strong>DotNetCloud</strong>"));
        Assert.IsTrue(result.Contains("<a"));
        Assert.IsTrue(result.Contains("<code"));
        Assert.IsTrue(result.Contains("<blockquote>"));
        Assert.IsTrue(result.Contains("<table>"));
        Assert.IsTrue(result.Contains("<hr"));
        Assert.IsTrue(result.Contains("<em>Last updated"));
    }
}
