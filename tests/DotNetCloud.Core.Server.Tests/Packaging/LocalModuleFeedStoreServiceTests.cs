using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.ModulePackaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for the first-release store client: a local feed directory (decision D6).
/// </summary>
[TestClass]
public class LocalModuleFeedStoreServiceTests
{
    private PackageInstallerFixture _fixture = null!;

    [TestInitialize]
    public void Setup() => _fixture = new PackageInstallerFixture();

    [TestCleanup]
    public void Cleanup() => _fixture.Dispose();

    private LocalModuleFeedStoreService CreateService(string? feedPath = null) =>
        new(
            Options.Create(new ModuleStoreOptions { LocalFeedPath = feedPath ?? _fixture.FeedDirectory }),
            Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _fixture.ModulesRoot }),
            NullLogger<LocalModuleFeedStoreService>.Instance);

    [TestMethod]
    public async Task ListPackagesAsync_MissingFeed_ReturnsEmpty()
    {
        var service = CreateService(Path.Combine(_fixture.Root, "does-not-exist"));

        Assert.AreEqual(0, (await service.ListPackagesAsync()).Count);
    }

    [TestMethod]
    public async Task ListPackagesAsync_FeedIndex_IsMappedWithIdentityAndCapabilities()
    {
        var package = await _fixture.CreatePackageAsync();
        var entry = await ModuleFeed.AddAsync(
            _fixture.FeedDirectory, package, ModulePackageSigner.Read(ModulePackageFormat.GetSignaturePath(package)));

        var packages = await CreateService().ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        var listed = packages[0];
        Assert.AreEqual(PackageInstallerFixture.ModuleId, listed.ModuleId);
        Assert.AreEqual("1.0.0", listed.Version);
        Assert.AreEqual(entry.Sha256, listed.Sha256);
        Assert.AreEqual("test-store-key", listed.KeyId);
        Assert.AreEqual("example-org", listed.PublisherId);
        CollectionAssert.Contains(listed.Capabilities.ToArray(), "IStorageProvider");
        CollectionAssert.Contains(listed.UserConsentScopes.ToArray(), "files:read");
        Assert.IsFalse(listed.Yanked);
    }

    [TestMethod]
    public async Task ListPackagesAsync_YankedVersion_IsFlagged()
    {
        var package = await _fixture.CreatePackageAsync();
        await ModuleFeed.AddAsync(_fixture.FeedDirectory, package, null);
        await _fixture.WriteYankedAsync(PackageInstallerFixture.ModuleId, "1.0.0");

        var packages = await CreateService().ListPackagesAsync();

        Assert.AreEqual(1, packages.Count);
        Assert.IsTrue(packages[0].Yanked);
        StringAssert.Contains(packages[0].YankReason ?? string.Empty, "credential leak");
    }

    [TestMethod]
    public async Task ResolveAsync_NoVersion_ReturnsHighestVersion()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");
        await ModuleFeed.AddAsync(_fixture.FeedDirectory, first, null);

        var second = await _fixture.CreatePackageAsync(version: "1.2.0");
        await ModuleFeed.AddAsync(_fixture.FeedDirectory, second, null);

        var resolution = await CreateService().ResolveAsync(PackageInstallerFixture.ModuleId);

        Assert.IsNotNull(resolution);
        Assert.AreEqual("1.2.0", resolution.Package.Version);
        Assert.IsTrue(File.Exists(resolution.PackagePath));
    }

    [TestMethod]
    public async Task ResolveAsync_ExplicitVersion_ReturnsThatVersion()
    {
        await ModuleFeed.AddAsync(_fixture.FeedDirectory, await _fixture.CreatePackageAsync(version: "1.0.0"), null);
        await ModuleFeed.AddAsync(_fixture.FeedDirectory, await _fixture.CreatePackageAsync(version: "2.0.0"), null);

        var resolution = await CreateService().ResolveAsync(PackageInstallerFixture.ModuleId, "1.0.0");

        Assert.IsNotNull(resolution);
        Assert.AreEqual("1.0.0", resolution.Package.Version);
    }

    [TestMethod]
    public async Task ResolveAsync_UnknownModule_ReturnsNull()
    {
        Assert.IsNull(await CreateService().ResolveAsync("org.example.absent"));
    }

    [TestMethod]
    public async Task ResolveAsync_EntryOutsideFeed_Throws()
    {
        await _fixture.WriteFeedIndexAsync(new ModuleFeedEntry(
            PackageInstallerFixture.ModuleId,
            "Receipts",
            "1.0.0",
            "../../etc/passwd",
            10,
            new string('a', 64)));

        var service = CreateService();

        await Assert.ThrowsExactlyAsync<ModulePackageException>(
            async () => await service.ResolveAsync(PackageInstallerFixture.ModuleId));
    }

    [TestMethod]
    public void SourceKind_IsLocalFeed()
    {
        Assert.AreEqual(ModuleStoreSourceKind.LocalFeed, CreateService().SourceKind);
    }
}
