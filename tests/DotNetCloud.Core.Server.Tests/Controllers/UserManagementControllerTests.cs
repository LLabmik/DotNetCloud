using System.Security.Claims;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
[DoNotParallelize]
public sealed class UserManagementControllerTests : IDisposable
{
    private Mock<IUserManagementService> _serviceMock = null!;
    private Mock<ILogger<UserManagementController>> _loggerMock = null!;
    private UserManagementController _controller = null!;
    private string? _tempAvatarDir;

    [TestInitialize]
    public void Setup()
    {
        _serviceMock = new Mock<IUserManagementService>();
        _loggerMock = new Mock<ILogger<UserManagementController>>();
        _controller = new UserManagementController(_serviceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", null);
        if (_tempAvatarDir is not null)
        {
            // _tempAvatarDir points to the "avatars" subdir; clean the parent temp dir
            var parent = Path.GetDirectoryName(_tempAvatarDir);
            if (parent is not null && parent != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) && Directory.Exists(parent))
            {
                try { Directory.Delete(parent, recursive: true); } catch { /* best-effort */ }
            }
            else if (_tempAvatarDir is not null && Directory.Exists(_tempAvatarDir))
            {
                try { Directory.Delete(_tempAvatarDir, recursive: true); } catch { /* best-effort */ }
            }
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetUserAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetUserAsync_CurrentUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _serviceMock.Setup(s => s.GetUserAsync(userId))
            .ReturnsAsync(CreateUserDto(userId));

        var result = await _controller.GetUserAsync(userId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetUserAsync_AdminViewingOtherUser_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.GetUserAsync(targetId))
            .ReturnsAsync(CreateUserDto(targetId));

        var result = await _controller.GetUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetUserAsync_NonAdminViewingOtherUser_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        SetUser(userId);

        var result = await _controller.GetUserAsync(otherId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task GetUserAsync_UserNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _serviceMock.Setup(s => s.GetUserAsync(userId))
            .ReturnsAsync((UserDto?)null);

        var result = await _controller.GetUserAsync(userId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpdateUserAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task UpdateUserAsync_CurrentUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        var dto = new UpdateUserDto { DisplayName = "New Name" };
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId, "New Name"));

        var result = await _controller.UpdateUserAsync(userId, dto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateUserAsync_NonAdminSetsIsActive_IsActiveNulled()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        var dto = new UpdateUserDto { DisplayName = "Name", IsActive = false };
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.Is<UpdateUserDto>(d => d.IsActive == null)))
            .ReturnsAsync(CreateUserDto(userId));

        var result = await _controller.UpdateUserAsync(userId, dto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.UpdateUserAsync(userId, It.Is<UpdateUserDto>(d => d.IsActive == null)), Times.Once);
    }

    [TestMethod]
    public async Task UpdateUserAsync_AdminSetsIsActive_IsActivePreserved()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        var dto = new UpdateUserDto { DisplayName = "Name", IsActive = false };
        _serviceMock.Setup(s => s.UpdateUserAsync(targetId, It.Is<UpdateUserDto>(d => d.IsActive == false)))
            .ReturnsAsync(CreateUserDto(targetId));

        var result = await _controller.UpdateUserAsync(targetId, dto);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateUserAsync_NonAdminUpdatingOtherUser_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        SetUser(userId);

        var result = await _controller.UpdateUserAsync(otherId, new UpdateUserDto());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task UpdateUserAsync_UserNotFound_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync((UserDto?)null);

        var result = await _controller.UpdateUserAsync(userId, new UpdateUserDto());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task UpdateUserAsync_ServiceThrows_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ThrowsAsync(new InvalidOperationException("Update failed"));

        var result = await _controller.UpdateUserAsync(userId, new UpdateUserDto());

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DeleteUserAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DeleteUserAsync_SelfDeletion_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetAdmin(userId);

