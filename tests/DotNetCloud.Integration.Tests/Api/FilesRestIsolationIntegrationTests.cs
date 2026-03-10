using System.Net;
using System.Net.Http.Json;
using System.Text;
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

    [TestMethod]
    public async Task UploadInitiation_ReportsExistingChunks_ForDedup()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        var firstInitiate = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "dedup-a.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [chunkHash]
            });
        var firstInitiateRoot = await ApiAssert.SuccessAsync(firstInitiate, HttpStatusCode.Created);
        var firstSessionId = DataOrRoot(firstInitiateRoot).GetProperty("sessionId").GetGuid();

        using var firstChunkContent = new ByteArrayContent(payload);
        var firstChunkUpload = await client.PutAsync($"/api/v1/files/upload/{firstSessionId}/chunks/{chunkHash}", firstChunkContent);
        await ApiAssert.SuccessAsync(firstChunkUpload, HttpStatusCode.OK);

        var firstComplete = await client.PostAsync($"/api/v1/files/upload/{firstSessionId}/complete", content: null);
        await ApiAssert.SuccessAsync(firstComplete, HttpStatusCode.OK);

        var secondInitiate = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "dedup-b.bin",
                TotalSize = payload.Length,
                MimeType = "application/octet-stream",
                ChunkHashes = [chunkHash]
            });
        var secondInitiateRoot = await ApiAssert.SuccessAsync(secondInitiate, HttpStatusCode.Created);
        var secondData = DataOrRoot(secondInitiateRoot);

        var existingChunks = secondData.GetProperty("existingChunks");
        var missingChunks = secondData.GetProperty("missingChunks");

        Assert.IsTrue(existingChunks.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(existingChunks.EnumerateArray().Any(v =>
            string.Equals(v.GetString(), chunkHash, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(0, missingChunks.GetArrayLength());
    }

    [TestMethod]
    public async Task ShareLifecycle_CreateUpdateRevoke_WorksForOwner()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createFolderResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "share-lifecycle" });
        var folderRoot = await ApiAssert.SuccessAsync(createFolderResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(folderRoot).GetProperty("id").GetGuid();

        var createShareResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/{nodeId}/shares?userId={userId}",
            new CreateShareDto
            {
                ShareType = "PublicLink",
                Permission = "Read"
            });
        var createShareRoot = await ApiAssert.SuccessAsync(createShareResponse, HttpStatusCode.Created);
        var shareId = DataOrRoot(createShareRoot).GetProperty("id").GetGuid();

        var updateShareResponse = await client.PutAsJsonAsync(
            $"/api/v1/files/{nodeId}/shares/{shareId}?userId={userId}",
            new UpdateShareDto
            {
                Permission = "ReadWrite",
                Note = "updated via integration"
            });
        var updatedShareRoot = await ApiAssert.SuccessAsync(updateShareResponse, HttpStatusCode.OK);
        var updatedShare = DataOrRoot(updatedShareRoot);
        Assert.AreEqual("ReadWrite", updatedShare.GetProperty("permission").GetString());

        var listSharesResponse = await client.GetAsync($"/api/v1/files/{nodeId}/shares?userId={userId}");
        var listSharesRoot = await ApiAssert.SuccessAsync(listSharesResponse, HttpStatusCode.OK);
        var shares = DataOrRoot(listSharesRoot);
        Assert.IsTrue(shares.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(shares.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == shareId));

        var revokeShareResponse = await client.DeleteAsync($"/api/v1/files/{nodeId}/shares/{shareId}?userId={userId}");
        await ApiAssert.SuccessAsync(revokeShareResponse, HttpStatusCode.OK);

        var listAfterRevokeResponse = await client.GetAsync($"/api/v1/files/{nodeId}/shares?userId={userId}");
        var listAfterRevokeRoot = await ApiAssert.SuccessAsync(listAfterRevokeResponse, HttpStatusCode.OK);
        var sharesAfterRevoke = DataOrRoot(listAfterRevokeRoot);
        Assert.IsTrue(sharesAfterRevoke.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsFalse(sharesAfterRevoke.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == shareId));
    }

    [TestMethod]
    public async Task VersionEndpoints_ListGetAndLabel_WorkForUploadedFile()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var payload = new byte[] { 21, 22, 23, 24 };
        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        var initiateResponse = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "versioned.txt",
                TotalSize = payload.Length,
                MimeType = "text/plain",
                ChunkHashes = [chunkHash]
            });
        var initiateRoot = await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetGuid();

        using var chunkContent = new ByteArrayContent(payload);
        var uploadChunkResponse = await client.PutAsync($"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}", chunkContent);
        await ApiAssert.SuccessAsync(uploadChunkResponse, HttpStatusCode.OK);

        var completeResponse = await client.PostAsync($"/api/v1/files/upload/{sessionId}/complete", content: null);
        var completeRoot = await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);
        var nodeId = DataOrRoot(completeRoot).GetProperty("id").GetGuid();

        var listVersionsResponse = await client.GetAsync($"/api/v1/files/{nodeId}/versions?userId={userId}");
        var listVersionsRoot = await ApiAssert.SuccessAsync(listVersionsResponse, HttpStatusCode.OK);
        var versions = DataOrRoot(listVersionsRoot);
        Assert.IsTrue(versions.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(versions.GetArrayLength() >= 1);

        var getVersionResponse = await client.GetAsync($"/api/v1/files/{nodeId}/versions/1?userId={userId}");
        var getVersionRoot = await ApiAssert.SuccessAsync(getVersionResponse, HttpStatusCode.OK);
        var versionOne = DataOrRoot(getVersionRoot);
        Assert.AreEqual(1, versionOne.GetProperty("versionNumber").GetInt32());

        var labelResponse = await client.PutAsJsonAsync(
            $"/api/v1/files/{nodeId}/versions/1/label?userId={userId}",
            new LabelVersionDto { Label = "baseline" });
        var labelRoot = await ApiAssert.SuccessAsync(labelResponse, HttpStatusCode.OK);
        var labeledVersion = DataOrRoot(labelRoot);
        Assert.AreEqual("baseline", labeledVersion.GetProperty("label").GetString());
    }

    [TestMethod]
    public async Task TrashLifecycle_ListSizeAndPurge_WorksForOwner()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "trash-target" });
        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(createRoot).GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/v1/files/{nodeId}");
        await ApiAssert.SuccessAsync(deleteResponse, HttpStatusCode.OK);

        var listTrashResponse = await client.GetAsync($"/api/v1/files/trash?userId={userId}");
        var listTrashRoot = await ApiAssert.SuccessAsync(listTrashResponse, HttpStatusCode.OK);
        var trashItems = DataOrRoot(listTrashRoot);
        Assert.IsTrue(trashItems.ValueKind == System.Text.Json.JsonValueKind.Array);

        var trashSizeResponse = await client.GetAsync($"/api/v1/files/trash/size?userId={userId}");
        var trashSizeRoot = await ApiAssert.SuccessAsync(trashSizeResponse, HttpStatusCode.OK);
        var trashSizeData = DataOrRoot(trashSizeRoot);
        Assert.IsTrue(trashSizeData.TryGetProperty("sizeBytes", out _));

        var purgeResponse = await client.DeleteAsync($"/api/v1/files/trash/{nodeId}?userId={userId}");
        await ApiAssert.SuccessAsync(purgeResponse, HttpStatusCode.OK);

        var restoreAfterPurgeResponse = await client.PostAsync(
            $"/api/v1/files/trash/{nodeId}/restore?userId={userId}",
            content: null);
        await ApiAssert.ErrorAsync(restoreAfterPurgeResponse, HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task WopiFileEndpoints_CheckGetPut_WorkWithGeneratedToken()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var originalBytes = Encoding.UTF8.GetBytes("wopi-original");
        var fileNodeId = await UploadFileAsync(client, "wopi-test.txt", "text/plain", originalBytes);

        var tokenResponse = await client.PostAsync($"/api/v1/wopi/token/{fileNodeId}?userId={userId}", content: null);
        if (tokenResponse.StatusCode == HttpStatusCode.BadRequest)
        {
            var tokenError = await ApiAssert.ErrorAsync(tokenResponse, HttpStatusCode.BadRequest);
            var errorCode = DataOrRoot(tokenError).GetProperty("error").GetProperty("code").GetString();
            Assert.AreEqual("DB_INVALID_OPERATION", errorCode);
            return;
        }

        var tokenRoot = await ApiAssert.SuccessAsync(tokenResponse, HttpStatusCode.OK);
        var token = DataOrRoot(tokenRoot).GetProperty("accessToken").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(token));
        var accessToken = token!;

        var checkResponse = await client.GetAsync($"/api/v1/wopi/files/{fileNodeId}?access_token={Uri.EscapeDataString(accessToken)}");
        var checkRoot = await ApiAssert.SuccessAsync(checkResponse, HttpStatusCode.OK);
        Assert.AreEqual("wopi-test.txt", checkRoot.GetProperty("BaseFileName").GetString());
        Assert.IsTrue(checkRoot.GetProperty("SupportsUpdate").GetBoolean());

        var getResponse = await client.GetAsync($"/api/v1/wopi/files/{fileNodeId}/contents?access_token={Uri.EscapeDataString(accessToken)}");
        ApiAssert.StatusCode(getResponse, HttpStatusCode.OK);
        var downloadedOriginal = await getResponse.Content.ReadAsByteArrayAsync();
        CollectionAssert.AreEqual(originalBytes, downloadedOriginal);

        var updatedBytes = Encoding.UTF8.GetBytes("wopi-updated");
        using var putContent = new ByteArrayContent(updatedBytes);
        var putResponse = await client.PostAsync(
            $"/api/v1/wopi/files/{fileNodeId}/contents?access_token={Uri.EscapeDataString(accessToken)}",
            putContent);

        var putRoot = await ApiAssert.SuccessAsync(putResponse, HttpStatusCode.OK);
        Assert.IsTrue(putRoot.TryGetProperty("LastModifiedTime", out _));

        var getUpdatedResponse = await client.GetAsync($"/api/v1/wopi/files/{fileNodeId}/contents?access_token={Uri.EscapeDataString(accessToken)}");
        ApiAssert.StatusCode(getUpdatedResponse, HttpStatusCode.OK);
        var downloadedUpdated = await getUpdatedResponse.Content.ReadAsByteArrayAsync();
        CollectionAssert.AreEqual(updatedBytes, downloadedUpdated);
    }

    [TestMethod]
    public async Task VersionRestore_RestoresPreviousContent()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var initialBytes = Encoding.UTF8.GetBytes("version-one");
        var fileNodeId = await UploadFileAsync(client, "restore-target.txt", "text/plain", initialBytes);

        var versionsResponse = await client.GetAsync($"/api/v1/files/{fileNodeId}/versions?userId={userId}");
        var versionsRoot = await ApiAssert.SuccessAsync(versionsResponse, HttpStatusCode.OK);
        var versions = DataOrRoot(versionsRoot);
        Assert.IsTrue(versions.ValueKind == System.Text.Json.JsonValueKind.Array);
        Assert.IsTrue(versions.GetArrayLength() >= 1);

        var restoreResponse = await client.PostAsync($"/api/v1/files/{fileNodeId}/versions/1/restore?userId={userId}", content: null);
        await ApiAssert.SuccessAsync(restoreResponse, HttpStatusCode.OK);

        var downloadRestoredResponse = await client.GetAsync($"/api/v1/files/{fileNodeId}/download");
        ApiAssert.StatusCode(downloadRestoredResponse, HttpStatusCode.OK);
        var restoredBytes = await downloadRestoredResponse.Content.ReadAsByteArrayAsync();
        CollectionAssert.AreEqual(initialBytes, restoredBytes);
    }

    [TestMethod]
    public async Task TrashRestore_WorkflowRestoresNodeVisibility()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "restore-me" });
        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(createRoot).GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/v1/files/{nodeId}");
        await ApiAssert.SuccessAsync(deleteResponse, HttpStatusCode.OK);

        var getDeletedResponse = await client.GetAsync($"/api/v1/files/{nodeId}");
        await ApiAssert.ErrorAsync(getDeletedResponse, HttpStatusCode.NotFound);

        var restoreResponse = await client.PostAsync($"/api/v1/files/trash/{nodeId}/restore?userId={userId}", content: null);
        await ApiAssert.SuccessAsync(restoreResponse, HttpStatusCode.OK);

        var getRestoredResponse = await client.GetAsync($"/api/v1/files/{nodeId}");
        var restoredRoot = await ApiAssert.SuccessAsync(getRestoredResponse, HttpStatusCode.OK);
        Assert.AreEqual(nodeId, DataOrRoot(restoredRoot).GetProperty("id").GetGuid());
    }

    [TestMethod]
    public async Task PublicShare_WithPassword_RequiresPasswordAndResolvesWithCorrectPassword()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto { Name = "public-share-password" });
        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        var nodeId = DataOrRoot(createRoot).GetProperty("id").GetGuid();

        var createShareResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/{nodeId}/shares?userId={userId}",
            new CreateShareDto
            {
                ShareType = "PublicLink",
                Permission = "Read",
                LinkPassword = "P@ssw0rd!"
            });
        var createShareRoot = await ApiAssert.SuccessAsync(createShareResponse, HttpStatusCode.Created);
        var linkToken = DataOrRoot(createShareRoot).GetProperty("linkToken").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(linkToken));
        var publicLinkToken = linkToken!;

        var resolveMissingPassword = await client.GetAsync($"/api/v1/public/shares/{publicLinkToken}");
        await ApiAssert.ErrorAsync(resolveMissingPassword, HttpStatusCode.NotFound);

        var resolveWrongPassword = await client.GetAsync($"/api/v1/public/shares/{publicLinkToken}?password=wrong");
        await ApiAssert.ErrorAsync(resolveWrongPassword, HttpStatusCode.NotFound);

        var resolveCorrectPassword = await client.GetAsync($"/api/v1/public/shares/{publicLinkToken}?password={Uri.EscapeDataString("P@ssw0rd!")}");
        var resolveRoot = await ApiAssert.SuccessAsync(resolveCorrectPassword, HttpStatusCode.OK);
        Assert.AreEqual(publicLinkToken, DataOrRoot(resolveRoot).GetProperty("linkToken").GetString());
    }

    [TestMethod]
    public async Task BulkOperations_MoveCopyDeleteAndPermanentDelete_ReturnExpectedCounts()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var sourceRootId = await CreateFolderAsync(client, "bulk-source");
        var targetRootId = await CreateFolderAsync(client, "bulk-target");

        var childAId = await UploadFileAsync(client, "bulk-file-a.txt", "text/plain", Encoding.UTF8.GetBytes("bulk-a"), sourceRootId);
        var childBId = await CreateFolderAsync(client, "child-b", sourceRootId);

        var moveResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/bulk/move?userId={userId}",
            new BulkOperationDto
            {
                NodeIds = [childAId],
                TargetParentId = targetRootId
            });
        var moveRoot = await ApiAssert.SuccessAsync(moveResponse, HttpStatusCode.OK);
        var moveData = DataOrRoot(moveRoot);
        Assert.AreEqual(1, moveData.GetProperty("successCount").GetInt32());

        var copyResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/bulk/copy?userId={userId}",
            new BulkOperationDto
            {
                NodeIds = [childAId],
                TargetParentId = targetRootId
            });
        var copyRoot = await ApiAssert.SuccessAsync(copyResponse, HttpStatusCode.OK);
        var copyData = DataOrRoot(copyRoot);
        Assert.AreEqual(1, copyData.GetProperty("successCount").GetInt32());

        var deleteResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/bulk/delete?userId={userId}",
            new BulkOperationDto
            {
                NodeIds = [childBId]
            });
        var deleteRoot = await ApiAssert.SuccessAsync(deleteResponse, HttpStatusCode.OK);
        var deleteData = DataOrRoot(deleteRoot);
        Assert.AreEqual(1, deleteData.GetProperty("successCount").GetInt32());

        var permanentDeleteResponse = await client.PostAsJsonAsync(
            $"/api/v1/files/bulk/permanent-delete?userId={userId}",
            new BulkOperationDto
            {
                NodeIds = [childBId]
            });
        var permanentDeleteRoot = await ApiAssert.SuccessAsync(permanentDeleteResponse, HttpStatusCode.OK);
        var permanentDeleteData = DataOrRoot(permanentDeleteRoot);
        Assert.AreEqual(1, permanentDeleteData.GetProperty("successCount").GetInt32());
    }

    private static async Task<Guid> UploadFileAsync(HttpClient client, string fileName, string mimeType, byte[] payload, Guid? parentId = null)
    {
        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(payload);

        var initiateResponse = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = fileName,
                ParentId = parentId,
                TotalSize = payload.Length,
                MimeType = mimeType,
                ChunkHashes = [chunkHash]
            });
        var initiateRoot = await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetGuid();

        using (var chunkContent = new ByteArrayContent(payload))
        {
            var uploadChunkResponse = await client.PutAsync($"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}", chunkContent);
            await ApiAssert.SuccessAsync(uploadChunkResponse, HttpStatusCode.OK);
        }

        var completeResponse = await client.PostAsync($"/api/v1/files/upload/{sessionId}/complete", content: null);
        var completeRoot = await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);
        return DataOrRoot(completeRoot).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateFolderAsync(HttpClient client, string name, Guid? parentId = null)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/files/folders",
            new CreateFolderDto
            {
                Name = name,
                ParentId = parentId
            });
        var createRoot = await ApiAssert.SuccessAsync(createResponse, HttpStatusCode.Created);
        return DataOrRoot(createRoot).GetProperty("id").GetGuid();
    }
}
