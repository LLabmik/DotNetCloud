using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Host.Protos;
using DotNetCloud.Modules.Search.Host.Services;
using DotNetCloud.Modules.Search.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase6;

/// <summary>
/// Tests for <see cref="SearchGrpcService"/> gRPC API endpoints (Phase 6 — Step 6.2).
/// Validates Search, IndexDocument, RemoveDocument, ReindexModule, and GetIndexStats RPCs.
/// </summary>
[TestClass]
public class SearchGrpcServiceTests
{
    private Mock<ISearchProvider> _searchProviderMock = null!;
    private SearchQueryService _queryService = null!;
    private SearchGrpcService _grpcService = null!;
    private TestServerCallContext _callContext = null!;

    private static readonly Guid TestUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestOwnerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [TestInitialize]
    public void Setup()
    {
        _searchProviderMock = new Mock<ISearchProvider>();
        _queryService = new SearchQueryService(
            _searchProviderMock.Object,
            NullLogger<SearchQueryService>.Instance);

        _grpcService = new SearchGrpcService(
            _queryService,
            _searchProviderMock.Object,
            NullLogger<SearchGrpcService>.Instance);

        _callContext = new TestServerCallContext();
    }

    #region Search RPC

    [TestMethod]
    public async Task Search_ValidRequest_ReturnsSuccessResponse()
    {
        var expected = new SearchResultDto
        {
            Items = [new SearchResultItem
            {
                ModuleId = "files",
                EntityId = "e1",
                EntityType = "FileNode",
                Title = "Budget Report",
                Snippet = "...budget...",
                RelevanceScore = 1.5,
                UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string> { ["MimeType"] = "application/pdf" }
            }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            FacetCounts = new Dictionary<string, int> { ["files"] = 1 }
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var request = new SearchRequest
        {
            QueryText = "budget report",
            UserId = TestUserId.ToString(),
            Page = 1,
            PageSize = 20,
            SortOrder = "Relevance"
        };

        var response = await _grpcService.Search(request, _callContext);

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, response.TotalCount);
        Assert.AreEqual(1, response.Items.Count);
        Assert.AreEqual("Budget Report", response.Items[0].Title);
        Assert.AreEqual("files", response.Items[0].ModuleId);
        Assert.IsTrue(response.FacetCounts.ContainsKey("files"));
    }

    [TestMethod]
    public async Task Search_InvalidUserId_ReturnsFailure()
    {
        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = "not-a-guid",
            Page = 1,
            PageSize = 20
        };

        var response = await _grpcService.Search(request, _callContext);

        Assert.IsFalse(response.Success);
        Assert.AreEqual("Invalid user ID", response.ErrorMessage);
    }

