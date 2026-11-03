using DotNetCloud.Modules.Chat.UI;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="MessageList"/>'s Markdown-to-HTML renderer.
/// </summary>
/// <remarks>
/// Regression guard for the "chat loses Markdown formatting" bug: the renderer used to
/// HTML-encode the entire message before splitting on line breaks, and <c>HtmlEncoder</c>
/// turns <c>\n</c> into <c>&amp;#xA;</c>. Every multi-line message therefore collapsed into a
/// single line and lost all block-level formatting (paragraphs, lists, headings, quotes,
/// code fences), because the line splitter never saw a newline.
/// </remarks>
[TestClass]
public class MessageListMarkdownTests
{
    [TestMethod]
    public void RenderMarkdown_InlineFormatting_ProducesHtmlElements()
    {
        var html = Probe.Render("**bold** *italic* ***both*** ~~gone~~ `code`");

        StringAssert.Contains(html, "<strong>bold</strong>");
        StringAssert.Contains(html, "<em>italic</em>");
        StringAssert.Contains(html, "<strong><em>both</em></strong>");
        StringAssert.Contains(html, "<s>gone</s>");
        StringAssert.Contains(html, "<code>code</code>");
    }

    [TestMethod]
    public void RenderMarkdown_Link_ProducesAnchor()
    {
        var html = Probe.Render("[docs](https://example.com/page)");

        StringAssert.Contains(html, "<a href=\"https://example.com/page\"");
        StringAssert.Contains(html, ">docs</a>");
    }

    [TestMethod]
    public void RenderMarkdown_MultiLineText_PreservesLineBreaks()
    {
        var html = Probe.Render("first paragraph\nsecond paragraph");

        Assert.AreEqual("first paragraph<br />second paragraph", html);
    }

    [TestMethod]
    public void RenderMarkdown_CrLfLineEndings_AreTreatedAsLineBreaks()
    {
        var html = Probe.Render("first\r\nsecond\rthird");

        Assert.AreEqual("first<br />second<br />third", html);
    }

    [TestMethod]
    public void RenderMarkdown_UnorderedListAcrossLines_ProducesListItems()
    {
        var html = Probe.Render("- one\n- two\n- three");

        Assert.AreEqual("<ul><li>one</li><li>two</li><li>three</li></ul>", html);
    }

    [TestMethod]
    public void RenderMarkdown_OrderedListAcrossLines_ProducesOrderedList()
    {
        var html = Probe.Render("1. one\n2. two");

        Assert.AreEqual("<ol><li>one</li><li>two</li></ol>", html);
    }

    [TestMethod]
    public void RenderMarkdown_ListItemsWithInlineFormatting_ProduceFormattedItems()
    {
        var html = Probe.Render("- **one**\n- *two*");

        Assert.AreEqual("<ul><li><strong>one</strong></li><li><em>two</em></li></ul>", html);
    }

    [TestMethod]
    public void RenderMarkdown_HeadingOnLaterLine_ProducesHeadingElement()
    {
        var html = Probe.Render("intro\n## Heading");

        StringAssert.Contains(html, "intro<br />");
        StringAssert.Contains(html, "<h2>Heading</h2>");
    }

    [TestMethod]
    public void RenderMarkdown_BlockquoteLines_ProduceBlockquotes()
    {
        var html = Probe.Render("> first\n> second");

        Assert.AreEqual("<blockquote>first</blockquote><blockquote>second</blockquote>", html);
    }

    [TestMethod]
    public void RenderMarkdown_CodeFence_ProducesPreElementWithEscapedCode()
    {
        var html = Probe.Render("before\n```\n<b>bold</b> & text\n```\nafter");

        StringAssert.Contains(html, "<pre><code>&lt;b&gt;bold&lt;/b&gt; &amp; text</code></pre>");
        StringAssert.Contains(html, "before<br />");
        StringAssert.Contains(html, "</pre>after");
    }

    [TestMethod]
    public void RenderMarkdown_HorizontalRule_ProducesHrElement()
    {
        var html = Probe.Render("above\n---\nbelow");

        StringAssert.Contains(html, "<hr />");
    }

    [TestMethod]
    public void RenderMarkdown_RawHtml_IsEncodedNotExecuted()
    {
        var html = Probe.Render("<script>alert('x')</script>");

        Assert.IsFalse(html.Contains("<script", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void RenderMarkdown_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, Probe.Render(string.Empty));
        Assert.AreEqual(string.Empty, Probe.Render("   \n  "));
    }

    /// <summary>Exposes the protected renderer for testing.</summary>
    private sealed class Probe : MessageList
    {
        public static string Render(string content) => RenderMarkdown(content).Value;
    }
}
