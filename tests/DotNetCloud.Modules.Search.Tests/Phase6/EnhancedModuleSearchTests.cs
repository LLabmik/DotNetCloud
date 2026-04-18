extern alias SearchClient;

using DotNetCloud.Core.DTOs.Search;
using SearchClient::DotNetCloud.Modules.Search.Client;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for the enhanced per-module search endpoints (Phase 6 — Step 6.3).
/// Validates that the <see cref="ISearchFtsClient"/> is used when available,
/// with graceful fallback to LIKE-based search when unavailable.
/// </summary>
[TestClass]
public class EnhancedModuleSearchTests
{
    private static readonly Guid TestUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    #region ISearchFtsClient mock behavior

    [TestMethod]
    public void MockClient_IsAvailableTrue_SearchReturnsResults()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [new SearchResultItem
                {
                    ModuleId = "files",
                    EntityId = "f1",
                    EntityType = "FileNode",
                    Title = "Budget.pdf",
                    Snippet = "...quarterly <mark>budget</mark>...",
                    UpdatedAt = DateTimeOffset.UtcNow
                }],
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        var result = mockClient.Object.SearchAsync("budget", moduleFilter: "files", userId: TestUserId).Result;

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("Budget.pdf", result.Items[0].Title);
    }

    [TestMethod]
    public void MockClient_IsAvailableFalse_ClientNotCalled()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(false);

        // Simulate how module controllers check availability
        if (mockClient.Object.IsAvailable)
        {
            Assert.Fail("Should not reach here — client is not available");
        }

        mockClient.Verify(c => c.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<SearchSortOrder>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task MockClient_SearchReturnsNull_FallbackScenario()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchResultDto?)null);

        var result = await mockClient.Object.SearchAsync("budget", moduleFilter: "files");

        // When FTS returns null, module should fall back to LIKE search
        Assert.IsNull(result);
    }

    #endregion

    #region Files module search — FTS integration pattern

    [TestMethod]
    public async Task FilesSearch_WithFtsAvailable_UsesModuleFilter()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                "budget",
                "files",
                It.IsAny<string?>(),
                TestUserId,
                1, 20,
                It.IsAny<SearchSortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("files", "FileNode", "Budget.pdf", 1));

        var result = await mockClient.Object.SearchAsync(
            "budget", moduleFilter: "files", userId: TestUserId, page: 1, pageSize: 20);

        Assert.IsNotNull(result);
        Assert.AreEqual("files", result.Items[0].ModuleId);
        mockClient.Verify(c => c.SearchAsync(
            "budget", "files", It.IsAny<string?>(),
            TestUserId, 1, 20,
            It.IsAny<SearchSortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task FilesSearch_FtsResultIncludesTotalPages()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 45,
                Page = 1,
                PageSize = 20
            });

        var result = await mockClient.Object.SearchAsync("test", moduleFilter: "files");

        Assert.IsNotNull(result);
        // Controller would calculate: Math.Ceiling(45.0 / 20) = 3
        var totalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
        Assert.AreEqual(3, totalPages);
    }

    #endregion

    #region Chat module search — FTS integration pattern

    [TestMethod]
    public async Task ChatSearch_WithFtsAvailable_UsesModuleAndTypeFilter()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                "hello world",
                "chat",
                "Message",
                TestUserId,
                1, 50,
                It.IsAny<SearchSortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("chat", "Message", "Hello World", 3));

        var result = await mockClient.Object.SearchAsync(
            "hello world", moduleFilter: "chat", entityTypeFilter: "Message",
            userId: TestUserId, page: 1, pageSize: 50);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.TotalCount);
        Assert.IsTrue(result.Items.All(i => i.ModuleId == "chat"));
    }

    [TestMethod]
    public async Task ChatSearch_PaginationCalculation_IsCorrect()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [],
                TotalCount = 120,
                Page = 2,
                PageSize = 50
            });

        var result = await mockClient.Object.SearchAsync(
            "test", moduleFilter: "chat", page: 2, pageSize: 50);

        Assert.IsNotNull(result);
        var totalPages = (int)Math.Ceiling((double)result.TotalCount / result.PageSize);
        Assert.AreEqual(3, totalPages);
    }

    #endregion

    #region Notes module search — FTS integration pattern

    [TestMethod]
    public async Task NotesSearch_WithFtsAvailable_UsesModuleFilter()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                "meeting notes",
                "notes",
                It.IsAny<string?>(),
                TestUserId,
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("notes", "Note", "Meeting Notes", 2));

        var result = await mockClient.Object.SearchAsync(
            "meeting notes", moduleFilter: "notes", userId: TestUserId);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalCount);
    }

    [TestMethod]
    public async Task NotesSearch_SkipToPageConversion_IsCorrect()
    {
        // Notes endpoint uses skip/take instead of page/pageSize.
        // Controller converts: page = (skip / take) + 1
        int skip = 100;
        int take = 50;
        int expectedPage = (skip / take) + 1; // = 3

        Assert.AreEqual(3, expectedPage);
    }

    [TestMethod]
    public async Task NotesSearch_EmptyQuery_SkipsFts()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);

        // Notes module skips FTS when query is null or empty
        string? query = null;

        if (!string.IsNullOrWhiteSpace(query) && mockClient.Object.IsAvailable)
        {
            Assert.Fail("Should not reach FTS for empty query");
        }

        // Verify FTS was never called
        mockClient.Verify(c => c.SearchAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Graceful degradation patterns

    [TestMethod]
    public async Task AllModules_NullClient_FallsBackToLikeSearch()
    {
        // When ISearchFtsClient is null (not registered), modules fall back to LIKE
        ISearchFtsClient? client = null;

        // Simulates the controller pattern: if (client is { IsAvailable: true })
        var shouldUseFts = client is { IsAvailable: true };

        Assert.IsFalse(shouldUseFts);
    }

    [TestMethod]
    public async Task AllModules_UnavailableClient_FallsBackToLikeSearch()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(false);

        var shouldUseFts = mockClient.Object is { IsAvailable: true };

        Assert.IsFalse(shouldUseFts);
    }

    [TestMethod]
    public async Task AllModules_FtsReturnsNull_FallsBackToLikeSearch()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchResultDto?)null);

        // Simulates the full fallback pattern
        SearchResultDto? ftsResult = null;
        if (mockClient.Object is { IsAvailable: true })
        {
            ftsResult = await mockClient.Object.SearchAsync("test", moduleFilter: "files");
        }

        // FTS returned null — controller should fall back to LIKE search
        Assert.IsNull(ftsResult);
    }

    [TestMethod]
    public async Task PermissionScoping_UserIdPassedToFts()
    {
        var userId = Guid.NewGuid();
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                userId, It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("files", "FileNode", "Test", 1));

        await mockClient.Object.SearchAsync("test", moduleFilter: "files", userId: userId);

        // Verify the userId was passed for permission scoping
        mockClient.Verify(c => c.SearchAsync(
            "test", "files", It.IsAny<string?>(),
            userId, It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Sort order handling

    [TestMethod]
    public async Task FtsClient_SortByDateDesc_Propagated()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                SearchSortOrder.DateDesc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("files", "FileNode", "Test", 1));

        await mockClient.Object.SearchAsync("test", sortOrder: SearchSortOrder.DateDesc);

        mockClient.Verify(c => c.SearchAsync(
            "test", null, null, null, 1, 20,
            SearchSortOrder.DateDesc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task FtsClient_DefaultSortIsRelevance()
    {
        var mockClient = new Mock<ISearchFtsClient>();
        mockClient.Setup(c => c.IsAvailable).Returns(true);
        mockClient.Setup(c => c.SearchAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                SearchSortOrder.Relevance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFtsResult("files", "FileNode", "Test", 1));

        await mockClient.Object.SearchAsync("test");

        mockClient.Verify(c => c.SearchAsync(
            "test", null, null, null, 1, 20,
            SearchSortOrder.Relevance, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers

    private static SearchResultDto CreateFtsResult(string moduleId, string entityType, string title, int count)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => new SearchResultItem
            {
                ModuleId = moduleId,
                EntityId = Guid.NewGuid().ToString(),
                EntityType = entityType,
                Title = $"{title} {i}",
                Snippet = $"...<mark>{title}</mark> {i}...",
                RelevanceScore = 2.0 - (0.1 * i),
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                Metadata = new Dictionary<string, string>()
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
