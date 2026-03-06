using System.Security.Cryptography;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.Host.Protos;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for Files gRPC tenant-isolation hardening.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class FilesGrpcIsolationIntegrationTests
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
    public async Task GetNode_OtherUserCannotReadNode_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var ownerClient = _factory.CreateFilesClient(ownerId);
        var createResponse = await ownerClient.CreateFolderAsync(new CreateFolderRequest
        {
            Name = "private-folder",
            UserId = ownerId.ToString(),
        });

        Assert.IsTrue(createResponse.Success, createResponse.ErrorMessage);

        var attackerClient = _factory.CreateFilesClient(otherUserId);
        var response = await attackerClient.GetNodeAsync(new GetNodeRequest
        {
            NodeId = createResponse.Node.Id,
            UserId = otherUserId.ToString(),
        });

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    public async Task GetNode_RequestUserSpoofing_IsRejected()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var ownerClient = _factory.CreateFilesClient(ownerId);
        var createResponse = await ownerClient.CreateFolderAsync(new CreateFolderRequest
        {
            Name = "owner-only",
            UserId = ownerId.ToString(),
        });

        Assert.IsTrue(createResponse.Success, createResponse.ErrorMessage);

        var attackerClient = _factory.CreateFilesClient(attackerId);
        var response = await attackerClient.GetNodeAsync(new GetNodeRequest
        {
            NodeId = createResponse.Node.Id,
            UserId = ownerId.ToString(),
        });

        Assert.IsFalse(response.Found);
    }

    [TestMethod]
    public async Task UploadChunk_DifferentAuthenticatedUser_FailsIdentityValidation()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var chunkData = new byte[] { 1, 2, 3 };
        var chunkHash = Convert.ToHexStringLower(SHA256.HashData(chunkData));

        var ownerClient = _factory.CreateFilesClient(ownerId);
        var initiateResponse = await ownerClient.InitiateUploadAsync(new InitiateUploadRequest
        {
            FileName = "secret.bin",
            TotalSize = chunkData.Length,
            MimeType = "application/octet-stream",
            UserId = ownerId.ToString(),
            ChunkHashes = { chunkHash },
        });

        Assert.IsTrue(initiateResponse.Success, initiateResponse.ErrorMessage);

        var attackerClient = _factory.CreateFilesClient(attackerId);
        var uploadResponse = await attackerClient.UploadChunkAsync(new UploadChunkRequest
        {
            SessionId = initiateResponse.SessionId,
            ChunkHash = chunkHash,
            ChunkData = Google.Protobuf.ByteString.CopyFrom(chunkData),
        });

        Assert.IsFalse(uploadResponse.Success);
        StringAssert.Contains(uploadResponse.ErrorMessage, "identity", StringComparison.OrdinalIgnoreCase);
    }
}
