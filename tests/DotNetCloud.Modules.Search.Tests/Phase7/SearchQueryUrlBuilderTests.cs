using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Modules.Search.Tests.Phase7;

/// <summary>
/// Tests for search query URL building and SearchResults page-level logic.
/// Validates URL construction, pagination calculations, and facet filtering
/// used in the SearchResults.razor page and DotNetCloudApiClient search methods.
/// </summary>
[TestClass]
public class SearchQueryUrlBuilderTests
{
    #region URL construction for search API

    [TestMethod]
    public void BuildSearchUrl_BasicQuery_ReturnsCorrectUrl()
    {
        var url = SearchQueryUrlBuilder.Build("hello world");
        Assert.AreEqual("api/v1/search?q=hello%20world&page=1&pageSize=20&sort=relevance", url);
    }

    [TestMethod]
    public void BuildSearchUrl_WithModuleFilter_IncludesModule()
    {
        var url = SearchQueryUrlBuilder.Build("test", module: "notes");
        Assert.IsTrue(url.Contains("&module=notes"));
    }

    [TestMethod]
    public void BuildSearchUrl_WithTypeFilter_IncludesType()
    {
        var url = SearchQueryUrlBuilder.Build("test", type: "Note");
        Assert.IsTrue(url.Contains("&type=Note"));
    }

    [TestMethod]
    public void BuildSearchUrl_WithPagination_IncludesPageAndSize()
    {
        var url = SearchQueryUrlBuilder.Build("test", page: 3, pageSize: 50);
        Assert.IsTrue(url.Contains("page=3"));
        Assert.IsTrue(url.Contains("pageSize=50"));
    }

    [TestMethod]
    public void BuildSearchUrl_WithSort_IncludesSortOrder()
    {
        var url = SearchQueryUrlBuilder.Build("test", sort: "date_desc");
        Assert.IsTrue(url.Contains("sort=date_desc"));
    }

    [TestMethod]
    public void BuildSearchUrl_SpecialCharacters_AreEncoded()
    {
        var url = SearchQueryUrlBuilder.Build("hello & goodbye <test>");
        Assert.IsTrue(url.Contains("q=hello%20%26%20goodbye%20%3Ctest%3E"));
    }

    [TestMethod]
    public void BuildSearchUrl_EmptyModuleFilter_ExcludesModule()
    {
        var url = SearchQueryUrlBuilder.Build("test", module: "");
        Assert.IsFalse(url.Contains("&module="));
    }

    [TestMethod]
    public void BuildSearchUrl_NullModuleFilter_ExcludesModule()
    {
        var url = SearchQueryUrlBuilder.Build("test", module: null);
        Assert.IsFalse(url.Contains("&module="));
    }

    [TestMethod]
    public void BuildSearchUrl_WhitespaceModuleFilter_ExcludesModule()
    {
        var url = SearchQueryUrlBuilder.Build("test", module: "   ");
        Assert.IsFalse(url.Contains("&module="));
    }

    #endregion

    #region Suggest URL

    [TestMethod]
    public void BuildSuggestUrl_ReturnsCorrectUrl()
    {
        var url = SearchQueryUrlBuilder.BuildSuggest("hel");
        Assert.AreEqual("api/v1/search/suggest?q=hel", url);
    }

    [TestMethod]
    public void BuildSuggestUrl_SpecialCharacters_AreEncoded()
    {
        var url = SearchQueryUrlBuilder.BuildSuggest("test & query");
        Assert.AreEqual("api/v1/search/suggest?q=test%20%26%20query", url);
    }

    #endregion

    #region Pagination calculations

    [TestMethod]
    public void TotalPages_ZeroTotalCount_Returns1()
    {
        Assert.AreEqual(1, SearchPaginationHelper.CalculateTotalPages(0, 20));
    }

    [TestMethod]
    public void TotalPages_ExactlyOnePage_Returns1()
    {
        Assert.AreEqual(1, SearchPaginationHelper.CalculateTotalPages(20, 20));
    }

    [TestMethod]
    public void TotalPages_OneOverPage_Returns2()
    {
        Assert.AreEqual(2, SearchPaginationHelper.CalculateTotalPages(21, 20));
    }

    [TestMethod]
    public void TotalPages_ExactlyTwoPages_Returns2()
    {
        Assert.AreEqual(2, SearchPaginationHelper.CalculateTotalPages(40, 20));
    }

    [TestMethod]
    public void TotalPages_LargeTotal_ReturnsCorrectCount()
    {
        Assert.AreEqual(50, SearchPaginationHelper.CalculateTotalPages(1000, 20));
    }

    [TestMethod]
    public void TotalPages_SmallPageSize_ReturnsCorrectCount()
    {
        Assert.AreEqual(10, SearchPaginationHelper.CalculateTotalPages(50, 5));
    }

