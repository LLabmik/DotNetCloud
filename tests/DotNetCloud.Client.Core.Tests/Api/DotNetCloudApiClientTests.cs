using System.IO.Compression;
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

    [TestMethod]
    public async Task GetChangesSinceAsync_CursorBased_SendsCursorAndDeserializesPagedResponse()
    {
        const string cursor = "dXNlcjoxMjM=";
        var pagedResponse = new PagedSyncChangesResponse
        {
            Changes = [new SyncChangeResponse { NodeId = Guid.NewGuid(), Name = "file.txt", NodeType = "File", UpdatedAt = DateTime.UtcNow }],
            NextCursor = "bmV4dEN1cnNvcg==",
            HasMore = true,
        };

        string? capturedUrl = null;
        var client = CreateMockHttpClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return JsonOk(pagedResponse);
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        var result = await apiClient.GetChangesSinceAsync(cursor, limit: 100);

        // Verify cursor and limit were sent in the query string
        Assert.IsNotNull(capturedUrl);
        Assert.IsTrue(capturedUrl.Contains("limit=100"), $"Expected 'limit=100' in URL: {capturedUrl}");
        Assert.IsTrue(capturedUrl.Contains(Uri.EscapeDataString(cursor)), $"Expected cursor in URL: {capturedUrl}");

        // Verify response deserialized correctly
        Assert.AreEqual(1, result.Changes.Count);
        Assert.AreEqual("bmV4dEN1cnNvcg==", result.NextCursor);
        Assert.IsTrue(result.HasMore);
    }

    [TestMethod]
    public async Task GetChangesSinceAsync_NullCursor_OmitsCursorFromQuery()
    {
        var pagedResponse = new PagedSyncChangesResponse
        {
            Changes = [],
            NextCursor = "firstCursor",
            HasMore = false,
        };

        string? capturedUrl = null;
        var client = CreateMockHttpClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return JsonOk(pagedResponse);
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        await apiClient.GetChangesSinceAsync(cursor: null);

        Assert.IsNotNull(capturedUrl);
        Assert.IsFalse(capturedUrl.Contains("cursor="), $"Expected no cursor in URL for null cursor: {capturedUrl}");
        Assert.IsTrue(capturedUrl.Contains("limit="), $"Expected limit in URL: {capturedUrl}");
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

    // ── Compression (Task 2.3) ──────────────────────────────────────────────

    [TestMethod]
    public async Task UploadChunkAsync_SetsContentEncodingGzip()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var chunkData = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
        string? capturedEncoding = null;
        byte[]? capturedBody = null;

        // Capture inside the mock handler — before the request is disposed
        var client = CreateMockHttpClient(req =>
        {
            capturedEncoding = req.Content?.Headers.ContentEncoding.FirstOrDefault();
            capturedBody = req.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        // Act
        await apiClient.UploadChunkAsync(sessionId, 0, "fakehash", new MemoryStream(chunkData));

        // Assert: Content-Encoding header is "gzip"
        Assert.AreEqual("gzip", capturedEncoding, "Content-Encoding should be 'gzip'.");

        // Assert: decompressing the body yields the original bytes
        Assert.IsNotNull(capturedBody, "Request body should not be null.");
        using var ms = new MemoryStream(capturedBody!);
        await using var gz = new GZipStream(ms, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        await gz.CopyToAsync(decompressed);
        CollectionAssert.AreEqual(chunkData, decompressed.ToArray(),
            "Decompressed body should match the original chunk data.");
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_DecompressesGzipResponse()
    {
        // Arrange: raw bytes to serve, compressed as the server would send them
        var rawBytes = new byte[] { 11, 22, 33, 44, 55, 66, 77, 88 };

        var compressed = new MemoryStream();
        await using (var gz = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            await gz.WriteAsync(rawBytes);
        var compressedBytes = compressed.ToArray();

        // Use a DelegatingHandler that decompresses gzip responses — simulates HttpClientHandler.AutomaticDecompression
        var inner = new Mock<HttpMessageHandler>();
        inner.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(compressedBytes),
                };
                resp.Content.Headers.ContentEncoding.Add("gzip");
                return resp;
            });

        var httpClient = new HttpClient(new GzipDecompressionHandler(inner.Object))
        {
            BaseAddress = new Uri("https://cloud.example.com/"),
        };
        var apiClient = new DotNetCloudApiClient(httpClient, NullLogger<DotNetCloudApiClient>.Instance);

        // Act
        var resultStream = await apiClient.DownloadChunkByHashAsync("abc123");
        using var ms = new MemoryStream();
        await resultStream.CopyToAsync(ms);

        // Assert: decompressed content matches original bytes
        CollectionAssert.AreEqual(rawBytes, ms.ToArray(),
            "DownloadChunkByHashAsync should return the decompressed content.");
    }

    /// <summary>
    /// Simulates the decompression that <see cref="System.Net.Http.HttpClientHandler"/> performs
    /// when <c>AutomaticDecompression = DecompressionMethods.All</c> is set.
    /// </summary>
    private sealed class GzipDecompressionHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (!response.Content.Headers.ContentEncoding.Contains("gzip"))
                return response;

            var decompressedMs = new MemoryStream();
            await using (var gz = new GZipStream(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                CompressionMode.Decompress))
            {
                await gz.CopyToAsync(decompressedMs, cancellationToken);
            }

            decompressedMs.Position = 0;
            var newContent = new StreamContent(decompressedMs);
            foreach (var header in response.Content.Headers)
            {
                if (!string.Equals(header.Key, "Content-Encoding", StringComparison.OrdinalIgnoreCase))
                    newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = newContent;
            return response;
        }
    }

    // ── NameConflictException (Issue #41) ───────────────────────────────────

    [TestMethod]
    public async Task CreateFolderAsync_409NameConflict_ThrowsNameConflictException()
    {
        var errorJson = """{"code":"NAME_CONFLICT","message":"A folder named 'Reports' already exists.","requestId":"test-rid","timestamp":"2026-01-01T00:00:00Z"}""";
        var client = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json"),
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        NameConflictException? caught = null;
        try { await apiClient.CreateFolderAsync("Reports", null); }
        catch (NameConflictException ex) { caught = ex; }

        Assert.IsNotNull(caught, "Expected NameConflictException but none was thrown.");
        StringAssert.Contains(caught!.Message, "Reports");
    }

    [TestMethod]
    public async Task InitiateUploadAsync_409NameConflict_ThrowsNameConflictException()
    {
        var errorJson = """{"code":"NAME_CONFLICT","message":"A file named 'report.pdf' conflicts with 'Report.pdf'.","requestId":"test-rid","timestamp":"2026-01-01T00:00:00Z"}""";
        var client = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json"),
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        NameConflictException? caught = null;
        try { await apiClient.InitiateUploadAsync("report.pdf", null, 100, null, ["hash1"]); }
        catch (NameConflictException ex) { caught = ex; }

        Assert.IsNotNull(caught, "Expected NameConflictException but none was thrown.");
    }

    [TestMethod]
    public async Task PostJsonAsync_409WithOtherCode_ThrowsHttpRequestException()
    {
        // A 409 with a different error code should NOT throw NameConflictException.
        var errorJson = """{"code":"INVALID_OPERATION","message":"Some other conflict."}""";
        var client = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json"),
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        // Should throw HttpRequestException (from EnsureSuccessStatusCode), not NameConflictException.
        HttpRequestException? caught = null;
        try { await apiClient.CreateFolderAsync("test", null); }
        catch (HttpRequestException ex) { caught = ex; }

        Assert.IsNotNull(caught, "Expected HttpRequestException but none was thrown.");
    }

    // ── POSIX metadata (Issue #42) ──────────────────────────────────────────

    [TestMethod]
    public async Task InitiateUploadAsync_IncludesPosixModeInRequestBody()
    {
        string? capturedBody = null;
        var responseBody = JsonSerializer.Serialize(new
        {
            data = new { sessionId = Guid.NewGuid(), expiresAt = DateTime.UtcNow, presentChunks = Array.Empty<string>() },
        }, JsonOptions);

        var client = CreateMockHttpClient(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
            };
        });
        var apiClient = new DotNetCloudApiClient(client, NullLogger<DotNetCloudApiClient>.Instance);

        await apiClient.InitiateUploadAsync("file.txt", null, 100, null, ["hash1"], null,
            posixMode: 493, posixOwnerHint: "alice:staff");

        Assert.IsNotNull(capturedBody);
        StringAssert.Contains(capturedBody, "493");
        StringAssert.Contains(capturedBody, "alice:staff");
    }
}
