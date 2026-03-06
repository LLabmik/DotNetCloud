using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class WopiTokenServiceTests
{
    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CallerContext UserCaller(Guid userId) =>
        new(userId, Array.Empty<string>(), CallerType.User);

    private static WopiTokenService CreateTokenService(
        FilesDbContext db,
        ICollaboraDiscoveryService? discovery = null,
        string? signingKey = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new Files.Options.CollaboraOptions
        {
            TokenLifetimeMinutes = 480,
            TokenSigningKey = signingKey ?? "test-signing-key-that-is-at-least-32-characters-long-for-hmac",
            WopiBaseUrl = "https://cloud.example.com",
            Enabled = true,
            ServerUrl = "https://collabora.example.com"
        });

        return new WopiTokenService(
            db,
            new PermissionService(db),
            discovery ?? CreateMockDiscovery(),
            options,
            NullLogger<WopiTokenService>.Instance);
    }

    private static ICollaboraDiscoveryService CreateMockDiscovery()
    {
        var mock = new Mock<ICollaboraDiscoveryService>();
        mock.Setup(d => d.GetEditorUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://collabora.example.com/browser/dist/cool.html");
        mock.Setup(d => d.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mock.Setup(d => d.IsSupportedExtensionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock.Object;
    }

    [TestMethod]
    public async Task GenerateTokenAsync_ValidFile_ReturnsToken()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "report.docx", NodeType = FileNodeType.File, OwnerId = userId, MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var result = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.AccessToken));
        Assert.IsTrue(result.AccessTokenTtl > 0);
        Assert.IsTrue(result.WopiSrc.Contains(node.Id.ToString()));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_NonexistentFile_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateTokenService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.NotFoundException>(
            () => service.GenerateTokenAsync(Guid.NewGuid(), UserCaller(Guid.NewGuid())));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_Folder_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var folder = new FileNode { Name = "Documents", NodeType = FileNodeType.Folder, OwnerId = userId };
        db.FileNodes.Add(folder);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.GenerateTokenAsync(folder.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_NoAccess_ThrowsForbiddenException()
    {
        using var db = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var node = new FileNode { Name = "secret.docx", NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.ForbiddenException>(
            () => service.GenerateTokenAsync(node.Id, UserCaller(otherId)));
    }

    [TestMethod]
    public async Task ValidateToken_ValidToken_ReturnsContext()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var token = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        var context = service.ValidateToken(token.AccessToken, node.Id);

        Assert.IsNotNull(context);
        Assert.AreEqual(userId, context.UserId);
        Assert.AreEqual(node.Id, context.FileId);
        Assert.IsTrue(context.CanWrite);
        Assert.IsTrue(context.ExpiresAt > DateTime.UtcNow);
    }

    [TestMethod]
    public void ValidateToken_EmptyToken_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateTokenService(db);

        var result = service.ValidateToken("", Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ValidateToken_MalformedToken_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateTokenService(db);

        var result = service.ValidateToken("not.a.valid.token", Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ValidateToken_WrongFileId_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var token = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        var result = service.ValidateToken(token.AccessToken, Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ValidateToken_TamperedPayload_ReturnsNull()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var token = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        // Tamper with the payload
        var parts = token.AccessToken.Split('.');
        var tamperedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"UserId\":\"00000000-0000-0000-0000-000000000000\"}"));
        var tamperedToken = $"{tamperedPayload}.{parts[1]}";

        var result = service.ValidateToken(tamperedToken, node.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GenerateTokenAsync_WopiSrcContainsBaseUrl()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var result = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        Assert.IsTrue(result.WopiSrc.StartsWith("https://cloud.example.com/api/v1/wopi/files/"));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_TokenIsUrlSafeBase64()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);
        var result = await service.GenerateTokenAsync(node.Id, UserCaller(userId));

        Assert.IsFalse(result.AccessToken.Contains('+'));
        Assert.IsFalse(result.AccessToken.Contains('/'));
    }

    [TestMethod]
    public async Task ValidateToken_MissingSigningKeyAcrossServiceInstances_StillValidatesWithinProcess()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var discovery = CreateMockDiscovery();
        var generator = CreateTokenService(db, discovery, signingKey: "too-short");
        var validator = CreateTokenService(db, discovery, signingKey: "too-short");

        var token = await generator.GenerateTokenAsync(node.Id, UserCaller(userId));
        var context = validator.ValidateToken(token.AccessToken, node.Id);

        Assert.IsNotNull(context);
        Assert.AreEqual(userId, context.UserId);
    }

    [TestMethod]
    public async Task GenerateTokenAsync_DeletedFile_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "deleted.docx", NodeType = FileNodeType.File, OwnerId = userId, IsDeleted = true };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var service = CreateTokenService(db);

        await Assert.ThrowsExactlyAsync<Core.Errors.NotFoundException>(
            () => service.GenerateTokenAsync(node.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_CollaboraUnavailable_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "doc.docx", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = CreateTokenService(db, discovery.Object);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.GenerateTokenAsync(node.Id, UserCaller(userId)));
    }

    [TestMethod]
    public async Task GenerateTokenAsync_UnsupportedExtension_ThrowsInvalidOperationException()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var node = new FileNode { Name = "archive.zip", NodeType = FileNodeType.File, OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        var discovery = new Mock<ICollaboraDiscoveryService>();
        discovery.Setup(d => d.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        discovery.Setup(d => d.IsSupportedExtensionAsync("zip", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var service = CreateTokenService(db, discovery.Object);

        await Assert.ThrowsExactlyAsync<Core.Errors.InvalidOperationException>(
            () => service.GenerateTokenAsync(node.Id, UserCaller(userId)));
    }
}
