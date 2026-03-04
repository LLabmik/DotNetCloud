using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Client.Core.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Client.Core.Tests.Api;

[TestClass]
public class DotNetCloudApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static HttpClient CreateMockHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => handler(req));

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://cloud.example.com/") };
    }

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value, options: JsonOptions),
        };

    // ── GetNodeAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetNodeAsync_ValidId_ReturnsNode()
    {
        var nodeId = Guid.NewGuid();
        var expected = new FileNodeResponse { Id = nodeId, Name = "test.txt", NodeType = "File" };

        var client = CreateMockHttpClient(_ => JsonOk(expected));
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetNodeAsync(nodeId);

        Assert.AreEqual(nodeId, result.Id);
        Assert.AreEqual("test.txt", result.Name);
    }

    // ── ListChildrenAsync ───────────────────────────────────────────────────

    [TestMethod]
    public async Task ListChildrenAsync_NullFolder_CallsRootEndpoint()
    {
        var capturedPath = string.Empty;
        var client = CreateMockHttpClient(req =>
        {
            capturedPath = req.RequestUri?.PathAndQuery ?? "";
            return JsonOk(new List<FileNodeResponse>());
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        await apiClient.ListChildrenAsync(null);

        StringAssert.Contains(capturedPath, "root/children");
    }

    [TestMethod]
    public async Task ListChildrenAsync_WithFolderId_IncludesFolderIdInPath()
    {
        var folderId = Guid.NewGuid();
        var capturedPath = string.Empty;
        var client = CreateMockHttpClient(req =>
        {
            capturedPath = req.RequestUri?.PathAndQuery ?? "";
            return JsonOk(new List<FileNodeResponse>());
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        await apiClient.ListChildrenAsync(folderId);

        StringAssert.Contains(capturedPath, folderId.ToString());
    }

    // ── AccessToken ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AccessToken_WhenSet_SentAsAuthorizationHeader()
    {
        string? capturedAuth = null;
        var client = CreateMockHttpClient(req =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            return JsonOk(new FileNodeResponse { Id = Guid.NewGuid(), Name = "f", NodeType = "File" });
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance)
        {
            AccessToken = "my-token-123",
        };

        await apiClient.GetNodeAsync(Guid.NewGuid());

        Assert.AreEqual("my-token-123", capturedAuth);
    }

    // ── GetChangesSinceAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task GetChangesSinceAsync_ReturnsChanges()
    {
        var changes = new List<SyncChangeResponse>
        {
            new() { NodeId = Guid.NewGuid(), Name = "doc.txt", NodeType = "File", UpdatedAt = DateTime.UtcNow },
        };
        var client = CreateMockHttpClient(_ => JsonOk(changes));
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetChangesSinceAsync(DateTime.UtcNow.AddHours(-1), null);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("doc.txt", result[0].Name);
    }

    // ── GetQuotaAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetQuotaAsync_ReturnsQuota()
    {
        var quota = new QuotaResponse { UserId = Guid.NewGuid(), QuotaBytes = 10_737_418_240, UsedBytes = 1_073_741_824 };
        var client = CreateMockHttpClient(_ => JsonOk(quota));
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetQuotaAsync();

        Assert.AreEqual(10L, result.QuotaBytes / 1_073_741_824);
        Assert.IsTrue(result.PercentUsed > 0);
    }

    // ── Retry on 5xx ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetNodeAsync_500ThenOk_RetriesAndSucceeds()
    {
        var callCount = 0;
        var nodeId = Guid.NewGuid();
        var client = CreateMockHttpClient(_ =>
        {
            callCount++;
            return callCount < 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : JsonOk(new FileNodeResponse { Id = nodeId, Name = "retry.txt", NodeType = "File" });
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetNodeAsync(nodeId);

        Assert.AreEqual(2, callCount);
        Assert.AreEqual("retry.txt", result.Name);
    }
}
