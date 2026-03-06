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
        var nodeId = createRoot.GetProperty("data").GetProperty("id").GetString();
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
        var sessionId = initiateRoot.GetProperty("data").GetProperty("sessionId").GetString();
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
        var nodeName = completeRoot.GetProperty("data").GetProperty("name").GetString();
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
        var nodeId = createRoot.GetProperty("data").GetProperty("id").GetString();
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
}
