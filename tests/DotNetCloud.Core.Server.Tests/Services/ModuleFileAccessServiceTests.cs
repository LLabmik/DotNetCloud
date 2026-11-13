using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Tests.Consent;
using DotNetCloud.Core.Services.ModuleApis;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="ModuleFileAccessService"/>: the core brokers a module's file request to the
/// Files module, and both gates — the user's consent scope and the acting user's presence — decide
/// whether the Files module is called at all.
/// </summary>
[TestClass]
public class ModuleFileAccessServiceTests
{
    private const string ModuleId = "dotnetcloud.email";

    private static readonly Guid UserId = Guid.CreateVersion7();

    [TestMethod]
    public async Task ListAsync_WithoutFilesReadConsent_ThrowsConsentRequiredAndDoesNotCallFiles()
    {
        var files = new Mock<IFilesApiClient>(MockBehavior.Strict);
        var service = CreateService(files, grantedScopes: []);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.ListAsync(ModuleId, UserId, parentId: null, page: 1, pageSize: 50, CancellationToken.None));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual("MODULE_CONSENT_REQUIRED", ex.Trailers.GetValue("error-code"));
        StringAssert.Contains(ex.Trailers.GetValue("consent-scopes"), "files:read");
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ListAsync_WithoutActingUser_ThrowsUnauthenticated()
    {
        var files = new Mock<IFilesApiClient>(MockBehavior.Strict);
        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.ListAsync(ModuleId, Guid.Empty, parentId: null, page: 1, pageSize: 50, CancellationToken.None));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ListAsync_WithConsent_ReturnsTheFilesModulesNodes()
    {
        var nodes = new[]
        {
            new FileNodeInfo { Id = Guid.CreateVersion7(), Name = "Report.pdf", NodeType = "File", Size = 42 },
        };

        var files = new Mock<IFilesApiClient>();
        files
            .Setup(client => client.ListChildrenAsync(UserId, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileListResult { Status = FilesAccessStatus.Success, Nodes = nodes, TotalCount = 1 });

        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead]);

        var result = await service.ListAsync(ModuleId, UserId, parentId: null, page: 1, pageSize: 50, CancellationToken.None);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("Report.pdf", result.Nodes[0].Name);
    }

    [TestMethod]
    public async Task ListAsync_FilesModuleUnavailable_ThrowsUnavailable()
    {
        var files = new Mock<IFilesApiClient>();
        files
            .Setup(client => client.ListChildrenAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileListResult { Status = FilesAccessStatus.Unavailable, ErrorMessage = "not running" });

        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.ListAsync(ModuleId, UserId, parentId: null, page: 1, pageSize: 50, CancellationToken.None));

        Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
    }

    [TestMethod]
    public async Task OpenReadAsync_FileGone_ThrowsNotFound()
    {
        var files = new Mock<IFilesApiClient>();
        files
            .Setup(client => client.OpenReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileReadResult { Status = FilesAccessStatus.NotFound });

        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.OpenReadAsync(ModuleId, UserId, Guid.CreateVersion7(), CancellationToken.None));

        Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
    }

    [TestMethod]
    public async Task SaveAsync_WithoutFilesWriteConsent_ThrowsConsentRequired()
    {
        var files = new Mock<IFilesApiClient>(MockBehavior.Strict);
        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead]);

        await using var content = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.SaveAsync(ModuleId, UserId, parentId: null, "notes.txt", "text/plain", content, CancellationToken.None));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        StringAssert.Contains(ex.Trailers.GetValue("consent-scopes"), "files:write");
        files.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SaveAsync_HostileFileName_IsReducedToOneSegment()
    {
        string? capturedName = null;

        var files = new Mock<IFilesApiClient>();
        files
            .Setup(client => client.UploadFileAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid?, string, string, Stream, CancellationToken>((_, _, name, _, _, _) => capturedName = name)
            .ReturnsAsync(new FileWriteResult
            {
                Status = FilesAccessStatus.Success,
                Node = new FileNodeInfo { Id = Guid.CreateVersion7(), Name = "passwd", NodeType = "File" },
            });

        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesRead, ModuleScopes.FilesWrite]);

        await using var content = new MemoryStream([1]);
        await service.SaveAsync(ModuleId, UserId, null, "../../etc/passwd", "text/plain", content, CancellationToken.None);

        Assert.AreEqual("passwd", capturedName);
    }

    [TestMethod]
    public async Task SaveAsync_WithoutContentType_FallsBackToOctetStream()
    {
        string? capturedType = null;

        var files = new Mock<IFilesApiClient>();
        files
            .Setup(client => client.UploadFileAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid?, string, string, Stream, CancellationToken>((_, _, _, type, _, _) => capturedType = type)
            .ReturnsAsync(new FileWriteResult
            {
                Status = FilesAccessStatus.Success,
                Node = new FileNodeInfo { Id = Guid.CreateVersion7(), Name = "f.bin", NodeType = "File" },
            });

        var service = CreateService(files, grantedScopes: [ModuleScopes.FilesWrite]);

        await using var content = new MemoryStream([1]);
        await service.SaveAsync(ModuleId, UserId, null, "f.bin", "  ", content, CancellationToken.None);

        Assert.AreEqual("application/octet-stream", capturedType);
    }

    private static ModuleFileAccessService CreateService(
        Mock<IFilesApiClient> files,
        IReadOnlyList<string> grantedScopes)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => files.Object);
        var provider = services.BuildServiceProvider();

        var manifests = new ModuleManifestRegistry();
        manifests.Record(ModuleId, new ModuleManifestData
        {
            Id = ModuleId,
            Name = "Email",
            Version = "1.2.0",
            ManifestVersion = 2,
            UserConsentScopes = [ModuleScopes.FilesRead, ModuleScopes.FilesWrite],
        });

        var policy = ConsentTestDoubles.Policy(
            manifests,
            new ConsentTestDoubles.GrantingConsentService(grantedScopes));

        return new ModuleFileAccessService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            policy,
            NullLogger<ModuleFileAccessService>.Instance);
    }
}
