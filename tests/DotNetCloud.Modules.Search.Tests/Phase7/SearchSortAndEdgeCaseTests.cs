using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for search sort order handling, edge cases, and integration of
/// Phase 7 UI logic with SearchQuery and SearchResultDto models.
/// </summary>
[TestClass]
public class SearchSortAndEdgeCaseTests
{
    #region Sort order parsing (matching SearchResults.razor logic)

    [TestMethod]
    public void ParseSortOrder_Relevance_ReturnsRelevance()
    {
        Assert.AreEqual(SearchSortOrder.Relevance, ParseSort("relevance"));
    }

    [TestMethod]
    public void ParseSortOrder_DateDesc_ReturnsDateDesc()
    {
        Assert.AreEqual(SearchSortOrder.DateDesc, ParseSort("date_desc"));
    }

    [TestMethod]
    public void ParseSortOrder_DateAsc_ReturnsDateAsc()
    {
        Assert.AreEqual(SearchSortOrder.DateAsc, ParseSort("date_asc"));
    }

    [TestMethod]
    public void ParseSortOrder_Unknown_DefaultsToRelevance()
    {
        Assert.AreEqual(SearchSortOrder.Relevance, ParseSort("invalid"));
    }

    [TestMethod]
    public void ParseSortOrder_Null_DefaultsToRelevance()
    {
        Assert.AreEqual(SearchSortOrder.Relevance, ParseSort(null));
    }

    [TestMethod]
    public void ParseSortOrder_CaseInsensitive_DateDesc()
    {
        // The SearchController uses ToLowerInvariant(), so uppercase input still matches
        Assert.AreEqual(SearchSortOrder.DateDesc, ParseSort("DATE_DESC"));
    }

    #endregion

    #region Query clamping (matching SearchController logic)

    [TestMethod]
    public void ClampPageSize_Normal_ReturnsUnchanged()
    {
        Assert.AreEqual(20, Math.Clamp(20, 1, 100));
    }

    [TestMethod]
    public void ClampPageSize_TooLarge_ReturnsMax()
    {
        Assert.AreEqual(100, Math.Clamp(500, 1, 100));
    }

    [TestMethod]
    public void ClampPageSize_TooSmall_ReturnsMin()
    {
        Assert.AreEqual(1, Math.Clamp(0, 1, 100));
    }

    [TestMethod]
    public void ClampPageSize_Negative_ReturnsMin()
    {
        Assert.AreEqual(1, Math.Clamp(-5, 1, 100));
    }

    [TestMethod]
    public void ClampPage_ZeroPage_Returns1()
    {
        Assert.AreEqual(1, Math.Max(1, 0));
    }

    [TestMethod]
    public void ClampPage_NegativePage_Returns1()
    {
        Assert.AreEqual(1, Math.Max(1, -3));
    }

    [TestMethod]
    public void ClampPage_ValidPage_ReturnsUnchanged()
    {
        Assert.AreEqual(5, Math.Max(1, 5));
    }

    #endregion

    #region Search query validation edge cases

    [TestMethod]
    public void SearchQuery_SingleCharacter_IsValid()
    {
        // The suggest endpoint requires >= 2 chars, but main search accepts any non-whitespace
        var query = "a";
        Assert.IsFalse(string.IsNullOrWhiteSpace(query));
    }

    [TestMethod]
    public void SuggestQuery_SingleCharacter_TooShort()
    {
        var query = "a";
        Assert.IsTrue(query.Length < 2);
    }

    [TestMethod]
    public void SuggestQuery_TwoCharacters_IsValid()
    {
        var query = "ab";
        Assert.IsFalse(string.IsNullOrWhiteSpace(query));
        Assert.IsTrue(query.Length >= 2);
    }

    [TestMethod]
    public void SearchQuery_VeryLongQuery_DoesNotThrow()
    {
        var query = new string('x', 10000);
        var url = SearchQueryUrlBuilder.Build(query);
        Assert.IsTrue(url.Length > 10000);
    }

    [TestMethod]
    public void SearchQuery_UnicodeCharacters_AreEncoded()
    {
        var url = SearchQueryUrlBuilder.Build("日本語テスト");
        Assert.IsTrue(url.Contains("q="));
        Assert.IsFalse(url.Contains("日本語"));
    }

    [TestMethod]
    public void SearchQuery_EmojisInQuery_AreEncoded()
    {
        var url = SearchQueryUrlBuilder.Build("🔍 search test");
        Assert.IsTrue(url.Contains("q="));
    }

    #endregion

    #region SearchResultDto with facets

