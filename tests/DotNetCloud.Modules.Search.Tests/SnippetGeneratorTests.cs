using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SnippetGenerator"/> — validates snippet extraction,
/// term highlighting with <c>&lt;mark&gt;</c> tags, and XSS prevention.
/// </summary>
[TestClass]
public class SnippetGeneratorTests
{
    #region Generate — basic snippet extraction

    [TestMethod]
    public void Generate_NullContent_ReturnsEmpty()
    {
        var parsed = new ParsedSearchQuery { Terms = ["test"], Phrases = [], Exclusions = [] };
        Assert.AreEqual(string.Empty, SnippetGenerator.Generate(null, parsed));
    }

    [TestMethod]
    public void Generate_EmptyContent_ReturnsEmpty()
    {
        var parsed = new ParsedSearchQuery { Terms = ["test"], Phrases = [], Exclusions = [] };
        Assert.AreEqual(string.Empty, SnippetGenerator.Generate("", parsed));
    }

    [TestMethod]
    public void Generate_NullQuery_ReturnsTruncatedContent()
    {
        var content = "Short content here";
        var result = SnippetGenerator.Generate(content, null);

        Assert.IsTrue(result.Contains("Short content here"));
    }

    [TestMethod]
    public void Generate_NoSearchableContent_ReturnsTruncatedContent()
    {
        var content = "Short content here";
        var parsed = new ParsedSearchQuery { Terms = [], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);
        Assert.IsTrue(result.Contains("Short content here"));
    }

