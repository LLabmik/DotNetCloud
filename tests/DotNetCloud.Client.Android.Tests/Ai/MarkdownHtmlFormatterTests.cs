using DotNetCloud.Client.Android.Ai;

namespace DotNetCloud.Client.Android.Tests.Ai;

[TestClass]
public sealed class MarkdownHtmlFormatterTests
{
    // ── ToHtmlDocument ─────────────────────────────────────────────

    [TestMethod]
    public void ToHtmlDocument_NullOrEmpty_ReturnsCompleteDocument()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument(null);
        StringAssert.Contains(html, "<html");
        StringAssert.Contains(html, "<body>");

        var empty = MarkdownHtmlFormatter.ToHtmlDocument(string.Empty);
        StringAssert.Contains(empty, "<html");
        StringAssert.Contains(empty, "<body>");
    }

    [TestMethod]
    public void ToHtmlDocument_Heading_ProducesH1()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("# Hello");
        StringAssert.Contains(html, "<h1");
        StringAssert.Contains(html, "Hello");
    }

    [TestMethod]
    public void ToHtmlDocument_UnorderedList_ProducesListItems()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("- one\n- two");
        StringAssert.Contains(html, "<li>one</li>");
        StringAssert.Contains(html, "<li>two</li>");
    }

    [TestMethod]
    public void ToHtmlDocument_Table_ProducesTable()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("| A | B |\n|---|---|\n| 1 | 2 |");
        StringAssert.Contains(html, "<table");
        StringAssert.Contains(html, "<th");
        StringAssert.Contains(html, "<td>1</td>");
    }

    [TestMethod]
    public void ToHtmlDocument_FencedCode_ProducesCodeBlock()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("```csharp\nvar x = 1;\n```");
        StringAssert.Contains(html, "<pre><code");
        StringAssert.Contains(html, "var x = 1;");
    }

    [TestMethod]
    public void ToHtmlDocument_InlineMarkdown_ProducesEmphasis()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("a **bold** word");
        StringAssert.Contains(html, "<strong>bold</strong>");
    }

    [TestMethod]
    public void ToHtmlDocument_RawHtml_IsEscapedNotExecuted()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("<script>alert(1)</script>");
        Assert.IsFalse(html.Contains("<script>", StringComparison.Ordinal));
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void ToHtmlDocument_IncludesContentSecurityPolicy()
    {
        var html = MarkdownHtmlFormatter.ToHtmlDocument("# Hi");
        StringAssert.Contains(html, "default-src 'none'");
    }

    // ── NeedsHtmlRendering ─────────────────────────────────────────

    [TestMethod]
    public void NeedsHtmlRendering_BlockConstructs_ReturnsTrue()
    {
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("# heading"));
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("- item"));
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("1. item"));
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("> quote"));
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("| A | B |"));
        Assert.IsTrue(MarkdownHtmlFormatter.NeedsHtmlRendering("```\ncode\n```"));
    }

    [TestMethod]
    public void NeedsHtmlRendering_InlineOnly_ReturnsFalse()
    {
        Assert.IsFalse(MarkdownHtmlFormatter.NeedsHtmlRendering("plain text"));
        Assert.IsFalse(MarkdownHtmlFormatter.NeedsHtmlRendering("**bold** and `code` and [a](https://x.com)"));
        Assert.IsFalse(MarkdownHtmlFormatter.NeedsHtmlRendering(string.Empty));
        Assert.IsFalse(MarkdownHtmlFormatter.NeedsHtmlRendering(null));
    }
}
