using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchQueryParser"/> — validates parsing of user search input
/// into structured queries with keywords, phrases, filters, and exclusions.
/// </summary>
[TestClass]
public class SearchQueryParserTests
{
    #region Basic keyword parsing

    [TestMethod]
    public void Parse_SingleKeyword_ReturnsSingleTerm()
    {
        var result = SearchQueryParser.Parse("budget");

        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("budget", result.Terms[0]);
        Assert.AreEqual(0, result.Phrases.Count);
        Assert.AreEqual(0, result.Exclusions.Count);
        Assert.IsNull(result.ModuleFilter);
        Assert.IsNull(result.TypeFilter);
    }

    [TestMethod]
    public void Parse_MultipleKeywords_ReturnsAllTerms()
    {
        var result = SearchQueryParser.Parse("quarterly report summary");

        Assert.AreEqual(3, result.Terms.Count);
        Assert.AreEqual("quarterly", result.Terms[0]);
        Assert.AreEqual("report", result.Terms[1]);
        Assert.AreEqual("summary", result.Terms[2]);
    }

    [TestMethod]
    public void Parse_KeywordsWithExtraSpaces_TrimsCorrectly()
    {
        var result = SearchQueryParser.Parse("  quarterly   report   ");

        Assert.AreEqual(2, result.Terms.Count);
        Assert.AreEqual("quarterly", result.Terms[0]);
        Assert.AreEqual("report", result.Terms[1]);
    }

    #endregion

    #region Quoted phrase parsing

    [TestMethod]
    public void Parse_QuotedPhrase_ExtractsPhraseAndKeywords()
    {
        var result = SearchQueryParser.Parse("\"quarterly report\" summary");

        Assert.AreEqual(1, result.Phrases.Count);
        Assert.AreEqual("quarterly report", result.Phrases[0]);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("summary", result.Terms[0]);
    }

    [TestMethod]
    public void Parse_MultipleQuotedPhrases_ExtractsAll()
    {
        var result = SearchQueryParser.Parse("\"quarterly report\" \"annual budget\"");

        Assert.AreEqual(2, result.Phrases.Count);
        Assert.AreEqual("quarterly report", result.Phrases[0]);
        Assert.AreEqual("annual budget", result.Phrases[1]);
        Assert.AreEqual(0, result.Terms.Count);
    }

    [TestMethod]
    public void Parse_EmptyQuotedPhrase_IsIgnored()
    {
        var result = SearchQueryParser.Parse("\"\" budget");

        Assert.AreEqual(0, result.Phrases.Count);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("budget", result.Terms[0]);
    }

    [TestMethod]
    public void Parse_PhraseWithSurroundingKeywords_ParsesBoth()
    {
        var result = SearchQueryParser.Parse("find \"exact match\" here");

        Assert.AreEqual(1, result.Phrases.Count);
        Assert.AreEqual("exact match", result.Phrases[0]);
        Assert.AreEqual(2, result.Terms.Count);
        Assert.AreEqual("find", result.Terms[0]);
        Assert.AreEqual("here", result.Terms[1]);
    }

    #endregion

    #region Module filter (in:module)

    [TestMethod]
    public void Parse_ModuleFilter_ExtractsModuleAndTerms()
    {
        var result = SearchQueryParser.Parse("in:notes budget");

        Assert.AreEqual("notes", result.ModuleFilter);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("budget", result.Terms[0]);
    }

    [TestMethod]
    public void Parse_ModuleFilter_CaseInsensitive()
    {
        var result = SearchQueryParser.Parse("IN:Files report");

        Assert.AreEqual("files", result.ModuleFilter);
        Assert.AreEqual(1, result.Terms.Count);
    }

