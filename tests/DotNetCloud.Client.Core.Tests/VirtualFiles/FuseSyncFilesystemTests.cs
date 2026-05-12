using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.VirtualFiles;

/// <summary>
/// Unit tests for the Linux FUSE virtual file system provider.
/// These tests validate the contract defined by <see cref="IVirtualFileProvider"/>
/// and the FUSE-based implementation logic.
/// Note: FUSE tests require fuse3 and can only run fully on Linux.
/// On non-Linux platforms, only contract-level validation is performed.
/// </summary>
[TestClass]
public sealed class FuseSyncFilesystemTests
{
    // ── IVirtualFileProvider contract validation ───────────────────────
    // The FuseSyncFilesystem implements IVirtualFileProvider, which defines:
    //   - InitializeAsync
    //   - CreatePlaceholdersAsync
    //   - HydrateFileAsync
    //   - DehydrateFileAsync
    //   - PinFileAsync / UnpinFileAsync
    //   - IsHydratedAsync
    //   - ShutdownAsync
    // Full integration tests are environment-gated (Linux + fuse3) and
    // documented under VFS Phase 6 in VIRTUAL_FILE_SYNCING_PLAN.md.
    //
    // When FuseSyncFilesystem is implemented (Phase 4), add unit tests here
    // using a mocked FUSE layer (e.g., ITmdsFuseOperations or equivalent).

    [TestMethod]
    public void IVirtualFileProvider_Interface_NotYetImplemented()
    {
        // FuseSyncFilesystem is scheduled for Phase 4 (Linux FUSE).
        // This test will be updated once the class exists.
        Assert.Inconclusive("FuseSyncFilesystem not yet implemented (Phase 4 - Linux FUSE).");
    }

    [TestMethod]
    public void IVirtualFileProvider_Contract_MethodsDefined()
    {
        var methods = typeof(IVirtualFileProvider).GetMethods();

        Assert.IsNotNull(methods);
        Assert.IsTrue(methods.Any(m => m.Name == "InitializeAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "CreatePlaceholdersAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "HydrateFileAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "DehydrateFileAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "PinFileAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "UnpinFileAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "IsHydratedAsync"));
        Assert.IsTrue(methods.Any(m => m.Name == "ShutdownAsync"));
    }

    [TestMethod]
    public void IVirtualFileProvider_Implements_IAsyncDisposable()
    {
        Assert.IsTrue(typeof(IVirtualFileProvider).GetInterfaces()
            .Contains(typeof(IAsyncDisposable)));
    }

    [TestMethod]
    public async Task NoOpVirtualFileProvider_AllOperationsComplete()
    {
        // Verify that the NoOp stub (used on non-Linux platforms) doesn't throw.
        var noop = new NoOpVirtualFileProvider(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NoOpVirtualFileProvider>.Instance);

        await noop.InitializeAsync(new DotNetCloud.Client.Core.Sync.SyncContext
        {
            Id = Guid.NewGuid(),
            ServerBaseUrl = "https://cloud.example.com",
            UserId = Guid.NewGuid(),
            LocalFolderPath = "/tmp/synctray",
            StateDatabasePath = "/tmp/synctray/state.db",
            AccountKey = "test",
        });

        await noop.CreatePlaceholdersAsync(new DotNetCloud.Client.Core.Api.SyncTreeNodeResponse
        {
            Name = "root",
            NodeType = "Folder",
        });

        await noop.HydrateFileAsync("/tmp/synctray/file.txt", Guid.NewGuid());
        await noop.DehydrateFileAsync("/tmp/synctray/file.txt");
        await noop.PinFileAsync("/tmp/synctray/file.txt");
        await noop.UnpinFileAsync("/tmp/synctray/file.txt");

        var hydrated = await noop.IsHydratedAsync("/tmp/synctray/file.txt");
        Assert.IsTrue(hydrated);

        await noop.ShutdownAsync();
        await noop.DisposeAsync();
    }
}
