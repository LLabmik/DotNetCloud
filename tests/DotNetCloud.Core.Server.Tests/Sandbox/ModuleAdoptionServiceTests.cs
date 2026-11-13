using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleAdoptionService"/>: the upgrade path classifies every installed
/// module, adopts only eligible schemas, and applies the bundled-only legacy override.
/// </summary>
[TestClass]
public class ModuleAdoptionServiceTests
{
    private const string BundledModule = "org.example.bundled";
    private const string StoreModule = "com.thirdparty.store";

    private string _dbName = null!;
    private string _modulesDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"ModuleAdoptionServiceTests_{Guid.NewGuid():N}";
        _modulesDirectory = Path.Combine(Path.GetTempPath(), $"dnc-adoption-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_modulesDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_modulesDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of the per-test modules directory.
        }
    }

    /// <summary>
    /// Writes a deployed module directory exactly as the supervisor would find it: the executable
    /// plus the manifest that decides the schema mode.
    /// </summary>
    private void WriteDeployedModule(string moduleId, ModuleDataStoreMode mode)
    {
        var moduleDir = Path.Combine(_modulesDirectory, moduleId);
        Directory.CreateDirectory(moduleDir);
        File.WriteAllText(Path.Combine(moduleDir, $"{moduleId}.dll"), string.Empty);

        var dataStore = mode == ModuleDataStoreMode.Isolated ? "isolated" : "none";
        File.WriteAllText(
            Path.Combine(moduleDir, "manifest.json"),
            $$"""
            {
              "manifestVersion": 2,
              "id": "{{moduleId}}",
              "name": "Adoption test module",
              "version": "1.0.0",
              "moduleApiVersion": 1,
              "minCoreVersion": "0.7.0",
              "dataStore": "{{dataStore}}",
              "handlesUserErasure": true
            }
            """);
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private ServiceProvider CreateProvider(NoopAuditLogger? auditLogger = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        services.AddSingleton<IAuditLogger>(auditLogger ?? new NoopAuditLogger());
        return services.BuildServiceProvider();
    }

    private void SeedModule(
        string moduleId,
        bool isRequired = false,
        ModuleTrustTier tier = ModuleTrustTier.Bundled,
        ModuleDataStoreMode mode = ModuleDataStoreMode.Isolated,
        bool provisioned = false)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = moduleId,
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow,
            IsRequired = isRequired,
            TrustTier = tier,
            DataStoreMode = mode,
            SandboxStatus = ModuleSandboxStatus.PendingProvisioning,
        });

        if (provisioned)
        {
            db.ModuleDataStores.Add(new ModuleDataStore
            {
                ModuleId = moduleId,
                Provider = "PostgreSQL",
                SchemaName = "dnc_example_00000000",
                PrincipalName = "dnc_mod_example_00000000",
                EncryptedConnectionString = "ciphertext",
                Status = ModuleDataStoreStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        db.SaveChanges();
    }

    private ModuleAdoptionService CreateService(
        ServiceProvider provider,
        Mock<IModuleDataStoreProvisioningService> provisioning,
        bool allowLegacyAccess = true) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provisioning.Object,
            new ModuleDiscoveryService(
                NullLogger<ModuleDiscoveryService>.Instance,
                Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _modulesDirectory })),
            new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance),
            Options.Create(new ProcessSupervisorOptions
            {
                AllowLegacyAccess = allowLegacyAccess,
                ModulesDirectory = _modulesDirectory,
            }),
            NullLogger<ModuleAdoptionService>.Instance);

    private static Mock<IModuleDataStoreProvisioningService> ProvisioningWithPrerequisites(bool satisfied)
    {
        var provisioning = new Mock<IModuleDataStoreProvisioningService>();
        provisioning
            .Setup(p => p.CheckPrerequisitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(satisfied
                ? ModuleDataStorePrerequisiteResult.Satisfied
                : new ModuleDataStorePrerequisiteResult(false, ["role lacks CREATEROLE"]));
        return provisioning;
    }

    private ModuleSandboxStatus ReadStatus(string moduleId)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        return db.InstalledModules.AsNoTracking().First(m => m.ModuleId == moduleId).SandboxStatus;
    }

    [TestMethod]
    public async Task GetPreflightAsync_ClassifiesEveryInstalledModule()
    {
        SeedModule("dotnetcloud.core", isRequired: true);
        SeedModule("org.example.none", mode: ModuleDataStoreMode.None);
        SeedModule("org.example.ready", provisioned: true);
        SeedModule(BundledModule);

        using var provider = CreateProvider();
        var service = CreateService(provider, ProvisioningWithPrerequisites(satisfied: true));

        var report = await service.GetPreflightAsync(CancellationToken.None);

        Assert.AreEqual(4, report.Entries.Count);
        Assert.IsFalse(report.HasBlockingProblems);

        ModuleAdoptionOutcome Outcome(string id) =>
            report.Entries.First(e => e.ModuleId == id).Outcome;

        Assert.AreEqual(ModuleAdoptionOutcome.Skipped, Outcome("dotnetcloud.core"));
        Assert.AreEqual(ModuleAdoptionOutcome.Skipped, Outcome("org.example.none"));
        Assert.AreEqual(ModuleAdoptionOutcome.AlreadyProvisioned, Outcome("org.example.ready"));
        Assert.AreEqual(ModuleAdoptionOutcome.Adoptable, Outcome(BundledModule));
    }

    [TestMethod]
    public async Task GetPreflightAsync_WhenPrerequisitesMissing_BlocksAdoptableModules()
    {
        SeedModule(BundledModule);

        using var provider = CreateProvider();
        var service = CreateService(provider, ProvisioningWithPrerequisites(satisfied: false));

        var report = await service.GetPreflightAsync(CancellationToken.None);

        var entry = report.Entries.Single(e => e.ModuleId == BundledModule);
        Assert.AreEqual(ModuleAdoptionOutcome.Blocked, entry.Outcome);
        StringAssert.Contains(entry.Reason, "CREATEROLE");
        Assert.IsTrue(report.HasBlockingProblems);
    }

    [TestMethod]
    public async Task AdoptInstalledModulesAsync_AdoptsOnlyEligibleModules()
    {
        SeedModule(BundledModule);
        SeedModule("org.example.ready", provisioned: true);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(BundledModule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_example_00000000", "dnc_mod_example_00000000", null));

        var service = CreateService(provider, provisioning);

        var report = await service.AdoptInstalledModulesAsync(CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Adopted,
            report.Entries.First(e => e.ModuleId == BundledModule).Outcome);
        provisioning.Verify(
            p => p.AdoptExistingStoreAsync(BundledModule, It.IsAny<CancellationToken>()), Times.Once);
        provisioning.Verify(
            p => p.AdoptExistingStoreAsync("org.example.ready", It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(ModuleSandboxStatus.Active, ReadStatus(BundledModule));
    }

    [TestMethod]
    public async Task AdoptAsync_WhenRequired_IsSkipped()
    {
        SeedModule("dotnetcloud.core", isRequired: true);

        using var provider = CreateProvider();
        var provisioning = new Mock<IModuleDataStoreProvisioningService>();
        var service = CreateService(provider, provisioning);

        var entry = await service.AdoptAsync("dotnetcloud.core", CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Skipped, entry.Outcome);
        provisioning.Verify(
            p => p.AdoptExistingStoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task AdoptAsync_WhenBundledAdoptionFails_AppliesLegacyOverrideAndAudits()
    {
        SeedModule(BundledModule);

        var logger = new NoopAuditLogger();
        using var auditedProvider = CreateProvider(logger);

        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(BundledModule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModuleDataStoreProvisioningResult.Failed("adoption failed"));

        var service = CreateService(auditedProvider, provisioning);

        var entry = await service.AdoptAsync(BundledModule, CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.LegacyOverride, entry.Outcome);
        Assert.AreEqual(ModuleSandboxStatus.LegacyOverride, ReadStatus(BundledModule));
        Assert.IsTrue(logger.Entries.Any(e =>
            e.Description == $"module-legacy-access-used:{BundledModule}"));
    }

    [TestMethod]
    public async Task AdoptAsync_WhenStoreTierAdoptionFails_IsHeldWithError()
    {
        SeedModule(StoreModule, tier: ModuleTrustTier.Store);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(StoreModule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModuleDataStoreProvisioningResult.Failed("adoption failed"));

        var service = CreateService(provider, provisioning);

        var entry = await service.AdoptAsync(StoreModule, CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Failed, entry.Outcome);
        Assert.AreEqual("adoption failed", entry.Reason);
        Assert.AreEqual(ModuleSandboxStatus.Error, ReadStatus(StoreModule));
    }

    [TestMethod]
    public async Task AdoptAsync_WhenBundledFailsAndOverrideDisabled_IsHeldWithError()
    {
        SeedModule(BundledModule);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(BundledModule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModuleDataStoreProvisioningResult.Failed("adoption failed"));

        var service = CreateService(provider, provisioning, allowLegacyAccess: false);

        var entry = await service.AdoptAsync(BundledModule, CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Failed, entry.Outcome);
        Assert.AreEqual(ModuleSandboxStatus.Error, ReadStatus(BundledModule));
    }

    [TestMethod]
    public async Task AdoptAsync_WhenModuleIsNotInstalled_Throws()
    {
        using var provider = CreateProvider();
        var service = CreateService(provider, new Mock<IModuleDataStoreProvisioningService>());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.AdoptAsync("org.example.missing", CancellationToken.None));
    }

    [TestMethod]
    public async Task AdoptAsync_BundledNamespaceModule_PassesTheHistoricalSchemaToProvisioning()
    {
        // Track C: a bundled module keeps its data, so adoption must also move the objects out of
        // the schema the core used before the sandbox existed. The name is derived by the core.
        const string moduleId = "dotnetcloud.bookmarks";
        SeedModule(moduleId);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(moduleId, "bookmarks", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_bookmarks_1a2b3c4d", "dnc_mod_bookmarks_1a2b3c4d", null));

        var service = CreateService(provider, provisioning);

        var entry = await service.AdoptAsync(moduleId, CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Adopted, entry.Outcome);
        provisioning.Verify(
            p => p.AdoptExistingStoreAsync(moduleId, "bookmarks", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.AreEqual(ModuleSandboxStatus.Active, ReadStatus(moduleId));
    }

    [TestMethod]
    public async Task AdoptAsync_ThirdPartyModule_DoesNotAskForAnySchemaMove()
    {
        SeedModule(BundledModule);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(BundledModule, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_example_00000000", "dnc_mod_example_00000000", null));

        var service = CreateService(provider, provisioning);

        await service.AdoptAsync(BundledModule, CancellationToken.None);

        provisioning.Verify(
            p => p.AdoptExistingStoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetPreflightAsync_WhenPersistedModeIsNoneButManifestIsIsolated_IsAdoptable()
    {
        // Track C: a module converted to a sandboxed host still has dataStore "none" persisted from
        // the previous release until the supervisor's discovery upsert runs. Trusting the column
        // would skip adoption, so the supervisor would provision an empty schema and the module's
        // real data would silently stay behind in the legacy schema.
        const string moduleId = "dotnetcloud.bookmarks";
        SeedModule(moduleId, mode: ModuleDataStoreMode.None);
        WriteDeployedModule(moduleId, ModuleDataStoreMode.Isolated);

        using var provider = CreateProvider();
        var service = CreateService(provider, ProvisioningWithPrerequisites(satisfied: true));

        var report = await service.GetPreflightAsync(CancellationToken.None);

        var entry = report.Entries.Single(e => e.ModuleId == moduleId);
        Assert.AreEqual(ModuleAdoptionOutcome.Adoptable, entry.Outcome);
        Assert.AreEqual(nameof(ModuleDataStoreMode.Isolated), entry.DataStoreMode);
    }

    [TestMethod]
    public async Task AdoptAsync_WhenPersistedModeIsNoneButManifestIsIsolated_AdoptsAndMovesLegacySchema()
    {
        const string moduleId = "dotnetcloud.bookmarks";
        SeedModule(moduleId, mode: ModuleDataStoreMode.None);
        WriteDeployedModule(moduleId, ModuleDataStoreMode.Isolated);

        using var provider = CreateProvider();
        var provisioning = ProvisioningWithPrerequisites(satisfied: true);
        provisioning
            .Setup(p => p.AdoptExistingStoreAsync(moduleId, "bookmarks", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_bookmarks_1a2b3c4d", "dnc_mod_bookmarks_1a2b3c4d", null));

        var service = CreateService(provider, provisioning);

        var entry = await service.AdoptAsync(moduleId, CancellationToken.None);

        Assert.AreEqual(ModuleAdoptionOutcome.Adopted, entry.Outcome);
        provisioning.Verify(
            p => p.AdoptExistingStoreAsync(moduleId, "bookmarks", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.AreEqual(ModuleSandboxStatus.Active, ReadStatus(moduleId));
    }

    [TestMethod]
    public async Task GetPreflightAsync_WhenManifestIsNotDeployed_UsesPersistedMode()
    {
        // A row that already records Isolated must still be adopted when the binaries have not been
        // laid down yet (or were installed from a package outside the modules directory).
        SeedModule(BundledModule, mode: ModuleDataStoreMode.Isolated);

        using var provider = CreateProvider();
        var service = CreateService(provider, ProvisioningWithPrerequisites(satisfied: true));

        var report = await service.GetPreflightAsync(CancellationToken.None);

        var entry = report.Entries.Single(e => e.ModuleId == BundledModule);
        Assert.AreEqual(ModuleAdoptionOutcome.Adoptable, entry.Outcome);
        Assert.AreEqual(nameof(ModuleDataStoreMode.Isolated), entry.DataStoreMode);
    }
}