        var result = await _controller.DeleteUserAsync(userId);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteUserAsync_UserNotFound_ReturnsNotFound()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.DeleteUserAsync(targetId)).ReturnsAsync(false);

        var result = await _controller.DeleteUserAsync(targetId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteUserAsync_Success_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.DeleteUserAsync(targetId)).ReturnsAsync(true);

        var result = await _controller.DeleteUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DisableUserAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DisableUserAsync_SelfDisable_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetAdmin(userId);

        var result = await _controller.DisableUserAsync(userId);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task DisableUserAsync_UserNotFound_ReturnsNotFound()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.DisableUserAsync(targetId)).ReturnsAsync(false);

        var result = await _controller.DisableUserAsync(targetId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task DisableUserAsync_Success_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.DisableUserAsync(targetId)).ReturnsAsync(true);

        var result = await _controller.DisableUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EnableUserAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task EnableUserAsync_UserNotFound_ReturnsNotFound()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.EnableUserAsync(targetId)).ReturnsAsync(false);

        var result = await _controller.EnableUserAsync(targetId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task EnableUserAsync_Success_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.EnableUserAsync(targetId)).ReturnsAsync(true);

        var result = await _controller.EnableUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AdminResetPasswordAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AdminResetPasswordAsync_Success_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.AdminResetPasswordAsync(targetId, It.IsAny<AdminResetPasswordRequest>()))
            .ReturnsAsync(true);

        var result = await _controller.AdminResetPasswordAsync(targetId, new AdminResetPasswordRequest { NewPassword = "NewPass1!" });

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task AdminResetPasswordAsync_Failure_ReturnsBadRequest()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        _serviceMock.Setup(s => s.AdminResetPasswordAsync(targetId, It.IsAny<AdminResetPasswordRequest>()))
            .ReturnsAsync(false);

        var result = await _controller.AdminResetPasswordAsync(targetId, new AdminResetPasswordRequest { NewPassword = "weak" });

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UploadAvatarAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task UploadAvatarAsync_NonAdminUploadForOtherUser_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        SetUser(userId);

        var file = CreateMockFormFile("avatar.jpg", "image/jpeg");
        var result = await _controller.UploadAvatarAsync(otherId, file);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_NullFile_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);

        var result = await _controller.UploadAvatarAsync(userId, null!);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_EmptyFile_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);

        var file = CreateMockFormFile("avatar.jpg", "image/jpeg", contentSize: 0);
        var result = await _controller.UploadAvatarAsync(userId, file);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_InvalidContentType_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);

        var file = CreateMockFormFile("doc.pdf", "application/pdf");
        var result = await _controller.UploadAvatarAsync(userId, file);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    [DataRow("image/jpeg", ".jpg")]
    [DataRow("image/png", ".png")]
    [DataRow("image/gif", ".gif")]
    [DataRow("image/webp", ".webp")]
    public async Task UploadAvatarAsync_ValidImage_SavesFileAndReturnsOk(string contentType, string extension)
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId));

        var file = CreateMockFormFile($"avatar{extension}", contentType);
        var result = await _controller.UploadAvatarAsync(userId, file);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.UpdateUserAsync(userId,
            It.Is<UpdateUserDto>(d => d.AvatarUrl == $"/api/v1/core/users/{userId}/avatar")), Times.Once);

        // Verify file was saved
        var savedFiles = Directory.GetFiles(_tempAvatarDir!, $"{userId}.*");
        Assert.AreEqual(1, savedFiles.Length);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_AdminUploadForOtherUser_Succeeds()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(targetId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(targetId));

        var file = CreateMockFormFile("avatar.png", "image/png");
        var result = await _controller.UploadAvatarAsync(targetId, file);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_ReplacesExistingAvatar()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId));

        // Place an existing avatar
        File.WriteAllText(Path.Combine(_tempAvatarDir!, $"{userId}.png"), "old-data");

        var file = CreateMockFormFile("new-avatar.jpg", "image/jpeg");
        var result = await _controller.UploadAvatarAsync(userId, file);

        Assert.IsInstanceOfType<OkObjectResult>(result);

        // Old file should be gone, new one present
        var files = Directory.GetFiles(_tempAvatarDir!, $"{userId}.*");
        Assert.AreEqual(1, files.Length);
        Assert.IsTrue(files[0].EndsWith(".jpg"));
    }

    [TestMethod]
    public async Task UploadAvatarAsync_NoExtensionInFileName_InfersFromContentType()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId));

        var file = CreateMockFormFile("avatar", "image/png"); // no extension
        var result = await _controller.UploadAvatarAsync(userId, file);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var files = Directory.GetFiles(_tempAvatarDir!, $"{userId}.*");
        Assert.AreEqual(1, files.Length);
        Assert.IsTrue(files[0].EndsWith(".png"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetAvatar
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void GetAvatar_NoAvatarFile_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        SetupTempAvatarDir();

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public void GetAvatar_DirectoryDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        // Set env to a non-existent subdirectory (creates unique path that doesn't exist)
        var fakePath = Path.Combine(Path.GetTempPath(), $"dnc-nonexistent-{Guid.NewGuid()}");
        Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", fakePath);

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public void GetAvatar_JpegAvatarExists_ReturnsFileWithCorrectContentType()
    {
        var userId = Guid.NewGuid();
        SetupTempAvatarDir();
        File.WriteAllBytes(Path.Combine(_tempAvatarDir!, $"{userId}.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("image/jpeg", fileResult.ContentType);
        fileResult.FileStream.Dispose();
    }

    [TestMethod]
    public void GetAvatar_PngAvatarExists_ReturnsCorrectContentType()
    {
        var userId = Guid.NewGuid();
        SetupTempAvatarDir();
        File.WriteAllBytes(Path.Combine(_tempAvatarDir!, $"{userId}.png"), new byte[] { 0x89, 0x50, 0x4E });

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("image/png", fileResult.ContentType);
        fileResult.FileStream.Dispose();
    }

    [TestMethod]
    public void GetAvatar_WebpAvatarExists_ReturnsCorrectContentType()
    {
        var userId = Guid.NewGuid();
        SetupTempAvatarDir();
        File.WriteAllBytes(Path.Combine(_tempAvatarDir!, $"{userId}.webp"), new byte[] { 0x52, 0x49, 0x46 });

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("image/webp", fileResult.ContentType);
        fileResult.FileStream.Dispose();
    }

    [TestMethod]
    public void GetAvatar_GifAvatarExists_ReturnsCorrectContentType()
    {
        var userId = Guid.NewGuid();
        SetupTempAvatarDir();
        File.WriteAllBytes(Path.Combine(_tempAvatarDir!, $"{userId}.gif"), new byte[] { 0x47, 0x49, 0x46 });

        var result = _controller.GetAvatar(userId);

        Assert.IsInstanceOfType<FileStreamResult>(result);
        var fileResult = (FileStreamResult)result;
        Assert.AreEqual("image/gif", fileResult.ContentType);
        fileResult.FileStream.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DeleteAvatarAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task DeleteAvatarAsync_NonAdminDeleteOtherUser_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        SetUser(userId);

        var result = await _controller.DeleteAvatarAsync(otherId);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task DeleteAvatarAsync_CurrentUser_DeletesFileAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        SetupTempAvatarDir();
        File.WriteAllText(Path.Combine(_tempAvatarDir!, $"{userId}.jpg"), "data");
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId));

        var result = await _controller.DeleteAvatarAsync(userId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.AreEqual(0, Directory.GetFiles(_tempAvatarDir!, $"{userId}.*").Length);
        _serviceMock.Verify(s => s.UpdateUserAsync(userId,
            It.Is<UpdateUserDto>(d => d.AvatarUrl == null)), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAvatarAsync_NoExistingFile_StillSucceeds()
    {
        var userId = Guid.NewGuid();
        SetUser(userId);
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(userId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(userId));

        var result = await _controller.DeleteAvatarAsync(userId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task DeleteAvatarAsync_AdminDeleteForOtherUser_Succeeds()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetAdmin(adminId);
        SetupTempAvatarDir();
        File.WriteAllText(Path.Combine(_tempAvatarDir!, $"{targetId}.png"), "data");
        _serviceMock.Setup(s => s.UpdateUserAsync(targetId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(targetId));

        var result = await _controller.DeleteAvatarAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.AreEqual(0, Directory.GetFiles(_tempAvatarDir!, $"{targetId}.*").Length);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ListUsersAsync
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ListUsersAsync_ReturnsOkWithPagination()
    {
        var adminId = Guid.NewGuid();
        SetAdmin(adminId);
        var result = new PaginatedResult<UserDto>
        {
            Items = [CreateUserDto(Guid.NewGuid())],
            Page = 1,
            PageSize = 25,
            TotalCount = 1,
        };
        _serviceMock.Setup(s => s.ListUsersAsync(It.IsAny<UserListQuery>())).ReturnsAsync(result);

        var actionResult = await _controller.ListUsersAsync(new UserListQuery());

        Assert.IsInstanceOfType<OkObjectResult>(actionResult);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Authorization edge cases
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task GetUserAsync_AdminRoleClaim_CanViewOtherUser()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetUserWithClaims(userId, new Claim(ClaimTypes.Role, "Administrator"));
        _serviceMock.Setup(s => s.GetUserAsync(targetId))
            .ReturnsAsync(CreateUserDto(targetId));

        var result = await _controller.GetUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetUserAsync_AdminStringRoleClaim_CanViewOtherUser()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetUserWithClaims(userId, new Claim(ClaimTypes.Role, "admin"));
        _serviceMock.Setup(s => s.GetUserAsync(targetId))
            .ReturnsAsync(CreateUserDto(targetId));

        var result = await _controller.GetUserAsync(targetId);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task UploadAvatarAsync_PermissionClaimAdmin_CanUploadForOtherUser()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetUserWithClaims(adminId, new Claim(PermissionAuthorizationHandler.PermissionClaimType, "admin"));
        SetupTempAvatarDir();
        _serviceMock.Setup(s => s.UpdateUserAsync(targetId, It.IsAny<UpdateUserDto>()))
            .ReturnsAsync(CreateUserDto(targetId));

        var file = CreateMockFormFile("avatar.jpg", "image/jpeg");
        var result = await _controller.UploadAvatarAsync(targetId, file);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private void SetUser(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", userId.ToString())], "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private void SetAdmin(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, "Administrator"),
            ], "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private void SetUserWithClaims(Guid userId, params Claim[] extraClaims)
    {
        var claims = new List<Claim> { new("sub", userId.ToString()) };
        claims.AddRange(extraClaims);
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private void SetupTempAvatarDir()
    {
        _tempAvatarDir = Path.Combine(Path.GetTempPath(), $"dnc-test-avatars-{Guid.NewGuid()}");
        var parentDir = Path.GetDirectoryName(_tempAvatarDir)!;
        // Set env variable so GetAvatarStoragePath() uses our temp dir
        Environment.SetEnvironmentVariable("DOTNETCLOUD_DATA_DIR", _tempAvatarDir);
        // The method appends "avatars" to the data dir, so create the full path
        var avatarsPath = Path.Combine(_tempAvatarDir, "avatars");
        Directory.CreateDirectory(avatarsPath);
        _tempAvatarDir = avatarsPath;
    }

    private static IFormFile CreateMockFormFile(string fileName, string contentType, int contentSize = 100)
    {
        var content = new byte[contentSize];
        if (contentSize > 0)
        {
            new Random(42).NextBytes(content);
        }

        var stream = new MemoryStream(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.Length).Returns(contentSize);
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<Stream, CancellationToken>((target, _) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(target);
            });
        return mock.Object;
    }

    private static UserDto CreateUserDto(Guid userId, string displayName = "Test User") => new()
    {
        Id = userId,
        Email = "test@example.com",
        DisplayName = displayName,
        Locale = "en-US",
        Timezone = "UTC",
        IsActive = true,
    };
}