    [TestMethod]
    public void Parse_ModuleFilterOnly_NoSearchTerms()
    {
        var result = SearchQueryParser.Parse("in:chat");

        Assert.AreEqual("chat", result.ModuleFilter);
        Assert.AreEqual(0, result.Terms.Count);
        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_MultipleModuleFilters_LastOneWins()
    {
        var result = SearchQueryParser.Parse("in:notes in:files budget");

        // Last one wins since we overwrite
        Assert.AreEqual("files", result.ModuleFilter);
        Assert.AreEqual(1, result.Terms.Count);
    }

    #endregion

    #region Type filter (type:value)

    [TestMethod]
    public void Parse_TypeFilter_ExtractsTypeAndTerms()
    {
        var result = SearchQueryParser.Parse("type:pdf annual report");

        Assert.AreEqual("pdf", result.TypeFilter);
        Assert.AreEqual(2, result.Terms.Count);
        Assert.AreEqual("annual", result.Terms[0]);
        Assert.AreEqual("report", result.Terms[1]);
    }

    [TestMethod]
    public void Parse_TypeFilterCaseInsensitive_ExtractsValue()
    {
        var result = SearchQueryParser.Parse("TYPE:Note budget");

        Assert.AreEqual("Note", result.TypeFilter);
        Assert.AreEqual(1, result.Terms.Count);
    }

    [TestMethod]
    public void Parse_BothModuleAndTypeFilter_ExtractsBoth()
    {
        var result = SearchQueryParser.Parse("in:files type:pdf annual");

        Assert.AreEqual("files", result.ModuleFilter);
        Assert.AreEqual("pdf", result.TypeFilter);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("annual", result.Terms[0]);
    }

    #endregion

    #region Exclusion parsing (-term)

    [TestMethod]
    public void Parse_Exclusion_ExtractsExcludedTerm()
    {
        var result = SearchQueryParser.Parse("budget -draft");

        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("budget", result.Terms[0]);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual("draft", result.Exclusions[0]);
    }

    [TestMethod]
    public void Parse_MultipleExclusions_ExtractsAll()
    {
        var result = SearchQueryParser.Parse("report -draft -template -old");

        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual(3, result.Exclusions.Count);
        Assert.AreEqual("draft", result.Exclusions[0]);
        Assert.AreEqual("template", result.Exclusions[1]);
        Assert.AreEqual("old", result.Exclusions[2]);
    }

    [TestMethod]
    public void Parse_ExclusionOnly_HasNoSearchableContent()
    {
        var result = SearchQueryParser.Parse("-draft");

        Assert.AreEqual(0, result.Terms.Count);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_DoubleDash_NotTreatedAsExclusion()
    {
        var result = SearchQueryParser.Parse("budget --flags");

        Assert.AreEqual(2, result.Terms.Count);
        Assert.AreEqual(0, result.Exclusions.Count);
    }

    [TestMethod]
    public void Parse_StandaloneDash_NotTreatedAsExclusion()
    {
        var result = SearchQueryParser.Parse("budget -");

        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual(0, result.Exclusions.Count);
    }

    #endregion

    #region Complex combined queries

    [TestMethod]
    public void Parse_ComplexQuery_AllComponentsExtracted()
    {
        var result = SearchQueryParser.Parse("\"quarterly report\" in:files type:pdf budget -draft -old");

        Assert.AreEqual(1, result.Phrases.Count);
        Assert.AreEqual("quarterly report", result.Phrases[0]);
        Assert.AreEqual("files", result.ModuleFilter);
        Assert.AreEqual("pdf", result.TypeFilter);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("budget", result.Terms[0]);
        Assert.AreEqual(2, result.Exclusions.Count);
        Assert.AreEqual("draft", result.Exclusions[0]);
        Assert.AreEqual("old", result.Exclusions[1]);
        Assert.IsTrue(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_PhraseWithFiltersAndExclusions_AllParsed()
    {
        var result = SearchQueryParser.Parse("\"meeting notes\" in:notes -archived important");

        Assert.AreEqual(1, result.Phrases.Count);
        Assert.AreEqual("meeting notes", result.Phrases[0]);
        Assert.AreEqual("notes", result.ModuleFilter);
        Assert.AreEqual(1, result.Exclusions.Count);
        Assert.AreEqual("archived", result.Exclusions[0]);
        Assert.AreEqual(1, result.Terms.Count);
        Assert.AreEqual("important", result.Terms[0]);
    }

    #endregion

    #region Edge cases

    [TestMethod]
    public void Parse_NullInput_ReturnsEmptyResult()
    {
        var result = SearchQueryParser.Parse(null);

        Assert.AreEqual(0, result.Terms.Count);
        Assert.AreEqual(0, result.Phrases.Count);
        Assert.AreEqual(0, result.Exclusions.Count);
        Assert.IsNull(result.ModuleFilter);
        Assert.IsNull(result.TypeFilter);
        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyResult()
    {
        var result = SearchQueryParser.Parse("");

        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_WhitespaceOnly_ReturnsEmptyResult()
    {
        var result = SearchQueryParser.Parse("   ");

        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void Parse_InColonWithNoValue_TreatedAsTerm()
    {
        var result = SearchQueryParser.Parse("in: budget");

        // "in:" without a value is treated as incomplete, so it's not a module filter
        Assert.IsNull(result.ModuleFilter);
        Assert.AreEqual(2, result.Terms.Count);
    }

    [TestMethod]
    public void Parse_TypeColonWithNoValue_TreatedAsTerm()
    {
        var result = SearchQueryParser.Parse("type: budget");

        Assert.IsNull(result.TypeFilter);
        Assert.AreEqual(2, result.Terms.Count);
    }

    #endregion

    #region HasSearchableContent

    [TestMethod]
    public void HasSearchableContent_WithTerms_ReturnsTrue()
    {
        var result = SearchQueryParser.Parse("budget");
        Assert.IsTrue(result.HasSearchableContent);
    }

    [TestMethod]
    public void HasSearchableContent_WithPhraseOnly_ReturnsTrue()
    {
        var result = SearchQueryParser.Parse("\"quarterly report\"");
        Assert.IsTrue(result.HasSearchableContent);
    }

    [TestMethod]
    public void HasSearchableContent_WithExclusionOnly_ReturnsFalse()
    {
        var result = SearchQueryParser.Parse("-draft");
        Assert.IsFalse(result.HasSearchableContent);
    }

    [TestMethod]
    public void HasSearchableContent_WithFilterOnly_ReturnsFalse()
    {
        var result = SearchQueryParser.Parse("in:notes");
        Assert.IsFalse(result.HasSearchableContent);
    }

    #endregion
}
