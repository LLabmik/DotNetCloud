using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase5;

/// <summary>
/// Tests for <see cref="SearchQueryService"/> Phase 5 enhancements —
/// query parsing integration, filter extraction from query syntax, and edge cases.
/// </summary>
[TestClass]
public class SearchQueryServicePhase5Tests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchQueryService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _service = new SearchQueryService(
            _searchProviderMock.Object,
            NullLogger<SearchQueryService>.Instance);
    }

    #region Parser integration

    [TestMethod]
    public async Task SearchAsync_WithInModuleSyntax_AppliesModuleFilter()
    {
        var query = new SearchQuery
        {
            QueryText = "in:notes budget",
            UserId = Guid.NewGuid()
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        // Verify the provider was called with the module filter applied
        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "notes"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_WithTypeSyntax_AppliesTypeFilter()
    {
        var query = new SearchQuery
        {
            QueryText = "type:pdf annual report",
            UserId = Guid.NewGuid()
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.EntityTypeFilter == "pdf"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_WithBothFilterSyntax_AppliesBothFilters()
    {
        var query = new SearchQuery
        {
            QueryText = "in:files type:pdf annual",
            UserId = Guid.NewGuid()
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "files" && q.EntityTypeFilter == "pdf"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_ParsedFilterOverridesExplicitFilter()
    {
        var query = new SearchQuery
        {
            QueryText = "in:chat budget",
            UserId = Guid.NewGuid(),
            ModuleFilter = "files" // explicit filter
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        // Parsed filter (in:chat) should override explicit filter (files)
        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "chat"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_NoFilterSyntax_PreservesExplicitFilter()
    {
        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = Guid.NewGuid(),
            ModuleFilter = "files"
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "files"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Empty/edge cases

    [TestMethod]
    public async Task SearchAsync_EmptyQueryText_ReturnsEmptyResult()
    {
        var query = new SearchQuery
        {
            QueryText = "   ",
            UserId = Guid.NewGuid()
        };

        var result = await _service.SearchAsync(query);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Items.Count);
        _searchProviderMock.Verify(
            p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SearchAsync_OnlyFiltersNoTerms_ReturnsEmptyResult()
    {
        var query = new SearchQuery
        {
            QueryText = "in:notes",
            UserId = Guid.NewGuid()
        };

        var result = await _service.SearchAsync(query);

        // in:notes alone has no searchable content (no terms, no phrases)
        Assert.AreEqual(0, result.TotalCount);
        _searchProviderMock.Verify(
            p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SearchAsync_OnlyExclusionsNoTerms_ReturnsEmptyResult()
    {
        var query = new SearchQuery
        {
            QueryText = "-draft -old",
            UserId = Guid.NewGuid()
        };

        var result = await _service.SearchAsync(query);

        // Only exclusions, no positive search terms
        Assert.AreEqual(0, result.TotalCount);
        _searchProviderMock.Verify(
            p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SearchAsync_NullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SearchAsync(null!));
    }

    [TestMethod]
    public async Task SearchAsync_QuotedPhraseOnly_DelegatesToProvider()
    {
        var query = new SearchQuery
        {
            QueryText = "\"quarterly report\"",
            UserId = Guid.NewGuid()
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [new SearchResultItem
                {
                    ModuleId = "notes",
                    EntityId = "n1",
                    EntityType = "Note",
                    Title = "Quarterly Report",
                    UpdatedAt = DateTimeOffset.UtcNow
                }],
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        var result = await _service.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        _searchProviderMock.Verify(
            p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_ComplexQuery_DelegatesToProviderWithFilters()
    {
        var query = new SearchQuery
        {
            QueryText = "\"quarterly report\" in:files type:pdf budget -draft",
            UserId = Guid.NewGuid()
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        await _service.SearchAsync(query);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q =>
                q.ModuleFilter == "files" &&
                q.EntityTypeFilter == "pdf"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Return value forwarding

    [TestMethod]
    public async Task SearchAsync_ReturnsProviderResultsUnmodified()
    {
        var userId = Guid.NewGuid();
        var query = new SearchQuery { QueryText = "test", UserId = userId };

        var expected = new SearchResultDto
        {
            Items = [
                new SearchResultItem
                {
                    ModuleId = "files",
                    EntityId = "f1",
                    EntityType = "FileNode",
                    Title = "Test <mark>File</mark>",
                    Snippet = "Content with <mark>test</mark>",
                    RelevanceScore = 3.0,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Metadata = new Dictionary<string, string> { ["MimeType"] = "application/pdf" }
                }
            ],
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            FacetCounts = new Dictionary<string, int> { ["files"] = 1 }
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.SearchAsync(query);

        Assert.AreEqual(expected.TotalCount, result.TotalCount);
        Assert.AreEqual(expected.Items.Count, result.Items.Count);
        Assert.AreEqual(expected.Items[0].Title, result.Items[0].Title);
        Assert.AreEqual(expected.Items[0].Snippet, result.Items[0].Snippet);
        Assert.AreEqual(expected.Items[0].RelevanceScore, result.Items[0].RelevanceScore);
        Assert.AreEqual(expected.FacetCounts["files"], result.FacetCounts["files"]);
    }

    [TestMethod]
    public async Task GetStatsAsync_DelegatesToProvider()
    {
        var expected = new SearchIndexStats
        {
            TotalDocuments = 100,
            DocumentsPerModule = new Dictionary<string, int>
            {
                ["files"] = 50,
                ["notes"] = 30,
                ["chat"] = 20
            },
            LastFullReindexAt = DateTimeOffset.UtcNow.AddHours(-12)
        };

        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var stats = await _service.GetStatsAsync();

        Assert.AreEqual(100, stats.TotalDocuments);
        Assert.AreEqual(3, stats.DocumentsPerModule.Count);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_DelegatesToProvider()
    {
        await _service.ReindexModuleAsync("files");

        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("files", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
