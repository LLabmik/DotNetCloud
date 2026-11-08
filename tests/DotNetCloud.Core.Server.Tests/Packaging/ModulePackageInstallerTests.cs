using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.ModulePackaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for the package installer: verification, review, consent acknowledgements, the swap, the
/// health-check rollback and the retained-version rollback (phase B3).
/// </summary>
[TestClass]
[DoNotParallelize]
public class ModulePackageInstallerTests
{
    private PackageInstallerFixture _fixture = null!;
    private string? _previousModulesDirectory;
    private string _dbName = null!;

    [TestInitialize]
    public void Setup()
    {
        _fixture = new PackageInstallerFixture();
        _dbName = $"ModulePackageInstallerTests_{Guid.NewGuid():N}";

        // The installer resolves the modules directory exactly like the CLI: an explicit
        // DOTNETCLOUD_MODULES_DIR wins over the configured default.
        _previousModulesDirectory = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULES_DIR");
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULES_DIR", _fixture.ModulesRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULES_DIR", _previousModulesDirectory);
        _fixture.Dispose();
    }

    // ---------------------------------------------------------------- helpers

    private CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options,
            new PostgreSqlNamingStrategy());

    private ModuleStoreOptions CreateStoreOptions(bool allowUnverified = true) => new()
    {
        LocalFeedPath = _fixture.FeedDirectory,
        AllowUnverified = allowUnverified,
        TrustedKeys = new Dictionary<string, string>(_fixture.TrustedKeys, StringComparer.Ordinal),
        HealthCheckTimeout = TimeSpan.FromSeconds(1),
    };

    private ModulePackageInstaller CreateInstaller(
        CoreDbContext db,
        IModuleInstallRunner? runner = null,
        IModuleStoreService? store = null,
        ModuleStoreOptions? storeOptions = null,
        IAuditLogger? audit = null,
        IModuleDataStoreProvisioningService? dataStores = null) =>
        new(
            db,
            runner ?? new FakeRunner(),
            dataStores ?? Mock.Of<IModuleDataStoreProvisioningService>(),
            store ?? Mock.Of<IModuleStoreService>(),
            Options.Create(storeOptions ?? CreateStoreOptions()),
            Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _fixture.ModulesRoot }),
            NullLogger<ModulePackageInstaller>.Instance,
            audit);

    private static ModulePackageInstallRequest Request(
        string packagePath,
        ModulePackageReviewDto review,
        bool allowUnverified = false) => new()
        {
            PackagePath = packagePath,
            ExpectedSha256 = review.Sha256,
            AllowUnverified = allowUnverified,
            AcknowledgedCapabilities = review.Capabilities,
            AcknowledgedScopes = review.UserConsentScopes,
        };

    // ---------------------------------------------------------------- review

    [TestMethod]
    public async Task InspectAsync_TrustedSignedPackage_ReportsValidAndTrusted()
    {
        var package = await _fixture.CreatePackageAsync();

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsTrue(review.IsValid);
        Assert.AreEqual(PackageInstallerFixture.ModuleId, review.ModuleId);
        Assert.AreEqual("1.0.0", review.Version);
        Assert.AreEqual(2, review.ManifestVersion);
        Assert.IsTrue(review.IsSigned);
        Assert.IsTrue(review.IsSignedByTrustedKey);
        Assert.AreEqual("test-store-key", review.KeyId);
        Assert.AreEqual("example-org", review.PublisherId);
        CollectionAssert.AreEquivalent(
            new[] { "IAuditLogger", "IStorageProvider" }, review.Capabilities.ToArray());
        CollectionAssert.AreEqual(new[] { "files:read" }, review.UserConsentScopes.ToArray());
    }

    [TestMethod]
    public async Task InspectAsync_UnsignedPackage_ReportsMissingSignature()
    {
        var package = await _fixture.CreatePackageAsync(sign: false);

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.IsStructurallyValid, "an unsigned package is structurally installable with an override");
        Assert.IsFalse(review.IsSigned);
        Assert.IsFalse(review.IsSignedByTrustedKey);
        Assert.IsTrue(review.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureMissing));
    }

    [TestMethod]
    public async Task InspectAsync_PackageSignedByUnknownKey_ReportsUntrustedKey()
    {
        var package = await _fixture.CreatePackageAsync(
            signWith: Ed25519Signature.GenerateKeyPair("someone-else"));

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.IsStructurallyValid, "a package signed by a foreign key is structurally installable");
        Assert.IsTrue(review.IsSigned);
        Assert.IsFalse(review.IsSignedByTrustedKey);
        Assert.IsTrue(review.Issues.Any(issue => issue.Code == PackageIssueCodes.SignatureUntrustedKey));
    }

    [TestMethod]
    public async Task InspectAsync_PackageTamperedAfterSigning_ReportsHashMismatch()
    {
        var package = await _fixture.CreatePackageAsync();
        await File.AppendAllTextAsync(package, "tampered");

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.Issues.Any(issue =>
            issue.Code is PackageIssueCodes.HashMismatch or PackageIssueCodes.InvalidZip));
    }

    [TestMethod]
    public async Task InspectAsync_ManifestDeclaresUnknownScope_ReportsManifestInvalid()
    {
        // The core's own manifest loader owns the scope catalog (F18), so the verifier reports the
        // refusal as a manifest error rather than letting the installer re-implement the rule.
        var manifest = PackageInstallerFixture.BuildManifest(
                PackageInstallerFixture.ModuleId, "1.0.0")
            .Replace("\"files:read\"", "\"unknown:everything\"", StringComparison.Ordinal);

        var package = await _fixture.CreatePackageAsync(manifestOverride: manifest);

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.Issues.Any(issue => issue.Code == PackageIssueCodes.ManifestInvalid));
    }

    [TestMethod]
    public async Task InspectAsync_RequiredModuleId_ReportsModuleRequired()
    {
        var package = await _fixture.CreatePackageAsync(
            moduleId: "dotnetcloud.files");

        await using var db = CreateContext();
        var review = await CreateInstaller(db).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.IsRequiredModule);
        Assert.IsTrue(review.Issues.Any(issue => issue.Code == "MODULE_REQUIRED"));
    }

    [TestMethod]
    public async Task InspectAsync_VersionWithdrawnByFeed_ReportsWithdrawn()
    {
        var package = await _fixture.CreatePackageAsync();
        await _fixture.WriteYankedAsync(PackageInstallerFixture.ModuleId, "1.0.0");

        await using var db = CreateContext();
        var store = new LocalModuleFeedStoreService(
            Options.Create(CreateStoreOptions()),
            Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _fixture.ModulesRoot }),
            NullLogger<LocalModuleFeedStoreService>.Instance);

        await ModuleFeed.AddAsync(_fixture.FeedDirectory, package, null);

        var review = await CreateInstaller(db, store: store).InspectAsync(package);

        Assert.IsFalse(review.IsValid);
        Assert.IsTrue(review.IsYanked);
        StringAssert.Contains(review.YankReason ?? string.Empty, "credential leak");
    }

    // ---------------------------------------------------------------- install

    [TestMethod]
    public async Task InstallAsync_TrustedSignedPackage_InstallsBinariesAndRecord()
    {
        var package = await _fixture.CreatePackageAsync();

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var result = await installer.InstallAsync(Request(package, review));

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual(ModuleTrustTier.Store.ToString(), result.TrustTier);
        Assert.AreEqual("test-store-key", result.KeyId);

        var moduleDirectory = Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId);
        Assert.IsTrue(File.Exists(Path.Combine(moduleDirectory, "manifest.json")), "manifest.json was not installed");
        Assert.IsTrue(
            File.Exists(Path.Combine(moduleDirectory, PackageInstallerFixture.ModuleId + ".dll")),
            "the runtime binaries were not flattened to the module root");
        Assert.IsTrue(File.Exists(Path.Combine(moduleDirectory, ModulePackageInstaller.ReceiptFileName)));

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", row.Version);
        Assert.AreEqual(ModuleTrustTier.Store, row.TrustTier);
        Assert.AreEqual("test-store-key", row.SignedByKeyId);
        Assert.AreEqual("example-org", row.PublisherId);
        Assert.AreEqual(ModuleInstallStatus.Enabled, row.Status);
    }

    [TestMethod]
    public async Task InstallAsync_NoRuntimeForHost_FailsAndLeavesNoInstall()
    {
        var staging = _fixture.CreateStaging(rid: "plan9-sparc");
        var package = await _fixture.PackAsync(staging);

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var result = await installer.InstallAsync(Request(package, review));

        Assert.IsFalse(result.Success);
        Assert.AreEqual(PackageIssueCodes.NoMatchingRuntime, result.Issues[0].Code);
        Assert.IsFalse(Directory.Exists(Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId)));
        Assert.AreEqual(0, await db.InstalledModules.CountAsync());
    }

    [TestMethod]
    public async Task InstallAsync_UntrustedPackageWithoutAllowUnverified_Refuses()
    {
        var package = await _fixture.CreatePackageAsync(
            signWith: Ed25519Signature.GenerateKeyPair("someone-else"));

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var result = await installer.InstallAsync(Request(package, review, allowUnverified: false));

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "not signed by a key this server trusts");
        Assert.IsFalse(Directory.Exists(Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId)));
    }

    [TestMethod]
    public async Task InstallAsync_UntrustedPackageWithServerOverrideDisabled_Refuses()
    {
        var package = await _fixture.CreatePackageAsync(sign: false);

        await using var db = CreateContext();
        var installer = CreateInstaller(
            db, storeOptions: CreateStoreOptions(allowUnverified: false));
        var review = await installer.InspectAsync(package);

        var result = await installer.InstallAsync(Request(package, review, allowUnverified: true));

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "does not permit unverified installs");
    }

    [TestMethod]
    public async Task InstallAsync_UntrustedPackageWithAllowUnverified_InstallsWithoutSupportingKey()
    {
        var package = await _fixture.CreatePackageAsync(sign: false);

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var result = await installer.InstallAsync(Request(package, review, allowUnverified: true));

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNull(result.KeyId);

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual(ModuleTrustTier.Store, row.TrustTier);
        Assert.IsNull(row.SignedByKeyId, "an unverified install must not record a signing key");

        var receipt = await File.ReadAllTextAsync(Path.Combine(
            _fixture.ModulesRoot, PackageInstallerFixture.ModuleId, ModulePackageInstaller.ReceiptFileName));
        StringAssert.Contains(receipt, "\"unverified\": true");
    }

    [TestMethod]
    public async Task InstallAsync_AcknowledgedCapabilitiesDifferFromManifest_Refuses()
    {
        var package = await _fixture.CreatePackageAsync();

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var request = Request(package, review);
        request = new ModulePackageInstallRequest
        {
            PackagePath = package,
            ExpectedSha256 = review.Sha256,
            AcknowledgedCapabilities = ["IAuditLogger"],
            AcknowledgedScopes = review.UserConsentScopes,
        };

        var result = await installer.InstallAsync(request);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "capabilities do not match");
        Assert.IsFalse(Directory.Exists(Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId)));
    }

    [TestMethod]
    public async Task InstallAsync_ReviewedHashDiffersFromBytes_Refuses()
    {
        var package = await _fixture.CreatePackageAsync();

        await using var db = CreateContext();
        var installer = CreateInstaller(db);
        var review = await installer.InspectAsync(package);

        var request = new ModulePackageInstallRequest
        {
            PackagePath = package,
            ExpectedSha256 = new string('0', 64),
            AcknowledgedCapabilities = review.Capabilities,
            AcknowledgedScopes = review.UserConsentScopes,
        };

        var result = await installer.InstallAsync(request);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "changed since they were reviewed");
    }

    [TestMethod]
    public async Task InstallAsync_UnsafeArchive_RefusesAndAuditsVerificationFailure()
    {
        var package = _fixture.WriteRawArchive(
            "hostile.dncpkg",
            new[]
            {
                ("manifest.json", PackageInstallerFixture.BuildManifest("org.example.receipts", "1.0.0")),
                ("LICENSE", "licence"),
                ("README.md", "# x"),
                ("CHANGELOG.md", "# x"),
                (ModulePackageFormat.FilesDirectoryName + "/" + PackageInstallerFixture.HostRuntime + "/x.dll", "x"),
                ("../escape.txt", "escaped"),
            });

        var audit = new Mock<IAuditLogger>();
        audit.Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var db = CreateContext();
        var installer = CreateInstaller(db, audit: audit.Object);

        var result = await installer.InstallAsync(new ModulePackageInstallRequest { PackagePath = package });

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == PackageIssueCodes.PathTraversal));

        audit.Verify(
            a => a.LogAsync(
                It.Is<AuditEntry>(entry => entry.Action == AuditAction.ModulePackageVerificationFailed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InstallAsync_UpgradeRetainsPreviousVersionAndRequestsRestart()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");

        await using var db = CreateContext();
        var installer = CreateInstaller(db);

        var firstReview = await installer.InspectAsync(first);
        Assert.IsTrue((await installer.InstallAsync(Request(first, firstReview))).Success);

        var second = await _fixture.CreatePackageAsync(version: "1.1.0");
        var secondReview = await installer.InspectAsync(second);
        Assert.AreEqual("1.0.0", secondReview.InstalledVersion);

        var upgrade = await installer.InstallAsync(Request(second, secondReview));

        Assert.IsTrue(upgrade.Success, upgrade.Error);
        Assert.AreEqual("1.0.0", upgrade.PreviousVersion);

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.1.0", row.Version);
        Assert.AreEqual(ModuleInstallStatus.RestartPending, row.Status);

        var retained = Path.Combine(
            _fixture.ModulesRoot, ".previous", PackageInstallerFixture.ModuleId, "1.0.0");
        Assert.IsTrue(Directory.Exists(retained), "the replaced version was not retained for rollback");
    }

    [TestMethod]
    public async Task InstallAsync_ModuleFailsHealthCheck_RollsBackToPreviousVersion()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");

        await using var db = CreateContext();
        var runner = new FakeRunner { Healthy = true };
        var installer = CreateInstaller(db, runner);

        var firstReview = await installer.InspectAsync(first);
        Assert.IsTrue((await installer.InstallAsync(Request(first, firstReview))).Success);

        runner.Healthy = false;
        runner.Reason = "no health response";

        var second = await _fixture.CreatePackageAsync(version: "1.1.0");
        var secondReview = await installer.InspectAsync(second);
        var upgrade = await installer.InstallAsync(Request(second, secondReview));

        Assert.IsFalse(upgrade.Success);
        Assert.IsTrue(upgrade.RolledBack);
        Assert.AreEqual("no health response", upgrade.RollbackReason);

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", row.Version, "the install record was not restored to the previous version");
        Assert.AreEqual(ModuleInstallStatus.Disabled, row.Status);

        var live = Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId);
        Assert.IsTrue(File.Exists(Path.Combine(live, "manifest.json")));

        // The restored tree is the 1.0.0 build.
        var manifest = ModulePackageManifest.TryReadFile(Path.Combine(live, "manifest.json"));
        Assert.AreEqual("1.0.0", manifest?.Version);
    }

    [TestMethod]
    public async Task InstallAsync_FirstInstallFailsHealthCheck_RemovesRecordAndDirectory()
    {
        var package = await _fixture.CreatePackageAsync();

        await using var db = CreateContext();
        var runner = new FakeRunner { Healthy = false, Reason = "crashed on start" };
        var installer = CreateInstaller(db, runner);

        var review = await installer.InspectAsync(package);
        var result = await installer.InstallAsync(Request(package, review));

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.RolledBack);
        Assert.AreEqual(0, await db.InstalledModules.CountAsync());
        Assert.IsFalse(Directory.Exists(Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId)));
    }

    [TestMethod]
    public async Task InstallAsync_IsolatedDataStoreProvisionedBeforeSwap()
    {
        var package = await _fixture.CreatePackageAsync(
            manifestOverride: PackageInstallerFixture.BuildManifest(
                PackageInstallerFixture.ModuleId, "1.0.0", dataStore: "isolated"));

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.EnsureProvisionedAsync(
                PackageInstallerFixture.ModuleId, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "mod_org_example_receipts", "mod_org_example_receipts", null));

        await using var db = CreateContext();
        var installer = CreateInstaller(db, dataStores: dataStores.Object);

        var review = await installer.InspectAsync(package);
        var result = await installer.InstallAsync(Request(package, review));

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsTrue(result.DataStoreProvisioned);

        dataStores.Verify(
            d => d.EnsureProvisionedAsync(
                PackageInstallerFixture.ModuleId, It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task InstallAsync_DataStoreProvisioningFails_LeavesPreviousInstallIntact()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");

        await using var db = CreateContext();
        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.EnsureProvisionedAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(false, null, null, "role lacks CREATEROLE"));

        var installer = CreateInstaller(db, dataStores: dataStores.Object);

        var firstReview = await installer.InspectAsync(first);
        Assert.IsTrue((await installer.InstallAsync(Request(first, firstReview))).Success);

        var second = await _fixture.CreatePackageAsync(
            version: "1.1.0",
            manifestOverride: PackageInstallerFixture.BuildManifest(
                PackageInstallerFixture.ModuleId, "1.1.0", dataStore: "isolated"));

        var secondReview = await installer.InspectAsync(second);
        var upgrade = await installer.InstallAsync(Request(second, secondReview));

        Assert.IsFalse(upgrade.Success);
        Assert.AreEqual("DATASTORE_PROVISIONING_FAILED", upgrade.Issues[0].Code);

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", row.Version);

        var live = Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", ModulePackageManifest.TryReadFile(Path.Combine(live, "manifest.json"))?.Version);
    }

    // ---------------------------------------------------------------- rollback

    [TestMethod]
    public async Task RollbackAsync_RetainedVersion_ReplacesCurrentVersion()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");

        await using var db = CreateContext();
        var installer = CreateInstaller(db);

        var firstReview = await installer.InspectAsync(first);
        Assert.IsTrue((await installer.InstallAsync(Request(first, firstReview))).Success);

        var second = await _fixture.CreatePackageAsync(version: "1.1.0");
        var secondReview = await installer.InspectAsync(second);
        Assert.IsTrue((await installer.InstallAsync(Request(second, secondReview))).Success);

        var result = await installer.RollbackAsync(PackageInstallerFixture.ModuleId);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual("1.0.0", result.Version);
        Assert.AreEqual("1.1.0", result.PreviousVersion);

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", row.Version);
        Assert.AreEqual(ModuleInstallStatus.RestartPending, row.Status);

        var live = Path.Combine(_fixture.ModulesRoot, PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.0.0", ModulePackageManifest.TryReadFile(Path.Combine(live, "manifest.json"))?.Version);
    }

    [TestMethod]
    public async Task RollbackAsync_NothingRetained_Fails()
    {
        await using var db = CreateContext();
        var installer = CreateInstaller(db);

        var result = await installer.RollbackAsync(PackageInstallerFixture.ModuleId);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "no retained previous version");
    }

    [TestMethod]
    public async Task RollbackAsync_RetainedVersionWithdrawn_Refuses()
    {
        var first = await _fixture.CreatePackageAsync(version: "1.0.0");

        await using var db = CreateContext();

        // Nothing is withdrawn while both versions are installed; the store withdraws 1.0.0 only
        // afterwards, which is what a security revocation looks like.
        var withdrawnVersion = "-";

        var store = new Mock<IModuleStoreService>();
        store
            .Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string? version, CancellationToken _) => new ModuleStoreResolution(
                new ModuleStorePackage(id, "Receipts", version ?? "1.0.0", "x.dncpkg", 1, "abc",
                    "example-org", [], [], "test-store-key",
                    Yanked: version == withdrawnVersion,
                    YankReason: version == withdrawnVersion ? "Critical vulnerability" : null),
                "unused",
                null));

        var installer = CreateInstaller(db, store: store.Object);

        var firstReview = await installer.InspectAsync(first);
        Assert.IsTrue((await installer.InstallAsync(Request(first, firstReview))).Success);

        var second = await _fixture.CreatePackageAsync(version: "1.1.0");
        var secondReview = await installer.InspectAsync(second);
        Assert.IsTrue((await installer.InstallAsync(Request(second, secondReview))).Success);

        withdrawnVersion = "1.0.0";

        var result = await installer.RollbackAsync(PackageInstallerFixture.ModuleId);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error ?? string.Empty, "withdrawn");

        var row = await db.InstalledModules.SingleAsync(m => m.ModuleId == PackageInstallerFixture.ModuleId);
        Assert.AreEqual("1.1.0", row.Version);
    }

    // ------------------------------------------------- runtime selection (RID)

    [TestMethod]
    public void SelectRuntimeDirectory_PortableLinuxPackageOnDistroHost_SelectsLinuxDirectory()
    {
        // Regression: a package publishing the portable RID (linux-x64) was rejected on Ubuntu,
        // whose host RID is ubuntu.24.04-x64, because only the literal OS segment was compared.
        var root = CreateRuntimeLayout("linux-x64", "win-x64");

        var selected = ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64");

        Assert.AreEqual(RuntimeLayoutPath(root, "linux-x64"), selected);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_GlibcDistroHostWithMuslAndGlibcPackages_SelectsGlibcDirectory()
    {
        var root = CreateRuntimeLayout("linux-musl-x64", "linux-x64");

        var selected = ModulePackageInstaller.SelectRuntimeDirectory(root, "debian.12-x64");

        Assert.AreEqual(RuntimeLayoutPath(root, "linux-x64"), selected);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_AlpineHostWithMuslAndGlibcPackages_SelectsMuslDirectory()
    {
        var root = CreateRuntimeLayout("linux-x64", "linux-musl-x64");

        var selected = ModulePackageInstaller.SelectRuntimeDirectory(root, "alpine.3.20-x64");

        Assert.AreEqual(RuntimeLayoutPath(root, "linux-musl-x64"), selected);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_ExactDistroRidAlsoShipped_PrefersExactDirectory()
    {
        var root = CreateRuntimeLayout("linux-x64", "ubuntu.24.04-x64");

        var selected = ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64");

        Assert.AreEqual(RuntimeLayoutPath(root, "ubuntu.24.04-x64"), selected);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_ForeignOperatingSystemOnly_ThrowsNoMatchingRuntime()
    {
        var root = CreateRuntimeLayout("win-x64");

        var exception = Assert.ThrowsExactly<ModulePackageException>(
            () => ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64"));

        Assert.AreEqual(PackageIssueCodes.NoMatchingRuntime, exception.Issues[0].Code);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_MuslPackageOnGlibcHost_ThrowsNoMatchingRuntime()
    {
        var root = CreateRuntimeLayout("linux-musl-x64", "win-x64");

        var exception = Assert.ThrowsExactly<ModulePackageException>(
            () => ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64"));

        Assert.AreEqual(PackageIssueCodes.NoMatchingRuntime, exception.Issues[0].Code);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_TwoCompatibleDistroDirectories_ThrowsAmbiguous()
    {
        var root = CreateRuntimeLayout("debian.12-x64", "fedora.41-x64");

        var exception = Assert.ThrowsExactly<ModulePackageException>(
            () => ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64"));

        Assert.AreEqual(PackageIssueCodes.NoMatchingRuntime, exception.Issues[0].Code);
    }

    [TestMethod]
    public void SelectRuntimeDirectory_BinariesDirectlyUnderFiles_ReturnsFilesDirectory()
    {
        var root = CreateRuntimeLayout();
        File.WriteAllText(
            Path.Combine(root, ModulePackageFormat.FilesDirectoryName, PackageInstallerFixture.ModuleId + ".dll"),
            "fake assembly");

        var selected = ModulePackageInstaller.SelectRuntimeDirectory(root, "ubuntu.24.04-x64");

        Assert.AreEqual(Path.Combine(root, ModulePackageFormat.FilesDirectoryName), selected);
    }

    private string CreateRuntimeLayout(params string[] rids)
    {
        var root = Path.Combine(_fixture.Root, "rid-" + Guid.NewGuid().ToString("N"));
        var files = Path.Combine(root, ModulePackageFormat.FilesDirectoryName);
        Directory.CreateDirectory(files);

        foreach (var rid in rids)
        {
            Directory.CreateDirectory(Path.Combine(files, rid));
        }

        return root;
    }

    private static string RuntimeLayoutPath(string root, string rid) =>
        Path.Combine(root, ModulePackageFormat.FilesDirectoryName, rid);

    // ---------------------------------------------------------------- doubles

    private sealed class FakeRunner : IModuleInstallRunner
    {
        public bool CanStartModules { get; init; } = true;

        public bool Healthy { get; set; } = true;

        public string? Reason { get; set; }

        public List<string> Calls { get; } = [];

        public Task StopAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            Calls.Add("stop:" + moduleId);
            return Task.CompletedTask;
        }

        public Task<ModuleInstallHealth> StartAndAwaitHealthyAsync(
            string moduleId, CancellationToken cancellationToken = default)
        {
            Calls.Add("start:" + moduleId);
            return Task.FromResult(new ModuleInstallHealth(Healthy, Healthy ? null : Reason));
        }
    }
}
