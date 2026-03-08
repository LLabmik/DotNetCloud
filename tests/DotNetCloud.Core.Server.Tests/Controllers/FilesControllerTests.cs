using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
public sealed class FilesControllerTests
{
    [TestMethod]
    public async Task ListAsync_WithoutParentId_CallsListRootAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.ListRootAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFileNode(userId)]);

        var controller = deps.CreateController(userId);
        var result = await controller.ListAsync(parentId: null);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        deps.FileService.Verify(s => s.ListRootAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        deps.FileService.Verify(s => s.ListChildrenAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ListAsync_WithParentId_CallsListChildrenAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.ListChildrenAsync(parentId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFileNode(userId)]);

        var controller = deps.CreateController(userId);
        var result = await controller.ListAsync(parentId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        deps.FileService.Verify(s => s.ListChildrenAsync(parentId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Once);
        deps.FileService.Verify(s => s.ListRootAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetAsync_WhenNodeMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.GetNodeAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileNodeDto?)null);

        var controller = deps.CreateController(userId);
        var result = await controller.GetAsync(nodeId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateFolderAsync_ReturnsCreatedWithResourceLocation()
    {
        var userId = Guid.NewGuid();
        var created = CreateFileNode(userId, "New Folder", "Folder");
        var dto = new CreateFolderDto { Name = "New Folder", ParentId = null };
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.CreateFolderAsync(dto, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = deps.CreateController(userId);
        var result = await controller.CreateFolderAsync(dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var createdResult = (CreatedResult)result;
        Assert.AreEqual($"/api/v1/files/{created.Id}", createdResult.Location);
    }

    [TestMethod]
    public async Task RenameAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var renamed = CreateFileNode(userId, "renamed.txt");
        var dto = new RenameNodeDto { Name = "renamed.txt" };
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.RenameAsync(nodeId, dto, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(renamed);

        var controller = deps.CreateController(userId);
        var result = await controller.RenameAsync(nodeId, dto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task MoveAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var dto = new MoveNodeDto { TargetParentId = Guid.NewGuid() };
        var moved = CreateFileNode(userId, "moved.txt");
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.MoveAsync(nodeId, dto, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(moved);

        var controller = deps.CreateController(userId);
        var result = await controller.MoveAsync(nodeId, dto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CopyAsync_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var dto = new MoveNodeDto { TargetParentId = Guid.NewGuid() };
        var copied = CreateFileNode(userId, "copy.txt");
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.CopyAsync(nodeId, dto.TargetParentId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(copied);

        var controller = deps.CreateController(userId);
        var result = await controller.CopyAsync(nodeId, dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.DeleteAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = deps.CreateController(userId);
        var result = await controller.DeleteAsync(nodeId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ToggleFavoriteAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var node = CreateFileNode(userId) with { IsFavorite = true };
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.ToggleFavoriteAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(node);

        var controller = deps.CreateController(userId);
        var result = await controller.ToggleFavoriteAsync(nodeId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ListFavoritesAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.ListFavoritesAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFileNode(userId) with { IsFavorite = true }]);

        var controller = deps.CreateController(userId);
        var result = await controller.ListFavoritesAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ListRecentAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.ListRecentAsync(5, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFileNode(userId)]);

        var controller = deps.CreateController(userId);
        var result = await controller.ListRecentAsync(5);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task SearchAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.SearchAsync("sync", 2, 10, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FileNodeDto>
            {
                Items = [CreateFileNode(userId)],
                TotalCount = 1,
                Page = 2,
                PageSize = 10
            });

        var controller = deps.CreateController(userId);
        var result = await controller.SearchAsync("sync", page: 2, pageSize: 10);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task InitiateUploadAsync_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        var dto = new InitiateUploadDto
        {
            FileName = "upload.bin",
            ParentId = null,
            TotalSize = 8,
            MimeType = "application/octet-stream",
            ChunkHashes = ["hash1"]
        };

        var deps = CreateDeps();
        deps.UploadService
            .Setup(s => s.InitiateUploadAsync(dto, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadSessionDto
            {
                SessionId = Guid.NewGuid(),
                ExistingChunks = [],
                MissingChunks = ["hash1"],
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });

        var controller = deps.CreateController(userId);
        var result = await controller.InitiateUploadAsync(dto);

        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task UploadChunkAsync_ReadsBodyAndPassesBytesToService()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var bytes = new byte[] { 0xAA, 0xBB, 0xCC };
        var deps = CreateDeps();
        deps.UploadService
            .Setup(s => s.UploadChunkAsync(
                sessionId,
                "chunk1",
                It.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(bytes)),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = deps.CreateController(userId);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);

        var result = await controller.UploadChunkAsync(sessionId, "chunk1");

        Assert.IsInstanceOfType<OkObjectResult>(result);
        deps.UploadService.VerifyAll();
    }

    [TestMethod]
    public async Task CompleteUploadAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.UploadService
            .Setup(s => s.CompleteUploadAsync(sessionId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFileNode(userId));

        var controller = deps.CreateController(userId);
        var result = await controller.CompleteUploadAsync(sessionId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CancelUploadAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.UploadService
            .Setup(s => s.CancelUploadAsync(sessionId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = deps.CreateController(userId);
        var result = await controller.CancelUploadAsync(sessionId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetUploadSessionAsync_WhenMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.UploadService
            .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadSessionDto?)null);

        var controller = deps.CreateController(userId);
        var result = await controller.GetUploadSessionAsync(sessionId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadAsync_CurrentFileWithEmptyMimeType_UsesOctetStreamFallback()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();

        deps.FileService
            .Setup(s => s.GetNodeAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileNodeDto
            {
                Id = nodeId,
                Name = "dotnetcloud-sync-tray-win-x64-0.1.0-alpha.msix",
                NodeType = "File",
                MimeType = string.Empty,
                OwnerId = userId
            });

        deps.DownloadService
            .Setup(s => s.DownloadCurrentAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x01, 0x02 }));

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadAsync(nodeId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/octet-stream", fileResult.ContentType);
    }

    [TestMethod]
    public async Task DownloadAsync_WhenNodeMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.FileService
            .Setup(s => s.GetNodeAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileNodeDto?)null);

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadAsync(nodeId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadAsync_WhenVersionMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.VersionService
            .Setup(s => s.GetVersionByNumberAsync(nodeId, 3, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileVersionDto?)null);

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadAsync(nodeId, version: 3);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadAsync_VersionWithWhitespaceMimeType_UsesOctetStreamFallback()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var deps = CreateDeps();

        deps.VersionService
            .Setup(s => s.GetVersionByNumberAsync(nodeId, 1, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileVersionDto
            {
                Id = versionId,
                ContentHash = "abc",
                MimeType = "   "
            });

        deps.DownloadService
            .Setup(s => s.DownloadVersionAsync(versionId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x03, 0x04 }));

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadAsync(nodeId, version: 1);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/octet-stream", fileResult.ContentType);
    }

    [TestMethod]
    public async Task GetChunkManifestAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.DownloadService
            .Setup(s => s.GetChunkManifestAsync(nodeId, It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a", "b"]);

        var controller = deps.CreateController(userId);
        var result = await controller.GetChunkManifestAsync(nodeId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_WhenMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.DownloadService
            .Setup(s => s.DownloadChunkByHashAsync("hash-missing", It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadChunkByHashAsync("hash-missing");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DownloadChunkByHashAsync_WhenFound_ReturnsFileStreamResult()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.DownloadService
            .Setup(s => s.DownloadChunkByHashAsync("hash-ok", It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x10 }));

        var controller = deps.CreateController(userId);
        var result = await controller.DownloadChunkByHashAsync("hash-ok");

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("application/octet-stream", fileResult.ContentType);
    }

    [TestMethod]
    public async Task GetSharedWithMeAsync_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var deps = CreateDeps();
        deps.ShareService
            .Setup(s => s.GetSharedWithMeAsync(It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateShare()]);

        var controller = deps.CreateController(userId);
        var result = await controller.GetSharedWithMeAsync();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ResolvePublicLinkAsync_WhenMissing_ReturnsNotFound()
    {
        var deps = CreateDeps();
        deps.ShareService
            .Setup(s => s.ResolvePublicLinkAsync("token-missing", "pw", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileShareDto?)null);

        var controller = deps.CreateController(authenticatedUserId: null);
        var result = await controller.ResolvePublicLinkAsync("token-missing", "pw");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task ResolvePublicLinkAsync_WhenFound_ReturnsOk()
    {
        var deps = CreateDeps();
        deps.ShareService
            .Setup(s => s.ResolvePublicLinkAsync("token-ok", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateShare());

        var controller = deps.CreateController(authenticatedUserId: null);
        var result = await controller.ResolvePublicLinkAsync("token-ok");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task ListAsync_WhenUnauthenticated_ReturnsForbidden()
    {
        var deps = CreateDeps();

        var controller = deps.CreateController(authenticatedUserId: null);
        var result = await controller.ListAsync(parentId: null);

        Assert.IsInstanceOfType<ObjectResult>(result);
        var status = (ObjectResult)result;
        Assert.AreEqual(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    private static MockDeps CreateDeps()
    {
        return new MockDeps();
    }

    private static FileNodeDto CreateFileNode(Guid ownerId, string name = "file.txt", string nodeType = "File")
    {
        return new FileNodeDto
        {
            Id = Guid.NewGuid(),
            Name = name,
            NodeType = nodeType,
            OwnerId = ownerId,
            MimeType = nodeType == "Folder" ? null : "text/plain"
        };
    }

    private static FileShareDto CreateShare()
    {
        return new FileShareDto
        {
            Id = Guid.NewGuid(),
            ShareType = "PublicLink",
            Permission = "Read",
            LinkToken = "token"
        };
    }

    private sealed class MockDeps
    {
        public Mock<IFileService> FileService { get; } = new();

        public Mock<IChunkedUploadService> UploadService { get; } = new();

        public Mock<IDownloadService> DownloadService { get; } = new();

        public Mock<IVersionService> VersionService { get; } = new();

        public Mock<IShareService> ShareService { get; } = new();

        public FilesController CreateController(Guid? authenticatedUserId)
        {
            ClaimsPrincipal principal;

            if (authenticatedUserId.HasValue)
            {
                principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString())],
                    authenticationType: "TestAuth"));
            }
            else
            {
                principal = new ClaimsPrincipal(new ClaimsIdentity());
            }

            return new FilesController(
                FileService.Object,
                UploadService.Object,
                DownloadService.Object,
                VersionService.Object,
                ShareService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = principal
                    }
                }
            };
        }
    }
}