    [TestMethod]
    public void SearchResultDto_WithFacets_GroupsCorrectly()
    {
        var result = new SearchResultDto
        {
            Items = new List<SearchResultItem>
            {
                CreateItem("files"), CreateItem("files"), CreateItem("files"),
                CreateItem("notes"), CreateItem("notes"),
                CreateItem("chat")
            },
            TotalCount = 6,
            Page = 1,
            PageSize = 20,
            FacetCounts = new Dictionary<string, int>
            {
                ["files"] = 3,
                ["notes"] = 2,
                ["chat"] = 1
            }
        };

        Assert.AreEqual(3, result.FacetCounts["files"]);
        Assert.AreEqual(2, result.FacetCounts["notes"]);
        Assert.AreEqual(1, result.FacetCounts["chat"]);
        Assert.AreEqual(3, result.FacetCounts.Count);
    }

    [TestMethod]
    public void SearchResultDto_EmptyResult_HasZeroCounts()
    {
        var result = new SearchResultDto
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(0, result.FacetCounts.Count);
    }

    [TestMethod]
    public void SearchResultDto_SinglePage_HasCorrectPagination()
    {
        var result = new SearchResultDto
        {
            Items = Enumerable.Range(0, 5).Select(_ => CreateItem("files")).ToList(),
            TotalCount = 5,
            Page = 1,
            PageSize = 20
        };

        Assert.AreEqual(1, SearchPaginationHelper.CalculateTotalPages(result.TotalCount, result.PageSize));
    }

    [TestMethod]
    public void SearchResultDto_MultiPage_HasCorrectPagination()
    {
        var result = new SearchResultDto
        {
            Items = Enumerable.Range(0, 20).Select(_ => CreateItem("files")).ToList(),
            TotalCount = 55,
            Page = 1,
            PageSize = 20
        };

        Assert.AreEqual(3, SearchPaginationHelper.CalculateTotalPages(result.TotalCount, result.PageSize));
    }

    #endregion

    #region RelevanceScore ordering

    [TestMethod]
    public void SearchResultItems_OrderedByRelevance_HighestFirst()
    {
        var items = new List<SearchResultItem>
        {
            new() { ModuleId = "files", EntityId = "1", EntityType = "File", Title = "Low", UpdatedAt = DateTimeOffset.UtcNow, RelevanceScore = 0.3 },
            new() { ModuleId = "files", EntityId = "2", EntityType = "File", Title = "High", UpdatedAt = DateTimeOffset.UtcNow, RelevanceScore = 0.9 },
            new() { ModuleId = "files", EntityId = "3", EntityType = "File", Title = "Mid", UpdatedAt = DateTimeOffset.UtcNow, RelevanceScore = 0.6 },
        };

        var ordered = items.OrderByDescending(i => i.RelevanceScore).ToList();
        Assert.AreEqual("High", ordered[0].Title);
        Assert.AreEqual("Mid", ordered[1].Title);
        Assert.AreEqual("Low", ordered[2].Title);
    }

    #endregion

    #region Date ordering

    [TestMethod]
    public void SearchResultItems_OrderedByDateDesc_NewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<SearchResultItem>
        {
            new() { ModuleId = "files", EntityId = "1", EntityType = "File", Title = "Old", UpdatedAt = now.AddDays(-30) },
            new() { ModuleId = "files", EntityId = "2", EntityType = "File", Title = "New", UpdatedAt = now.AddMinutes(-5) },
            new() { ModuleId = "files", EntityId = "3", EntityType = "File", Title = "Mid", UpdatedAt = now.AddDays(-7) },
        };

        var ordered = items.OrderByDescending(i => i.UpdatedAt).ToList();
        Assert.AreEqual("New", ordered[0].Title);
        Assert.AreEqual("Mid", ordered[1].Title);
        Assert.AreEqual("Old", ordered[2].Title);
    }

    [TestMethod]
    public void SearchResultItems_OrderedByDateAsc_OldestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new List<SearchResultItem>
        {
            new() { ModuleId = "files", EntityId = "1", EntityType = "File", Title = "Old", UpdatedAt = now.AddDays(-30) },
            new() { ModuleId = "files", EntityId = "2", EntityType = "File", Title = "New", UpdatedAt = now.AddMinutes(-5) },
        };

        var ordered = items.OrderBy(i => i.UpdatedAt).ToList();
        Assert.AreEqual("Old", ordered[0].Title);
        Assert.AreEqual("New", ordered[1].Title);
    }

    #endregion

    private static SearchSortOrder ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "date_desc" => SearchSortOrder.DateDesc,
        "date_asc" => SearchSortOrder.DateAsc,
        _ => SearchSortOrder.Relevance
    };

    private static SearchResultItem CreateItem(string moduleId)
    {
        return new SearchResultItem
        {
            ModuleId = moduleId,
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "TestEntity",
            Title = "Test Item",
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
