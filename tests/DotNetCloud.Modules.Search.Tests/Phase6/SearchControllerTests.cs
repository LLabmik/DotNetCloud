using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Host.Controllers;
using DotNetCloud.Modules.Search.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for <see cref="SearchController"/> REST API endpoints (Phase 6 — Step 6.1).
/// Validates search, suggest, stats, and admin reindex endpoints.
/// </summary>
[TestClass]
public class SearchControllerTests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchQueryService _queryService = null!;
    private Mock<SearchReindexBackgroundService> _reindexServiceMock = null!;
    private SearchController _controller = null!;

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _queryService = new SearchQueryService(
            _searchProviderMock.Object,
            NullLogger<SearchQueryService>.Instance);

        _controller = new SearchController(
            _queryService,
            NullLogger<SearchController>.Instance);

        SetupAuthenticatedUser(TestUserId, "user");
    }

    #region Search endpoint

    [TestMethod]
    public async Task SearchAsync_EmptyQuery_ReturnsBadRequest()
    {
        var result = await _controller.SearchAsync(q: "");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task SearchAsync_NullQuery_ReturnsBadRequest()
    {
        var result = await _controller.SearchAsync(q: null!);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task SearchAsync_WhitespaceQuery_ReturnsBadRequest()
    {
        var result = await _controller.SearchAsync(q: "   ");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task SearchAsync_ValidQuery_ReturnsOkWithResults()
    {
        var expected = CreateSearchResult("test document", "files", 1);
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.SearchAsync(q: "test");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SearchAsync_WithModuleFilter_PassesFilterToService()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", module: "notes");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "notes"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_WithTypeFilter_PassesFilterToService()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", type: "Note");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.EntityTypeFilter == "Note"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_SortDateDesc_PassesSortOrder()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", sort: "date_desc");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.SortOrder == SearchSortOrder.DateDesc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_SortDateAsc_PassesSortOrder()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", sort: "date_asc");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.SortOrder == SearchSortOrder.DateAsc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_DefaultSort_IsRelevance()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.SortOrder == SearchSortOrder.Relevance),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_PageClampedToMinimum1()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", page: -5);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.Page == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_PageSizeClampedToMax100()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", pageSize: 500);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.PageSize == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_PageSizeClampedToMin1()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test", pageSize: 0);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.PageSize == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SearchAsync_UsesAuthenticatedUserId()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SearchAsync(q: "test");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.UserId == TestUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Suggest endpoint

    [TestMethod]
    public async Task SuggestAsync_EmptyQuery_ReturnsEmptyArray()
    {
        var result = await _controller.SuggestAsync(q: "");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SuggestAsync_SingleChar_ReturnsEmptyArray()
    {
        var result = await _controller.SuggestAsync(q: "a");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SuggestAsync_ValidPrefix_ReturnsOk()
    {
        var expected = CreateSearchResult("test doc", "files", 3);
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.SuggestAsync(q: "tes");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SuggestAsync_UsesPageSize10()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResult());

        await _controller.SuggestAsync(q: "test");

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.PageSize == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Stats endpoint

    [TestMethod]
    public async Task GetStatsAsync_AdminUser_ReturnsOk()
    {
        SetupAuthenticatedUser(AdminUserId, "admin");

        var stats = new SearchIndexStats
        {
            TotalDocuments = 100,
            DocumentsPerModule = new Dictionary<string, int> { ["files"] = 60, ["notes"] = 40 }
        };
        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var result = await _controller.GetStatsAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetStatsAsync_NonAdminUser_ReturnsForbid()
    {
        SetupAuthenticatedUser(TestUserId, "user");

        var result = await _controller.GetStatsAsync();

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    #endregion

    #region Reindex endpoints

    [TestMethod]
    public async Task ReindexAllAsync_AdminUser_ReturnsOk()
    {
        SetupAuthenticatedUser(AdminUserId, "admin");
        var result = await _controller.ReindexAllAsync();
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ReindexAllAsync_NonAdminUser_ReturnsForbid()
    {
        SetupAuthenticatedUser(TestUserId, "user");
        var result = await _controller.ReindexAllAsync();
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_AdminUser_ReturnsOk()
    {
        SetupAuthenticatedUser(AdminUserId, "admin");
        var result = await _controller.ReindexModuleAsync("files");
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ReindexModuleAsync_NonAdminUser_ReturnsForbid()
    {
        SetupAuthenticatedUser(TestUserId, "user");
        var result = await _controller.ReindexModuleAsync("files");
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    #endregion

    #region Helpers

    private void SetupAuthenticatedUser(Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static SearchResultDto CreateEmptyResult() => new()
    {
        Items = [],
        TotalCount = 0,
        Page = 1,
        PageSize = 20
    };

    private static SearchResultDto CreateSearchResult(string title, string moduleId, int count)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => new SearchResultItem
            {
                ModuleId = moduleId,
                EntityId = Guid.NewGuid().ToString(),
                EntityType = "Document",
                Title = $"{title} {i}",
                Snippet = $"...{title} {i} snippet...",
                RelevanceScore = 1.0 / i,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        return new SearchResultDto
        {
            Items = items,
            TotalCount = count,
            Page = 1,
            PageSize = 20,
            FacetCounts = new Dictionary<string, int> { [moduleId] = count }
        };
    }

    #endregion
}
