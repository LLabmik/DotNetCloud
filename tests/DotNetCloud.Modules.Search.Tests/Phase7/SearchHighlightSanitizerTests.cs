namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for the highlight sanitization and text formatting utilities
/// used in Phase 7 SearchResultCard component.
/// Validates XSS prevention and correct highlight rendering.
/// </summary>
[TestClass]
public class SearchHighlightSanitizerTests
{
    #region SanitizeHighlight — XSS prevention

    [TestMethod]
    public void SanitizeHighlight_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", HighlightSanitizer.SanitizeHighlight(""));
    }

    [TestMethod]
    public void SanitizeHighlight_Null_ReturnsNull()
    {
        Assert.IsNull(HighlightSanitizer.SanitizeHighlight(null!));
    }

    [TestMethod]
    public void SanitizeHighlight_PlainText_ReturnsUnchanged()
    {
        Assert.AreEqual("hello world", HighlightSanitizer.SanitizeHighlight("hello world"));
    }

    [TestMethod]
    public void SanitizeHighlight_WithMarkTags_PreservesMarkTags()
    {
        var input = "hello <mark>world</mark> test";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.AreEqual("hello <mark>world</mark> test", result);
    }

    [TestMethod]
    public void SanitizeHighlight_WithScriptTag_EscapesScriptTag()
    {
        var input = "<script>alert('xss')</script>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsFalse(result.Contains("<script>"));
        Assert.IsTrue(result.Contains("&lt;script&gt;"));
    }

    [TestMethod]
    public void SanitizeHighlight_MixedMarkAndScriptTags_PreservesMarkEscapesScript()
    {
        var input = "<mark>highlighted</mark> <script>evil</script> text";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsTrue(result.Contains("<mark>highlighted</mark>"));
        Assert.IsTrue(result.Contains("&lt;script&gt;"));
        Assert.IsFalse(result.Contains("<script>"));
    }

    [TestMethod]
    public void SanitizeHighlight_OnclickAttribute_EscapesHtml()
    {
        var input = "<div onclick='alert(1)'>test</div>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsFalse(result.Contains("<div"));
        Assert.IsTrue(result.Contains("&lt;div"));
    }

    [TestMethod]
    public void SanitizeHighlight_AnchorTag_EscapesHtml()
    {
        var input = "<a href='javascript:void(0)'>link</a>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsFalse(result.Contains("<a "));
    }

    [TestMethod]
    public void SanitizeHighlight_ImgTag_EscapesHtml()
    {
        var input = "<img src=x onerror=alert(1)>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsFalse(result.Contains("<img"));
    }

    [TestMethod]
    public void SanitizeHighlight_MultipleMarkTags_PreservesAll()
    {
        var input = "<mark>first</mark> middle <mark>second</mark>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.AreEqual("<mark>first</mark> middle <mark>second</mark>", result);
    }

    [TestMethod]
    public void SanitizeHighlight_NestedHtmlInMark_EscapesInner()
    {
        var input = "<mark><b>bold</b></mark>";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsTrue(result.Contains("<mark>&lt;b&gt;bold&lt;/b&gt;</mark>"));
    }

    [TestMethod]
    public void SanitizeHighlight_AmpersandAndAngleBrackets_EscapesCorrectly()
    {
        var input = "A & B < C > D";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.AreEqual("A &amp; B &lt; C &gt; D", result);
    }

    [TestMethod]
    public void SanitizeHighlight_QuotesInText_EscapesCorrectly()
    {
        var input = "He said \"hello\" & she said 'hi'";
        var result = HighlightSanitizer.SanitizeHighlight(input);
        Assert.IsTrue(result.Contains("&amp;"));
        Assert.IsTrue(result.Contains("&quot;"));
    }

    #endregion

    #region StripHighlight

    [TestMethod]
    public void StripHighlight_RemovesMarkTags()
    {
        Assert.AreEqual("hello world", HighlightSanitizer.StripHighlight("hello <mark>world</mark>"));
    }

    [TestMethod]
    public void StripHighlight_PlainText_ReturnsUnchanged()
    {
        Assert.AreEqual("hello", HighlightSanitizer.StripHighlight("hello"));
    }

    [TestMethod]
    public void StripHighlight_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", HighlightSanitizer.StripHighlight(""));
    }

    [TestMethod]
    public void StripHighlight_MultipleMarkTags_RemovesAll()
    {
        var input = "<mark>first</mark> <mark>second</mark>";
        Assert.AreEqual("first second", HighlightSanitizer.StripHighlight(input));
    }

    #endregion
}

/// <summary>
/// Testable extraction of the highlight sanitization logic from SearchResultCard.razor.
/// </summary>
public static class HighlightSanitizer
{
    /// <summary>
    /// Sanitizes highlight markup. Only allows &lt;mark&gt; tags, strips everything else.
    /// </summary>
    public static string SanitizeHighlight(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        var result = html;
        // Replace <mark> tags with placeholders
        result = result.Replace("<mark>", "\x01MARK_OPEN\x01").Replace("</mark>", "\x01MARK_CLOSE\x01");
        // Escape all remaining HTML
        result = System.Net.WebUtility.HtmlEncode(result);
        // Restore <mark> tags
        result = result.Replace("\x01MARK_OPEN\x01", "<mark>").Replace("\x01MARK_CLOSE\x01", "</mark>");
        return result;
    }

    /// <summary>Strips HTML highlight tags for plain-text display.</summary>
    public static string StripHighlight(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("<mark>", "").Replace("</mark>", "");
    }
}
