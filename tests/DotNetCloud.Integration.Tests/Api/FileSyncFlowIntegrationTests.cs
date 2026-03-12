using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the Files module sync workflow.
/// Tests uploading a file via chunked API → verification in tree/changes → reconciliation.
/// Runs against the standalone Files module process via FilesHostWebApplicationFactory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class FileSyncFlowIntegrationTests
{
    private static FilesHostWebApplicationFactory _factory = null!;
    private static readonly Guid UserId = Guid.NewGuid();

    private static System.Text.Json.JsonElement DataOrRoot(System.Text.Json.JsonElement root)
    {
        var current = root;

        while (current.ValueKind == System.Text.Json.JsonValueKind.Object &&
               current.TryGetProperty("data", out var nested))
        {
            current = nested;
        }

        return current;
    }

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new FilesHostWebApplicationFactory();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory.Dispose();
    }

    [TestMethod]
    public async Task Upload_ViaChunkedApi_AppearsInTree()
    {
        using var client = _factory.CreateAuthenticatedApiClient(UserId);

        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        // Initiate upload
        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/upload/initiate?userId={UserId}",
            new InitiateUploadDto
            {
                FileName = "sync-test-file.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [chunkHash]
            });

        await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var initiateRoot = System.Text.Json.JsonDocument.Parse(
            await initiateResponse.Content.ReadAsStringAsync()).RootElement;
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(sessionId));

        // Upload chunk
        using var chunkContent = new ByteArrayContent(payload);
        var uploadResponse = await client.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}?userId={UserId}",
            chunkContent);
        await ApiAssert.SuccessAsync(uploadResponse, HttpStatusCode.OK);

        // Complete upload
        var completeResponse = await client.PostAsync(
            $"/api/v1/files/upload/{sessionId}/complete?userId={UserId}",
            content: null);
        await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);

        var completeRoot = System.Text.Json.JsonDocument.Parse(
            await completeResponse.Content.ReadAsStringAsync()).RootElement;
        var fileNodeId = DataOrRoot(completeRoot).GetProperty("id").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(fileNodeId));

        // Verify file appears in tree listing
        var listResponse = await client.GetAsync($"/api/v1/files?userId={UserId}");
        await ApiAssert.SuccessAsync(listResponse, HttpStatusCode.OK);

        var listRoot = System.Text.Json.JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync()).RootElement;
        var nodes = DataOrRoot(listRoot);

        // Look for the uploaded file
        var foundFile = false;
        if (nodes.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.GetProperty("id").GetString() == fileNodeId)
                {
                    foundFile = true;
                    Assert.AreEqual("sync-test-file.bin", node.GetProperty("name").GetString());
                    break;
                }
            }
        }

        Assert.IsTrue(foundFile, "Uploaded file should appear in tree listing");
    }

    [TestMethod]
    public async Task Upload_MultipleChunks_CombinesSuccessfully()
    {
        using var client = _factory.CreateAuthenticatedApiClient(UserId);

        var chunk1 = new byte[] { 1, 2, 3, 4, 5 };
        var chunk2 = new byte[] { 6, 7, 8, 9, 10 };
        var hash1 = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(chunk1);
        var hash2 = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(chunk2);

        // Initiate upload with 2 chunks
        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/upload/initiate?userId={UserId}",
            new InitiateUploadDto
            {
                FileName = "multi-chunk.bin",
                TotalSize = chunk1.Length + chunk2.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [hash1, hash2]
            });

        await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var initiateRoot = System.Text.Json.JsonDocument.Parse(
            await initiateResponse.Content.ReadAsStringAsync()).RootElement;
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetString();

        // Upload both chunks
        using var content1 = new ByteArrayContent(chunk1);
        var upload1Response = await client.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{hash1}?userId={UserId}",
            content1);
        await ApiAssert.SuccessAsync(upload1Response, HttpStatusCode.OK);

        using var content2 = new ByteArrayContent(chunk2);
        var upload2Response = await client.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{hash2}?userId={UserId}",
            content2);
        await ApiAssert.SuccessAsync(upload2Response, HttpStatusCode.OK);

        // Complete upload
        var completeResponse = await client.PostAsync(
            $"/api/v1/files/upload/{sessionId}/complete?userId={UserId}",
            content: null);
        await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);

        var completeRoot = System.Text.Json.JsonDocument.Parse(
            await completeResponse.Content.ReadAsStringAsync()).RootElement;
        var fileName = DataOrRoot(completeRoot).GetProperty("name").GetString();
        Assert.AreEqual("multi-chunk.bin", fileName);
    }

    [TestMethod]
    public async Task Upload_TracksChangeSets_ForSync()
    {
        using var client = _factory.CreateAuthenticatedApiClient(UserId);

        // Upload a file
        var payload = new byte[] { 42 };
        var hash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        var initiateResponse = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "change-track.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [hash]
            });

        var initiateRoot = System.Text.Json.JsonDocument.Parse(
            await initiateResponse.Content.ReadAsStringAsync()).RootElement;
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetString();

        using var content = new ByteArrayContent(payload);
        await client.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{hash}",
            content);

        await client.PostAsync(
            $"/api/v1/files/upload/{sessionId}/complete",
            content: null);

        // Get changes since epoch — should include the uploaded file
        var changesResponse = await client.GetAsync(
            $"/api/v1/files/sync/changes?since={DateTime.UtcNow.AddMinutes(-5):O}");
        await ApiAssert.SuccessAsync(changesResponse, HttpStatusCode.OK);

        var changesRoot = System.Text.Json.JsonDocument.Parse(
            await changesResponse.Content.ReadAsStringAsync()).RootElement;
        var data = DataOrRoot(changesRoot);

        // The response is an array of SyncChangeDto
        Assert.AreEqual(System.Text.Json.JsonValueKind.Array, data.ValueKind,
            "Expected an array of sync changes");

        var foundChange = false;
        foreach (var change in data.EnumerateArray())
        {
            if (change.GetProperty("name").GetString() == "change-track.bin")
            {
                foundChange = true;
                break;
            }
        }

        Assert.IsTrue(foundChange, "Changes list should include the newly created file");
    }

    [TestMethod]
    public async Task Upload_AppearsInSyncTree()
    {
        using var client = _factory.CreateAuthenticatedApiClient(UserId);

        // Upload a file
        var payload = new byte[] { 99 };
        var hash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        var initiateResponse = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "tree-test.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [hash]
            });

        var initiateRoot = System.Text.Json.JsonDocument.Parse(
            await initiateResponse.Content.ReadAsStringAsync()).RootElement;
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetString();

        using var content = new ByteArrayContent(payload);
        await client.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{hash}",
            content);

        await client.PostAsync(
            $"/api/v1/files/upload/{sessionId}/complete",
            content: null);

        // Verify the uploaded file appears in the sync tree
        var treeResponse = await client.GetAsync("/api/v1/files/sync/tree");
        await ApiAssert.SuccessAsync(treeResponse, HttpStatusCode.OK);

        var treeRoot = System.Text.Json.JsonDocument.Parse(
            await treeResponse.Content.ReadAsStringAsync()).RootElement;
        var treeData = DataOrRoot(treeRoot);

        // The tree endpoint returns a single SyncTreeNodeDto (virtual root) with children
        var foundInTree = false;
        if (treeData.TryGetProperty("children", out var children) &&
            children.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var node in children.EnumerateArray())
            {
                if (node.GetProperty("name").GetString() == "tree-test.bin")
                {
                    foundInTree = true;
                    break;
                }
            }
        }

        Assert.IsTrue(foundInTree, "Uploaded file should appear in the sync tree");
    }
}
