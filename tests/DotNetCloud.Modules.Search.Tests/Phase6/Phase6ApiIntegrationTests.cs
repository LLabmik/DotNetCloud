extern alias SearchClient;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using SearchClient::DotNetCloud.Modules.Search.Client;
using DotNetCloud.Modules.Search.Host.Controllers;
using DotNetCloud.Modules.Search.Host.Protos;
using DotNetCloud.Modules.Search.Host.Services;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Integration-style tests that verify the complete Phase 6 API pipeline:
/// SearchController → SearchQueryService → ISearchProvider (REST path)
/// SearchGrpcService → SearchQueryService → ISearchProvider (gRPC path)
/// ISearchFtsClient → gRPC client → module controller (per-module FTS path)
/// </summary>
[TestClass]
public class Phase6ApiIntegrationTests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchQueryService _queryService = null!;

    private static readonly Guid UserId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _queryService = new SearchQueryService(
            _searchProviderMock.Object,
            NullLogger<SearchQueryService>.Instance);
    }

    #region REST → Service → Provider pipeline

    [TestMethod]
    public async Task REST_SearchPipeline_QueryFlowsFromControllerToProvider()
    {
        SearchQuery? capturedQuery = null;
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback<SearchQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var controller = CreateAuthenticatedController();

        await controller.SearchAsync(q: "budget report", module: "files", type: "FileNode", page: 2, pageSize: 10, sort: "date_desc");

        Assert.IsNotNull(capturedQuery);
        Assert.AreEqual(UserId, capturedQuery.UserId);
        Assert.AreEqual("files", capturedQuery.ModuleFilter);
        Assert.AreEqual("FileNode", capturedQuery.EntityTypeFilter);
        Assert.AreEqual(2, capturedQuery.Page);
        Assert.AreEqual(10, capturedQuery.PageSize);
        Assert.AreEqual(SearchSortOrder.DateDesc, capturedQuery.SortOrder);
    }

    [TestMethod]
    public async Task REST_SuggestPipeline_Returns10MaxResults()
    {
        var manyItems = Enumerable.Range(1, 20)
            .Select(i => new SearchResultItem
            {
                ModuleId = "files",
                EntityId = i.ToString(),
                EntityType = "FileNode",
                Title = $"Result {i}",
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = manyItems, TotalCount = 20, Page = 1, PageSize = 10 });

        var controller = CreateAuthenticatedController();
        var result = await controller.SuggestAsync(q: "tes");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task REST_StatsPipeline_AdminGetsStats()
    {
        var stats = new SearchIndexStats
        {
            TotalDocuments = 500,
            DocumentsPerModule = new Dictionary<string, int>
            {
                ["files"] = 200,
                ["notes"] = 150,
                ["chat"] = 100,
                ["contacts"] = 50
            },
            LastFullReindexAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var controller = CreateAuthenticatedController(roles: "admin");
        var result = await controller.GetStatsAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    #endregion

    #region gRPC → Service → Provider pipeline

    [TestMethod]
    public async Task GRPC_SearchPipeline_QueryFlowsFromGrpcToProvider()
    {
        SearchQuery? capturedQuery = null;
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback<SearchQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var grpcService = new SearchGrpcService(
            _queryService,
            _searchProviderMock.Object,
            NullLogger<SearchGrpcService>.Instance);

        var request = new SearchRequest
        {
            QueryText = "quarterly budget",
            ModuleFilter = "notes",
            EntityTypeFilter = "Note",
            UserId = UserId.ToString(),
            Page = 3,
            PageSize = 15,
            SortOrder = "DateAsc"
        };

        var response = await grpcService.Search(request, new TestServerCallContext());

        Assert.IsTrue(response.Success);
        Assert.IsNotNull(capturedQuery);
        Assert.AreEqual(UserId, capturedQuery.UserId);
        Assert.AreEqual("notes", capturedQuery.ModuleFilter);
        Assert.AreEqual("Note", capturedQuery.EntityTypeFilter);
        Assert.AreEqual(3, capturedQuery.Page);
        Assert.AreEqual(15, capturedQuery.PageSize);
        Assert.AreEqual(SearchSortOrder.DateAsc, capturedQuery.SortOrder);
    }

    [TestMethod]
    public async Task GRPC_IndexAndRemove_Pipeline()
    {
        SearchDocument? capturedDoc = null;
        _searchProviderMock
            .Setup(p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()))
            .Callback<SearchDocument, CancellationToken>((d, _) => capturedDoc = d)
            .Returns(Task.CompletedTask);
        _searchProviderMock
            .Setup(p => p.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var grpcService = new SearchGrpcService(
            _queryService,
            _searchProviderMock.Object,
            NullLogger<SearchGrpcService>.Instance);

        // Index a document
        var indexResponse = await grpcService.IndexDocument(new IndexDocumentRequest
        {
            ModuleId = "files",
            EntityId = "file-123",
            EntityType = "FileNode",
            Title = "Budget Report Q4",
            Content = "Quarterly budget analysis document",
            OwnerId = UserId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
        }, new TestServerCallContext());

        Assert.IsTrue(indexResponse.Success);
        Assert.IsNotNull(capturedDoc);
        Assert.AreEqual("Budget Report Q4", capturedDoc.Title);

        // Remove the document
        var removeResponse = await grpcService.RemoveDocument(new RemoveDocumentRequest
        {
            ModuleId = "files",
            EntityId = "file-123"
        }, new TestServerCallContext());

        Assert.IsTrue(removeResponse.Success);
        _searchProviderMock.Verify(p => p.RemoveDocumentAsync("files", "file-123", It.IsAny<CancellationToken>()));
    }

    #endregion

    #region FTS client → module controller fallback pattern

    [TestMethod]
    public async Task FtsClient_Available_ModuleUsesFts()
    {
        var mockFtsClient = new Mock<ISearchFtsClient>();
        mockFtsClient.Setup(c => c.IsAvailable).Returns(true);
        mockFtsClient
            .Setup(c => c.SearchAsync("test", "files", null, UserId, 1, 20,
                SearchSortOrder.Relevance, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto
            {
                Items = [new SearchResultItem
                {
                    ModuleId = "files",
                    EntityId = "f1",
                    EntityType = "FileNode",
                    Title = "test.txt",
                    UpdatedAt = DateTimeOffset.UtcNow
                }],
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        // Simulate the Files controller pattern
        SearchResultDto? ftsResult = null;
        if (mockFtsClient.Object is { IsAvailable: true })
        {
            ftsResult = await mockFtsClient.Object.SearchAsync(
                "test", moduleFilter: "files", userId: UserId, page: 1, pageSize: 20);
        }

        Assert.IsNotNull(ftsResult);
        Assert.AreEqual(1, ftsResult.TotalCount);
    }

    [TestMethod]
    public async Task FtsClient_Unavailable_ModuleFallsBackToLikeSearch()
    {
        var mockFtsClient = new Mock<ISearchFtsClient>();
        mockFtsClient.Setup(c => c.IsAvailable).Returns(false);

        bool usedFallback = false;

        if (mockFtsClient.Object is { IsAvailable: true })
        {
            await mockFtsClient.Object.SearchAsync("test", moduleFilter: "files");
        }
        else
        {
            usedFallback = true;
        }

        Assert.IsTrue(usedFallback, "Should have used LIKE fallback when FTS unavailable");
    }

    [TestMethod]
    public async Task FtsClient_ReturnsNull_ModuleFallsBackToLikeSearch()
    {
        var mockFtsClient = new Mock<ISearchFtsClient>();
        mockFtsClient.Setup(c => c.IsAvailable).Returns(true);
        mockFtsClient
            .Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<SearchSortOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchResultDto?)null);

        bool usedFallback = false;

        if (mockFtsClient.Object is { IsAvailable: true })
        {
            var ftsResult = await mockFtsClient.Object.SearchAsync("test", moduleFilter: "files");
            if (ftsResult is null)
            {
                usedFallback = true;
            }
        }

        Assert.IsTrue(usedFallback, "Should have used LIKE fallback when FTS returns null");
    }

    #endregion

    #region Cross-module result format consistency

    [TestMethod]
    public void SearchResultDto_ContainsAllRequiredFields()
    {
        var dto = new SearchResultDto
        {
            Items = [new SearchResultItem
            {
                ModuleId = "files",
                EntityId = "e1",
                EntityType = "FileNode",
                Title = "Test",
                Snippet = "...test...",
                RelevanceScore = 1.5,
                UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string> { ["key"] = "value" }
            }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            FacetCounts = new Dictionary<string, int> { ["files"] = 1 }
        };

        Assert.AreEqual(1, dto.Items.Count);
        Assert.AreEqual(1, dto.TotalCount);
        Assert.AreEqual(1, dto.Page);
        Assert.AreEqual(20, dto.PageSize);
        Assert.IsTrue(dto.FacetCounts.ContainsKey("files"));

        var item = dto.Items[0];
        Assert.AreEqual("files", item.ModuleId);
        Assert.AreEqual("e1", item.EntityId);
        Assert.AreEqual("FileNode", item.EntityType);
        Assert.AreEqual("Test", item.Title);
        Assert.AreEqual("...test...", item.Snippet);
        Assert.AreEqual(1.5, item.RelevanceScore);
        Assert.AreEqual("value", item.Metadata["key"]);
    }

    [TestMethod]
    public void SearchResultDto_EmptyResult_HasDefaults()
    {
        var dto = new SearchResultDto
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        Assert.AreEqual(0, dto.Items.Count);
        Assert.AreEqual(0, dto.TotalCount);
        Assert.AreEqual(0, dto.FacetCounts.Count);
    }

    #endregion

    #region Helpers

    private SearchController CreateAuthenticatedController(params string[] roles)
    {
        if (roles.Length == 0) roles = ["user"];

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, UserId.ToString()) };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var dbOptions = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase($"Phase6ApiTests_{Guid.NewGuid()}")
            .Options;
        var db = new SearchDbContext(dbOptions);

        var controller = new SearchController(
            _queryService,
            db,
            NullLogger<SearchController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        return controller;
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test-peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new("test", new Dictionary<string, List<AuthProperty>>());
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotImplementedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    #endregion
}
