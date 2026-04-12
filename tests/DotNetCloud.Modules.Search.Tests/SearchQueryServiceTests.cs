using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchQueryService"/>.
/// </summary>
[TestClass]
public class SearchQueryServiceTests
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

    [TestMethod]
    public async Task SearchAsync_EmptyQueryText_ReturnsEmptyResult()
    {
        var query = new SearchQuery
        {
            QueryText = "   ",
            UserId = Guid.NewGuid(),
            Page = 1,
            PageSize = 20
        };

        var result = await _service.SearchAsync(query);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Items.Count);
        _searchProviderMock.Verify(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SearchAsync_ValidQuery_DelegatesToProvider()
    {
        var query = new SearchQuery
        {
            QueryText = "test search",
            UserId = Guid.NewGuid(),
            Page = 1,
            PageSize = 20
        };

        var expected = new SearchResultDto
        {
            Items = [new SearchResultItem
            {
                ModuleId = "files",
                EntityId = "e1",
                EntityType = "FileNode",
                Title = "Test result",
                UpdatedAt = DateTimeOffset.UtcNow,
            }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("Test result", result.Items[0].Title);
    }

    [TestMethod]
    public async Task SearchAsync_NullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _service.SearchAsync(null!));
    }

    [TestMethod]
    public async Task GetStatsAsync_DelegatesToProvider()
    {
        var expected = new SearchIndexStats
        {
            TotalDocuments = 42,
            DocumentsPerModule = new Dictionary<string, int> { ["files"] = 30, ["notes"] = 12 }
        };

        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var stats = await _service.GetStatsAsync();

        Assert.AreEqual(42, stats.TotalDocuments);
        Assert.AreEqual(2, stats.DocumentsPerModule.Count);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_DelegatesToProvider()
    {
        await _service.ReindexModuleAsync("files");

        _searchProviderMock.Verify(
            p => p.ReindexModuleAsync("files", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
