using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// REST integration tests for Files module isolation and core workflows.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class FilesRestIsolationIntegrationTests
{
    private static FilesHostWebApplicationFactory _factory = null!;

    private static System.Text.Json.JsonElement DataOrRoot(System.Text.Json.JsonElement root)
    {
        return root.ValueKind == System.Text.Json.JsonValueKind.Object &&
               root.TryGetProperty("data", out var data)
            ? data
            : root;
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
    public async Task FileCrud_OtherUserCannotAccessNode()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using var ownerClient = _factory.CreateAuthenticatedApiClient(ownerId);
        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/files/folders?userId={ownerId}",
            new CreateFolderDto { Name = "private-folder" });

        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(createRoot).GetProperty("id").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(nodeId));

        using var otherClient = _factory.CreateAuthenticatedApiClient(otherUserId);
        var getResponse = await otherClient.GetAsync($"/api/v1/files/{nodeId}?userId={otherUserId}");

        await ApiAssert.ErrorAsync(getResponse, HttpStatusCode.NotFound);

        var renameResponse = await otherClient.PutAsJsonAsync(
            $"/api/v1/files/{nodeId}/rename?userId={otherUserId}",
            new RenameNodeDto { Name = "stolen-name" });

        await ApiAssert.ErrorAsync(renameResponse, HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task UploadWorkflow_AndCrossUserSessionIsolation_Works()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var payload = new byte[] { 10, 20, 30, 40 };
        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        using var ownerClient = _factory.CreateAuthenticatedApiClient(ownerId);
        var initiateResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/files/upload/initiate?userId={ownerId}",
            new InitiateUploadDto
            {
                FileName = "isolation.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [chunkHash]
            });

        var initiateRoot = await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(sessionId));

        using var otherClient = _factory.CreateAuthenticatedApiClient(otherUserId);
        using var foreignChunk = new ByteArrayContent(payload);
        var foreignUploadResponse = await otherClient.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}?userId={otherUserId}",
            foreignChunk);

        await ApiAssert.ErrorAsync(foreignUploadResponse, HttpStatusCode.Forbidden);

        using var ownChunk = new ByteArrayContent(payload);
        var ownUploadResponse = await ownerClient.PutAsync(
            $"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}?userId={ownerId}",
            ownChunk);
        await ApiAssert.SuccessAsync(ownUploadResponse, HttpStatusCode.OK);

        var completeResponse = await ownerClient.PostAsync(
            $"/api/v1/files/upload/{sessionId}/complete?userId={ownerId}",
            content: null);

        var completeRoot = await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);
        var nodeName = DataOrRoot(completeRoot).GetProperty("name").GetString();
        Assert.AreEqual("isolation.bin", nodeName);
    }

    [TestMethod]
    public async Task ShareAndTrashFlows_AreOwnerScoped()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        using var ownerClient = _factory.CreateAuthenticatedApiClient(ownerId);
        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/files/folders?userId={ownerId}",
            new CreateFolderDto { Name = "share-me" });

        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(createRoot).GetProperty("id").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(nodeId));

        var shareResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/files/{nodeId}/shares?userId={ownerId}",
            new CreateShareDto
            {
                ShareType = "PublicLink",
                Permission = "Read"
            });

        await ApiAssert.SuccessAsync(shareResponse, HttpStatusCode.Created);

        using var otherClient = _factory.CreateAuthenticatedApiClient(otherUserId);
        var listSharesResponse = await otherClient.GetAsync($"/api/v1/files/{nodeId}/shares?userId={otherUserId}");
        await ApiAssert.ErrorAsync(listSharesResponse, HttpStatusCode.Forbidden);

        var deleteResponse = await ownerClient.DeleteAsync($"/api/v1/files/{nodeId}?userId={ownerId}");
        await ApiAssert.SuccessAsync(deleteResponse, HttpStatusCode.OK);

        var otherRestoreResponse = await otherClient.PostAsync(
            $"/api/v1/files/trash/{nodeId}/restore?userId={otherUserId}",
            content: null);
        await ApiAssert.ErrorAsync(otherRestoreResponse, HttpStatusCode.Forbidden);

        var ownerRestoreResponse = await ownerClient.PostAsync(
            $"/api/v1/files/trash/{nodeId}/restore?userId={ownerId}",
            content: null);
        await ApiAssert.SuccessAsync(ownerRestoreResponse, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task QuotaExceeded_BlocksUploadInitiation()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var setQuotaResponse = await client.PutAsJsonAsync(
            $"/api/v1/files/quota/{userId}?userId={userId}",
            new SetQuotaDto { MaxBytes = 1 });
        await ApiAssert.SuccessAsync(setQuotaResponse, HttpStatusCode.OK);

        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/upload/initiate?userId={userId}",
            new InitiateUploadDto
            {
                FileName = "too-large.bin",
                TotalSize = 16,
                MimeType = "application/octet-stream",
                ChunkHashes = ["abc"]
            });

        await ApiAssert.ErrorAsync(initiateResponse, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task FileListSearchFavoritesAndRecent_WorkForOwner()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createAlpha = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "alpha-docs" });
        var alphaRoot = await ApiAssert.SuccessAsync(createAlpha, HttpStatusCode.Created);
        var alphaNodeId = DataOrRoot(alphaRoot).GetProperty("id").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(alphaNodeId));

        var createBeta = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "beta-assets" });
        await ApiAssert.SuccessAsync(createBeta, HttpStatusCode.Created);

        var listRootResponse = await client.GetAsync("/api/v1/files");
        var listRoot = await ApiAssert.SuccessAsync(listRootResponse, HttpStatusCode.OK);
        var listData = DataOrRoot(listRoot);
        Assert.IsTrue(listData.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(listData.GetArrayLength() >= 2);

        var searchResponse = await client.GetAsync("/api/v1/files/search?query=alpha&page=1&pageSize=10");
        var searchRoot = await ApiAssert.SuccessAsync(searchResponse, HttpStatusCode.OK);
        var searchItems = DataOrRoot(searchRoot).GetProperty("items");
        Assert.IsTrue(searchItems.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(searchItems.GetArrayLength() >= 1);

        var toggleFavoriteResponse = await client.PostAsync($"/api/v1/files/{alphaNodeId}/favorite", content: null);
        await ApiAssert.SuccessAsync(toggleFavoriteResponse, HttpStatusCode.OK);

        var favoritesResponse = await client.GetAsync("/api/v1/files/favorites");
        var favoritesRoot = await ApiAssert.SuccessAsync(favoritesResponse, HttpStatusCode.OK);
        var favoritesData = DataOrRoot(favoritesRoot);
        Assert.IsTrue(favoritesData.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(favoritesData.EnumerateArray().Any(n =>
            n.TryGetProperty("id", out var idProp) &&
            string.Equals(idProp.GetString(), alphaNodeId, StringComparison.OrdinalIgnoreCase)));

        var recentResponse = await client.GetAsync("/api/v1/files/recent?count=10");
        var recentRoot = await ApiAssert.SuccessAsync(recentResponse, HttpStatusCode.OK);
        var recentData = DataOrRoot(recentRoot);
        Assert.IsTrue(recentData.ValueKind == System.Text.Json.JsonValueKind.Array);
    }

    [TestMethod]
    public async Task SyncEndpoints_TreeChangesAndReconcile_ReturnSuccess()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createFolderResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "sync-root" });
        await ApiAssert.SuccessAsync(createFolderResponse, HttpStatusCode.Created);

        var treeResponse = await client.GetAsync("/api/v1/files/sync/tree");
        var treeRoot = await ApiAssert.SuccessAsync(treeResponse, HttpStatusCode.OK);
        var treeData = DataOrRoot(treeRoot);
        Assert.IsTrue(treeData.ValueKind is System.Text.Json.JsonValueKind.Array or System.Text.Json.JsonValueKind.Object);

        var since = Uri.EscapeDataString(DateTime.UtcNow.AddHours(-1).ToString("o"));
        var changesResponse = await client.GetAsync($"/api/v1/files/sync/changes?since={since}");
        var changesRoot = await ApiAssert.SuccessAsync(changesResponse, HttpStatusCode.OK);
        var changesData = DataOrRoot(changesRoot);
        Assert.IsTrue(changesData.ValueKind is System.Text.Json.JsonValueKind.Array or System.Text.Json.JsonValueKind.Object);

        var reconcileResponse = await client.PostAsJsonAsync(
            "/api/v1/files/sync/reconcile",
            new SyncReconcileRequestDto
            {
                ClientNodes = []
            });
        var reconcileRoot = await ApiAssert.SuccessAsync(reconcileResponse, HttpStatusCode.OK);
        var actions = DataOrRoot(reconcileRoot).GetProperty("actions");
        Assert.IsTrue(actions.ValueKind == System.Text.Json.JsonValueKind.Array);
    }

    [TestMethod]
    public async Task WopiDiscoveryEndpoints_ReturnExpectedShape()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var discoveryResponse = await client.GetAsync("/api/v1/wopi/discovery");
        var discoveryRoot = await ApiAssert.SuccessAsync(discoveryResponse, HttpStatusCode.OK);
        var discoveryData = DataOrRoot(discoveryRoot);
        Assert.IsTrue(discoveryData.TryGetProperty("available", out _));
        Assert.IsTrue(discoveryData.TryGetProperty("supportedExtensions", out var supportedExtensions));
        Assert.IsTrue(supportedExtensions.ValueKind == System.Text.Json.JsonValueKind.Array);

        var supportsResponse = await client.GetAsync("/api/v1/wopi/discovery/supports/docx");
        var supportsRoot = await ApiAssert.SuccessAsync(supportsResponse, HttpStatusCode.OK);
        var supportsData = DataOrRoot(supportsRoot);
        Assert.AreEqual("docx", supportsData.GetProperty("extension").GetString());
        Assert.IsTrue(supportsData.TryGetProperty("supported", out _));
    }
}
