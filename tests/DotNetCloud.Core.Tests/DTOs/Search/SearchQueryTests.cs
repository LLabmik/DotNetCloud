using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Tests.DTOs.Search;

/// <summary>
/// Tests for the <see cref="SearchQuery"/> record and <see cref="SearchSortOrder"/> enum.
/// </summary>
[TestClass]
public class SearchQueryTests
{
    [TestMethod]
    public void SearchQuery_CanBeCreated_WithDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var query = new SearchQuery
        {
            QueryText = "quarterly report",
            UserId = userId
        };

        // Assert
        Assert.AreEqual("quarterly report", query.QueryText);
        Assert.AreEqual(userId, query.UserId);
        Assert.IsNull(query.ModuleFilter);
        Assert.IsNull(query.EntityTypeFilter);
        Assert.AreEqual(1, query.Page);
        Assert.AreEqual(20, query.PageSize);
        Assert.AreEqual(SearchSortOrder.Relevance, query.SortOrder);
    }

    [TestMethod]
    public void SearchQuery_CanBeCreated_WithAllFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = userId,
            ModuleFilter = "notes",
            EntityTypeFilter = "Note",
            Page = 3,
            PageSize = 50,
            SortOrder = SearchSortOrder.DateDesc
        };

        // Assert
        Assert.AreEqual("budget", query.QueryText);
        Assert.AreEqual("notes", query.ModuleFilter);
        Assert.AreEqual("Note", query.EntityTypeFilter);
        Assert.AreEqual(3, query.Page);
        Assert.AreEqual(50, query.PageSize);
        Assert.AreEqual(SearchSortOrder.DateDesc, query.SortOrder);
    }

    [TestMethod]
    public void SearchSortOrder_HasExpectedValues()
    {
        // Assert
        Assert.AreEqual(0, (int)SearchSortOrder.Relevance);
        Assert.AreEqual(1, (int)SearchSortOrder.DateDesc);
        Assert.AreEqual(2, (int)SearchSortOrder.DateAsc);
    }

    [TestMethod]
    public void SearchSortOrder_CanParse_FromString()
    {
        // Act
        var relevance = Enum.Parse<SearchSortOrder>("Relevance");
        var dateDesc = Enum.Parse<SearchSortOrder>("DateDesc");
        var dateAsc = Enum.Parse<SearchSortOrder>("DateAsc");

        // Assert
        Assert.AreEqual(SearchSortOrder.Relevance, relevance);
        Assert.AreEqual(SearchSortOrder.DateDesc, dateDesc);
        Assert.AreEqual(SearchSortOrder.DateAsc, dateAsc);
    }
}