    [TestMethod]
    public async Task Search_WithModuleFilter_PassesFilter()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString(),
            ModuleFilter = "notes",
            Page = 1,
            PageSize = 20
        };

        await _grpcService.Search(request, _callContext);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == "notes"),
            It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Search_EmptyModuleFilter_TreatedAsNull()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString(),
            ModuleFilter = "",
            Page = 1,
            PageSize = 20
        };

        await _grpcService.Search(request, _callContext);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.ModuleFilter == null),
            It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Search_SortDateDesc_Works()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString(),
            SortOrder = "DateDesc",
            Page = 1,
            PageSize = 20
        };

        await _grpcService.Search(request, _callContext);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.SortOrder == SearchSortOrder.DateDesc),
            It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Search_ResponseIncludesMetadata()
    {
        var expected = new SearchResultDto
        {
            Items = [new SearchResultItem
            {
                ModuleId = "files",
                EntityId = "e1",
                EntityType = "FileNode",
                Title = "Test",
                UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["MimeType"] = "text/plain",
                    ["Size"] = "1024"
                }
            }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString(),
            Page = 1,
            PageSize = 20
        };

        var response = await _grpcService.Search(request, _callContext);

        Assert.AreEqual("text/plain", response.Items[0].Metadata["MimeType"]);
        Assert.AreEqual("1024", response.Items[0].Metadata["Size"]);
    }

    [TestMethod]
    public async Task Search_DefaultPageAndPageSize_Applied()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString()
            // page and pageSize default to 0
        };

        await _grpcService.Search(request, _callContext);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.Page == 1 && q.PageSize == 20),
            It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task Search_PageSizeCappedAt100()
    {
        _searchProviderMock
            .Setup(p => p.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResultDto { Items = [], TotalCount = 0, Page = 1, PageSize = 100 });

        var request = new SearchRequest
        {
            QueryText = "test",
            UserId = TestUserId.ToString(),
            Page = 1,
            PageSize = 500
        };

        await _grpcService.Search(request, _callContext);

        _searchProviderMock.Verify(p => p.SearchAsync(
            It.Is<SearchQuery>(q => q.PageSize == 100),
            It.IsAny<CancellationToken>()));
    }

    #endregion

    #region IndexDocument RPC

    [TestMethod]
    public async Task IndexDocument_ValidRequest_ReturnsSuccess()
    {
        _searchProviderMock
            .Setup(p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IndexDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1",
            EntityType = "FileNode",
            Title = "Test Document",
            Content = "Document content here",
            OwnerId = TestOwnerId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            UpdatedAt = DateTimeOffset.UtcNow.ToString("o")
        };

        var response = await _grpcService.IndexDocument(request, _callContext);

        Assert.IsTrue(response.Success);
        _searchProviderMock.Verify(p => p.IndexDocumentAsync(
            It.Is<SearchDocument>(d =>
                d.ModuleId == "files" &&
                d.EntityId == "entity-1" &&
                d.Title == "Test Document"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IndexDocument_InvalidOwnerId_ReturnsFailure()
    {
        var request = new IndexDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1",
            EntityType = "FileNode",
            Title = "Test",
            OwnerId = "not-a-guid"
        };

        var response = await _grpcService.IndexDocument(request, _callContext);

        Assert.IsFalse(response.Success);
        Assert.AreEqual("Invalid owner ID", response.ErrorMessage);
    }

    [TestMethod]
    public async Task IndexDocument_WithOptionalOrganizationId_SetsIdCorrectly()
    {
        var orgId = Guid.NewGuid();
        _searchProviderMock
            .Setup(p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IndexDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1",
            EntityType = "FileNode",
            Title = "Test",
            OwnerId = TestOwnerId.ToString(),
            OrganizationId = orgId.ToString()
        };

        var response = await _grpcService.IndexDocument(request, _callContext);

        Assert.IsTrue(response.Success);
        _searchProviderMock.Verify(p => p.IndexDocumentAsync(
            It.Is<SearchDocument>(d => d.OrganizationId == orgId),
            It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task IndexDocument_WithMetadata_PassesMetadata()
    {
        _searchProviderMock
            .Setup(p => p.IndexDocumentAsync(It.IsAny<SearchDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IndexDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1",
            EntityType = "FileNode",
            Title = "Test",
            OwnerId = TestOwnerId.ToString()
        };
        request.Metadata["MimeType"] = "application/pdf";
        request.Metadata["Size"] = "2048";

        var response = await _grpcService.IndexDocument(request, _callContext);

        Assert.IsTrue(response.Success);
        _searchProviderMock.Verify(p => p.IndexDocumentAsync(
            It.Is<SearchDocument>(d =>
                d.Metadata["MimeType"] == "application/pdf" &&
                d.Metadata["Size"] == "2048"),
            It.IsAny<CancellationToken>()));
    }

    #endregion

    #region RemoveDocument RPC

    [TestMethod]
    public async Task RemoveDocument_ValidRequest_ReturnsSuccess()
    {
        _searchProviderMock
            .Setup(p => p.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new RemoveDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1"
        };

        var response = await _grpcService.RemoveDocument(request, _callContext);

        Assert.IsTrue(response.Success);
        _searchProviderMock.Verify(p => p.RemoveDocumentAsync("files", "entity-1", It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task RemoveDocument_ProviderThrows_ReturnsFailure()
    {
        _searchProviderMock
            .Setup(p => p.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var request = new RemoveDocumentRequest
        {
            ModuleId = "files",
            EntityId = "entity-1"
        };

        var response = await _grpcService.RemoveDocument(request, _callContext);

        Assert.IsFalse(response.Success);
        Assert.AreEqual("Remove failed", response.ErrorMessage);
    }

    #endregion

    #region ReindexModule RPC

    [TestMethod]
    public async Task ReindexModule_ValidRequest_ReturnsSuccess()
    {
        _searchProviderMock
            .Setup(p => p.ReindexModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ReindexModuleRequest { ModuleId = "files" };

        var response = await _grpcService.ReindexModule(request, _callContext);

        Assert.IsTrue(response.Success);
        Assert.IsFalse(string.IsNullOrEmpty(response.JobId));
    }

    [TestMethod]
    public async Task ReindexModule_ProviderThrows_ReturnsFailure()
    {
        _searchProviderMock
            .Setup(p => p.ReindexModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Reindex failed"));

        var request = new ReindexModuleRequest { ModuleId = "files" };

        var response = await _grpcService.ReindexModule(request, _callContext);

        Assert.IsFalse(response.Success);
    }

    #endregion

    #region GetIndexStats RPC

    [TestMethod]
    public async Task GetIndexStats_ReturnsCorrectData()
    {
        var now = DateTimeOffset.UtcNow;
        var stats = new SearchIndexStats
        {
            TotalDocuments = 150,
            DocumentsPerModule = new Dictionary<string, int>
            {
                ["files"] = 80,
                ["notes"] = 40,
                ["chat"] = 30
            },
            LastFullReindexAt = now.AddHours(-2),
            LastIncrementalIndexAt = now.AddMinutes(-5)
        };

        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var response = await _grpcService.GetIndexStats(new GetIndexStatsRequest(), _callContext);

        Assert.IsTrue(response.Success);
        Assert.AreEqual(150, response.TotalDocuments);
        Assert.AreEqual(80, response.DocumentsPerModule["files"]);
        Assert.AreEqual(40, response.DocumentsPerModule["notes"]);
        Assert.AreEqual(30, response.DocumentsPerModule["chat"]);
        Assert.IsFalse(string.IsNullOrEmpty(response.LastFullReindexAt));
        Assert.IsFalse(string.IsNullOrEmpty(response.LastIncrementalIndexAt));
    }

    [TestMethod]
    public async Task GetIndexStats_NoReindexTimes_ReturnsEmptyStrings()
    {
        var stats = new SearchIndexStats
        {
            TotalDocuments = 0,
            DocumentsPerModule = new Dictionary<string, int>(),
            LastFullReindexAt = null,
            LastIncrementalIndexAt = null
        };

        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var response = await _grpcService.GetIndexStats(new GetIndexStatsRequest(), _callContext);

        Assert.IsTrue(response.Success);
        Assert.AreEqual(0, response.TotalDocuments);
        Assert.AreEqual(string.Empty, response.LastFullReindexAt);
    }

    [TestMethod]
    public async Task GetIndexStats_ProviderThrows_ReturnsFailure()
    {
        _searchProviderMock
            .Setup(p => p.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var response = await _grpcService.GetIndexStats(new GetIndexStatsRequest(), _callContext);

        Assert.IsFalse(response.Success);
        Assert.AreEqual("Stats retrieval failed", response.ErrorMessage);
    }

    #endregion

    #region TestServerCallContext helper

    /// <summary>
    /// Minimal <see cref="ServerCallContext"/> stub for unit testing gRPC services.
    /// </summary>
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
