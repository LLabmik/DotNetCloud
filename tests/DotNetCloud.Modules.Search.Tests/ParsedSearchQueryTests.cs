using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="ParsedSearchQuery"/> — validates provider-specific query string generation
/// (PostgreSQL tsquery, SQL Server CONTAINS, MariaDB BOOLEAN MODE).
/// </summary>
[TestClass]
public class ParsedSearchQueryTests
{
    #region ToPlainTextQuery

    [TestMethod]
    public void ToPlainTextQuery_TermsOnly_JoinsWithSpace()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["quarterly", "report"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("quarterly report", query.ToPlainTextQuery());
    }

    [TestMethod]
    public void ToPlainTextQuery_WithPhrases_QuotesPhrases()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        Assert.AreEqual("budget \"quarterly report\"", query.ToPlainTextQuery());
    }

    [TestMethod]
    public void ToPlainTextQuery_Empty_ReturnsEmpty()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("", query.ToPlainTextQuery());
    }

    #endregion

    #region ToPostgreSqlTsQuery

    [TestMethod]
    public void ToPostgreSqlTsQuery_SingleTerm_ReturnsSanitizedTerm()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("budget", query.ToPostgreSqlTsQuery());
    }

    [TestMethod]
    public void ToPostgreSqlTsQuery_MultipleTerms_AndJoined()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["quarterly", "report"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("quarterly & report", query.ToPostgreSqlTsQuery());
    }

    [TestMethod]
    public void ToPostgreSqlTsQuery_Phrase_UsesProximityOperator()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        Assert.AreEqual("quarterly <-> report", query.ToPostgreSqlTsQuery());
    }

    [TestMethod]
    public void ToPostgreSqlTsQuery_WithExclusion_UsesBangOperator()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = ["draft"]
        };

        Assert.AreEqual("budget & !draft", query.ToPostgreSqlTsQuery());
    }

    [TestMethod]
    public void ToPostgreSqlTsQuery_ComplexQuery_AllOperators()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["annual"],
            Phrases = ["quarterly report"],
            Exclusions = ["draft"]
        };

        Assert.AreEqual("annual & quarterly <-> report & !draft", query.ToPostgreSqlTsQuery());
    }

    [TestMethod]
    public void ToPostgreSqlTsQuery_SpecialCharacters_Sanitized()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["test!@#$%^&*()"],
            Phrases = [],
            Exclusions = []
        };

        var result = query.ToPostgreSqlTsQuery();
        Assert.AreEqual("test", result);
    }

    #endregion

    #region ToSqlServerContainsQuery

    [TestMethod]
    public void ToSqlServerContainsQuery_SingleTerm_DoubleQuoted()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("\"budget\"", query.ToSqlServerContainsQuery());
    }

    [TestMethod]
    public void ToSqlServerContainsQuery_MultipleTerms_AndJoined()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["quarterly", "report"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("\"quarterly\" AND \"report\"", query.ToSqlServerContainsQuery());
    }

    [TestMethod]
    public void ToSqlServerContainsQuery_WithExclusion_UsesAndNot()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = ["draft"]
        };

        Assert.AreEqual("(\"budget\") AND NOT (\"draft\")", query.ToSqlServerContainsQuery());
    }

    [TestMethod]
    public void ToSqlServerContainsQuery_WithPhrase_QuotedPhrase()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        Assert.AreEqual("\"quarterly report\"", query.ToSqlServerContainsQuery());
    }

    #endregion

    #region ToMariaDbBooleanQuery

    [TestMethod]
    public void ToMariaDbBooleanQuery_SingleTerm_PlusPrefixed()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("+budget", query.ToMariaDbBooleanQuery());
    }

    [TestMethod]
    public void ToMariaDbBooleanQuery_MultipleTerms_EachPlusPrefixed()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["quarterly", "report"],
            Phrases = [],
            Exclusions = []
        };

        Assert.AreEqual("+quarterly +report", query.ToMariaDbBooleanQuery());
    }

    [TestMethod]
    public void ToMariaDbBooleanQuery_WithPhrase_PlusQuotedPhrase()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        Assert.AreEqual("+\"quarterly report\"", query.ToMariaDbBooleanQuery());
    }

    [TestMethod]
    public void ToMariaDbBooleanQuery_WithExclusion_MinusPrefixed()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = ["draft"]
        };

        Assert.AreEqual("+budget -draft", query.ToMariaDbBooleanQuery());
    }

    [TestMethod]
    public void ToMariaDbBooleanQuery_ComplexQuery_AllPrefixes()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["annual"],
            Phrases = ["quarterly report"],
            Exclusions = ["draft", "old"]
        };

        Assert.AreEqual("+annual +\"quarterly report\" -draft -old", query.ToMariaDbBooleanQuery());
    }

    [TestMethod]
    public void ToMariaDbBooleanQuery_SpecialCharacters_Sanitized()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["test+~*()"],
            Phrases = [],
            Exclusions = []
        };

        var result = query.ToMariaDbBooleanQuery();
        Assert.AreEqual("+test", result);
    }

    #endregion

    #region HasSearchableContent

    [TestMethod]
    public void HasSearchableContent_EmptyQuery_ReturnsFalse()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = [],
            Exclusions = ["draft"]
        };

        Assert.IsFalse(query.HasSearchableContent);
    }

    [TestMethod]
    public void HasSearchableContent_WithTerms_ReturnsTrue()
    {
        var query = new ParsedSearchQuery
        {
            Terms = ["budget"],
            Phrases = [],
            Exclusions = []
        };

        Assert.IsTrue(query.HasSearchableContent);
    }

    [TestMethod]
    public void HasSearchableContent_WithPhrasesOnly_ReturnsTrue()
    {
        var query = new ParsedSearchQuery
        {
            Terms = [],
            Phrases = ["quarterly report"],
            Exclusions = []
        };

        Assert.IsTrue(query.HasSearchableContent);
    }

    #endregion
}