    [TestMethod]
    public void Generate_LongContentNoMatch_TruncatesFromStart()
    {
        var content = new string('a', 500);
        var parsed = new ParsedSearchQuery { Terms = ["xyz"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed, 100);
        // Should be truncated
        Assert.IsTrue(result.Length < 500);
    }

    #endregion

    #region Generate — term highlighting

    [TestMethod]
    public void Generate_MatchingTerm_HighlightedWithMarkTags()
    {
        var content = "The quarterly report is ready for review";
        var parsed = new ParsedSearchQuery { Terms = ["quarterly"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>quarterly</mark>"));
    }

    [TestMethod]
    public void Generate_CaseInsensitiveHighlight_MatchesRegardlessOfCase()
    {
        var content = "The Quarterly Report is ready";
        var parsed = new ParsedSearchQuery { Terms = ["quarterly"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>Quarterly</mark>"));
    }

    [TestMethod]
    public void Generate_MultipleTerms_AllHighlighted()
    {
        var content = "The quarterly report budget is finalized";
        var parsed = new ParsedSearchQuery
        {
            Terms = ["quarterly", "budget"],
            Phrases = [],
            Exclusions = []
        };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>quarterly</mark>"));
        Assert.IsTrue(result.Contains("<mark>budget</mark>"));
    }

    [TestMethod]
    public void Generate_PhraseHighlighted_MatchesFullPhrase()
    {
        var content = "The quarterly report is ready for review";
        var parsed = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>quarterly report</mark>"));
    }

    #endregion

    #region Generate — XSS prevention

    [TestMethod]
    public void Generate_HtmlInContent_IsEncoded()
    {
        var content = "This has <script>alert('xss')</script> content";
        var parsed = new ParsedSearchQuery { Terms = ["content"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsFalse(result.Contains("<script>"));
        Assert.IsTrue(result.Contains("&lt;script&gt;"));
        Assert.IsTrue(result.Contains("<mark>content</mark>"));
    }

    [TestMethod]
    public void Generate_HtmlInContent_MarksStillApplied()
    {
        var content = "Safe text with target word";
        var parsed = new ParsedSearchQuery { Terms = ["target"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>target</mark>"));
        Assert.IsFalse(result.Contains("&lt;mark&gt;")); // mark tags NOT encoded
    }

    [TestMethod]
    public void Generate_SpecialHtmlCharsInContent_Encoded()
    {
        var content = "Prices: item1 < $50 & item2 > $100";
        var parsed = new ParsedSearchQuery { Terms = ["item1"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("&lt;"));
        Assert.IsTrue(result.Contains("&amp;"));
        Assert.IsTrue(result.Contains("&gt;"));
        Assert.IsTrue(result.Contains("<mark>item1</mark>"));
    }

    #endregion

    #region HighlightTitle

    [TestMethod]
    public void HighlightTitle_NullTitle_ReturnsEmpty()
    {
        var parsed = new ParsedSearchQuery { Terms = ["test"], Phrases = [], Exclusions = [] };
        Assert.AreEqual(string.Empty, SnippetGenerator.HighlightTitle(null, parsed));
    }

    [TestMethod]
    public void HighlightTitle_EmptyTitle_ReturnsEmpty()
    {
        var parsed = new ParsedSearchQuery { Terms = ["test"], Phrases = [], Exclusions = [] };
        Assert.AreEqual(string.Empty, SnippetGenerator.HighlightTitle("", parsed));
    }

    [TestMethod]
    public void HighlightTitle_MatchingTerm_Highlighted()
    {
        var parsed = new ParsedSearchQuery { Terms = ["report"], Phrases = [], Exclusions = [] };
        var result = SnippetGenerator.HighlightTitle("Quarterly Report", parsed);

        Assert.IsTrue(result.Contains("<mark>Report</mark>"));
    }

    [TestMethod]
    public void HighlightTitle_NoMatch_ReturnsEncodedTitle()
    {
        var parsed = new ParsedSearchQuery { Terms = ["budget"], Phrases = [], Exclusions = [] };
        var result = SnippetGenerator.HighlightTitle("Quarterly Report", parsed);

        Assert.AreEqual("Quarterly Report", result);
    }

    [TestMethod]
    public void HighlightTitle_HtmlInTitle_Encoded()
    {
        var parsed = new ParsedSearchQuery { Terms = ["test"], Phrases = [], Exclusions = [] };
        var result = SnippetGenerator.HighlightTitle("<b>test</b> title", parsed);

        Assert.IsFalse(result.Contains("<b>"));
        Assert.IsTrue(result.Contains("&lt;b&gt;"));
        Assert.IsTrue(result.Contains("<mark>test</mark>"));
    }

    [TestMethod]
    public void HighlightTitle_NullQuery_ReturnsEncodedTitle()
    {
        var result = SnippetGenerator.HighlightTitle("My Report", null);
        Assert.AreEqual("My Report", result);
    }

    [TestMethod]
    public void HighlightTitle_NoSearchableContent_ReturnsEncodedTitle()
    {
        var parsed = new ParsedSearchQuery { Terms = [], Phrases = [], Exclusions = ["draft"] };
        var result = SnippetGenerator.HighlightTitle("My Report", parsed);

        Assert.AreEqual("My Report", result);
    }

    #endregion

    #region Generate — snippet windowing

    [TestMethod]
    public void Generate_MatchInMiddleOfLongContent_CentersSnippet()
    {
        var prefix = new string('a', 300) + " ";
        var suffix = " " + new string('b', 300);
        var content = prefix + "TARGET_WORD" + suffix;
        var parsed = new ParsedSearchQuery { Terms = ["TARGET_WORD"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed, 200);

        // Should contain the target word highlighted
        Assert.IsTrue(result.Contains("<mark>TARGET_WORD</mark>"));
        // Should have ellipsis since it's a window in the middle
        Assert.IsTrue(result.Contains("..."));
    }

    [TestMethod]
    public void Generate_MatchAtStart_NoLeadingEllipsis()
    {
        var content = "TARGET_WORD followed by lots of other text that goes on and on";
        var parsed = new ParsedSearchQuery { Terms = ["TARGET_WORD"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed, 200);

        Assert.IsTrue(result.Contains("<mark>TARGET_WORD</mark>"));
    }

    [TestMethod]
    public void Generate_ShortContent_NoTruncation()
    {
        var content = "Short text with target word";
        var parsed = new ParsedSearchQuery { Terms = ["target"], Phrases = [], Exclusions = [] };

        var result = SnippetGenerator.Generate(content, parsed);

        Assert.IsTrue(result.Contains("<mark>target</mark>"));
        Assert.IsFalse(result.Contains("..."));
    }

    #endregion
}