    [TestMethod]
    public void TotalPages_PageSize100_ReturnsCorrectCount()
    {
        Assert.AreEqual(1, SearchPaginationHelper.CalculateTotalPages(50, 100));
    }

    #endregion

    #region SearchResultDto facet ordering

    [TestMethod]
    public void FacetCounts_OrderedByValueDescending()
    {
        var facets = new Dictionary<string, int>
        {
            ["files"] = 23,
            ["notes"] = 5,
            ["chat"] = 12,
            ["contacts"] = 1
        };

        var ordered = facets.OrderByDescending(f => f.Value).Select(f => f.Key).ToList();

        Assert.AreEqual("files", ordered[0]);
        Assert.AreEqual("chat", ordered[1]);
        Assert.AreEqual("notes", ordered[2]);
        Assert.AreEqual("contacts", ordered[3]);
    }

    #endregion

    #region SearchResultDto model validation

    [TestMethod]
    public void SearchResultDto_DefaultValues_AreCorrect()
    {
        var result = new SearchResultDto
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(20, result.PageSize);
        Assert.IsNotNull(result.FacetCounts);
        Assert.AreEqual(0, result.FacetCounts.Count);
    }

    [TestMethod]
    public void SearchResultItem_DefaultSnippet_IsEmpty()
    {
        var item = new SearchResultItem
        {
            ModuleId = "files",
            EntityId = "123",
            EntityType = "FileNode",
            Title = "Test",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        Assert.AreEqual(string.Empty, item.Snippet);
        Assert.AreEqual(0.0, item.RelevanceScore);
        Assert.IsNotNull(item.Metadata);
        Assert.AreEqual(0, item.Metadata.Count);
    }

    [TestMethod]
    public void SearchQuery_DefaultValues_AreCorrect()
    {
        var query = new SearchQuery
        {
            QueryText = "test",
            UserId = Guid.NewGuid()
        };

        Assert.AreEqual("test", query.QueryText);
        Assert.IsNull(query.ModuleFilter);
        Assert.IsNull(query.EntityTypeFilter);
        Assert.AreEqual(1, query.Page);
        Assert.AreEqual(20, query.PageSize);
        Assert.AreEqual(SearchSortOrder.Relevance, query.SortOrder);
    }

    #endregion

    #region Search results page URL generation

    [TestMethod]
    public void BuildSearchPageUrl_BasicQuery_ReturnsCorrectUrl()
    {
        var url = SearchQueryUrlBuilder.BuildPageUrl("test query");
        Assert.AreEqual("/search?q=test%20query", url);
    }

    [TestMethod]
    public void BuildSearchPageUrl_WithModule_IncludesModule()
    {
        var url = SearchQueryUrlBuilder.BuildPageUrl("test", module: "notes");
        Assert.AreEqual("/search?q=test&module=notes", url);
    }

    [TestMethod]
    public void BuildSearchPageUrl_WithPage_IncludesPage()
    {
        var url = SearchQueryUrlBuilder.BuildPageUrl("test", page: 3);
        Assert.AreEqual("/search?q=test&page=3", url);
    }

    [TestMethod]
    public void BuildSearchPageUrl_Page1_ExcludesPageParam()
    {
        var url = SearchQueryUrlBuilder.BuildPageUrl("test", page: 1);
        Assert.AreEqual("/search?q=test", url);
    }

    [TestMethod]
    public void BuildSearchPageUrl_WithModuleAndPage_IncludesBoth()
    {
        var url = SearchQueryUrlBuilder.BuildPageUrl("test", module: "files", page: 2);
        Assert.AreEqual("/search?q=test&module=files&page=2", url);
    }

    #endregion
}

/// <summary>
/// Testable extraction of URL construction logic from DotNetCloudApiClient and SearchResults.razor.
/// </summary>
public static class SearchQueryUrlBuilder
{
    public static string Build(
        string query,
        string? module = null,
        string? type = null,
        int page = 1,
        int pageSize = 20,
        string sort = "relevance")
    {
        var url = $"api/v1/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}&sort={Uri.EscapeDataString(sort)}";
        if (!string.IsNullOrWhiteSpace(module))
            url += $"&module={Uri.EscapeDataString(module)}";
        if (!string.IsNullOrWhiteSpace(type))
            url += $"&type={Uri.EscapeDataString(type)}";
        return url;
    }

    public static string BuildSuggest(string query)
    {
        return $"api/v1/search/suggest?q={Uri.EscapeDataString(query)}";
    }

    public static string BuildPageUrl(string query, string? module = null, int page = 1)
    {
        var url = $"/search?q={Uri.EscapeDataString(query)}";
        if (!string.IsNullOrWhiteSpace(module))
            url += $"&module={Uri.EscapeDataString(module)}";
        if (page > 1)
            url += $"&page={page}";
        return url;
    }
}

/// <summary>
/// Testable pagination helper matching SearchResults.razor logic.
/// </summary>
public static class SearchPaginationHelper
{
    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    }
}
