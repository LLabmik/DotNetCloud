using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Tests.DTOs.Search;

/// <summary>
/// Tests for the <see cref="SearchResultDto"/> and <see cref="SearchResultItem"/> records.
/// </summary>
[TestClass]
public class SearchResultDtoTests
{
    [TestMethod]
    public void SearchResultDto_CanBeCreated_WithEmptyResults()
    {
        // Act
        var result = new SearchResultDto
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        // Assert
        Assert.AreEqual(0, result.Items.Count);
        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(20, result.PageSize);
        Assert.AreEqual(0, result.FacetCounts.Count);
    }

    [TestMethod]
    public void SearchResultDto_CanBeCreated_WithItemsAndFacets()
    {
        // Arrange
        var items = new List<SearchResultItem>
        {
            new()
            {
                ModuleId = "notes",
                EntityId = Guid.NewGuid().ToString(),
                EntityType = "Note",
                Title = "Quarterly Report",
                Snippet = "...the <mark>quarterly</mark> <mark>report</mark> shows...",
                RelevanceScore = 0.95,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                ModuleId = "files",
                EntityId = Guid.NewGuid().ToString(),
                EntityType = "FileNode",
                Title = "Report.pdf",
                RelevanceScore = 0.8,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        var facets = new Dictionary<string, int> { ["notes"] = 5, ["files"] = 3 };

        // Act
        var result = new SearchResultDto
        {
            Items = items,
            TotalCount = 8,
            Page = 1,
            PageSize = 20,
            FacetCounts = facets
        };

        // Assert
        Assert.AreEqual(2, result.Items.Count);
        Assert.AreEqual(8, result.TotalCount);
        Assert.AreEqual(2, result.FacetCounts.Count);
        Assert.AreEqual(5, result.FacetCounts["notes"]);
        Assert.AreEqual(3, result.FacetCounts["files"]);
    }

    [TestMethod]
    public void SearchResultItem_CanBeCreated_WithDefaults()
    {
        // Act
        var item = new SearchResultItem
        {
            ModuleId = "chat",
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "Message",
            Title = "#general",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.AreEqual("chat", item.ModuleId);
        Assert.AreEqual("Message", item.EntityType);
        Assert.AreEqual(string.Empty, item.Snippet);
        Assert.AreEqual(0.0, item.RelevanceScore);
        Assert.AreEqual(0, item.Metadata.Count);
    }

    [TestMethod]
    public void SearchResultItem_CanBeCreated_WithMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["MimeType"] = "application/pdf",
            ["Size"] = "1048576"
        };

        // Act
        var item = new SearchResultItem
        {
            ModuleId = "files",
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "FileNode",
            Title = "Report.pdf",
            Snippet = "...extracted <mark>content</mark>...",
            RelevanceScore = 0.92,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = metadata
        };

        // Assert
        Assert.AreEqual("application/pdf", item.Metadata["MimeType"]);
        Assert.AreEqual("1048576", item.Metadata["Size"]);
        Assert.AreEqual(0.92, item.RelevanceScore);
    }
}
