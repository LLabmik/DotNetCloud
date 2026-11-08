using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Regression tests for module start/stop status persistence. Mirrors production
/// by configuring <see cref="QueryTrackingBehavior.NoTracking"/> so the tests fail
/// if a status update forgets to use <c>AsTracking()</c>.
/// </summary>
[TestClass]
public class AdminModuleServiceTests
{
    private const string ModuleId = "dotnetcloud.testmodule";

    // Unique per test method: the InMemory store is keyed by name, so a fresh
    // name avoids PK collisions across tests while still being reused between
    // the seed and verify contexts WITHIN a single test.
    private string _dbName = null!;

    private CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"AdminModuleServiceTests_{Guid.NewGuid():N}";
    }

    private static Mock<IProcessSupervisor> CreateSupervisor()
    {
        var supervisor = new Mock<IProcessSupervisor>();
        supervisor
            .Setup(s => s.StartModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        supervisor
            .Setup(s => s.StopModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return supervisor;
    }

    private static AdminModuleService CreateService(
        CoreDbContext db,
        IProcessSupervisor supervisor,
        IModuleDataStoreProvisioningService? dataStores = null,
        ModuleCredentialStore? credentials = null,
        string? modulesDirectory = null)
    {
        var quotas = CreateQuotaResolver(db);

        var discovery = new ModuleDiscoveryService(
            NullLogger<ModuleDiscoveryService>.Instance,
            Options.Create(new ProcessSupervisorOptions
            {
                ModulesDirectory = modulesDirectory
                    ?? Path.Combine(Path.GetTempPath(), "dnc-modules-" + Guid.NewGuid().ToString("N")),
            }));

        return new(
            db,
            supervisor,
            dataStores ?? Mock.Of<IModuleDataStoreProvisioningService>(),
            quotas,
            new ModuleQuotaCache(quotas, TimeProvider.System),
            Mock.Of<IModuleAccessEvaluator>(),
            discovery,
            credentials ?? new ModuleCredentialStore(),
            Mock.Of<IModulePackageInstaller>(),
            Mock.Of<IModuleStoreService>(),
            NullLogger<AdminModuleService>.Instance);
    }

    private static ModuleQuotaResolver CreateQuotaResolver(CoreDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddOptions<ModuleQuotasOptions>();
        services.AddOptions<ProcessSupervisorOptions>();

        // Deliberately not disposed: the resolver holds the scope factory and the tests resolve
        // against the same in-memory context for the lifetime of the test process.
        var provider = services.BuildServiceProvider();

        return new ModuleQuotaResolver(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<ModuleQuotasOptions>>(),
            provider.GetRequiredService<IOptions<ProcessSupervisorOptions>>());
    }

    private static void SeedModule(
        CoreDbContext db,
        string status,
        bool isRequired = false,
        ModuleDataStoreMode dataStoreMode = ModuleDataStoreMode.None,
        ModuleSandboxStatus sandboxStatus = ModuleSandboxStatus.Active)
    {
        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = ModuleId,
            Version = "1.0.0",
            Status = status,
            InstalledAt = DateTime.UtcNow,
            IsRequired = isRequired,
            DataStoreMode = dataStoreMode,
            SandboxStatus = sandboxStatus
        });
        db.SaveChanges();
    }

    private static void SeedStore(
        CoreDbContext db,
        ModuleDataStoreStatus status,
        string? encryptedConnectionString = "ciphertext",
        string? lastError = null)
    {
        db.ModuleDataStores.Add(new ModuleDataStore
        {
            ModuleId = ModuleId,
            Provider = "PostgreSQL",
            SchemaName = "dnc_example_abc12345",
            PrincipalName = "dnc_mod_example_abc12345",
            EncryptedConnectionString = encryptedConnectionString,
            Status = status,
            LastError = lastError,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    [TestMethod]
    public async Task StartModuleAsync_PersistsEnabledStatus()
    {
        using (var seed = CreateContext())
        {
            SeedModule(seed, "Disabled");
            var service = CreateService(seed, CreateSupervisor().Object);
            var result = await service.StartModuleAsync(ModuleId, CancellationToken.None);
            Assert.IsTrue(result);
        }

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == ModuleId);
        Assert.IsNotNull(module);
        Assert.AreEqual("Enabled", module!.Status);
    }

    [TestMethod]
    public async Task StopModuleAsync_PersistsDisabledStatus()
    {
        using (var seed = CreateContext())
        {
            SeedModule(seed, "Enabled");
            var service = CreateService(seed, CreateSupervisor().Object);
            var result = await service.StopModuleAsync(ModuleId, CancellationToken.None);
            Assert.IsTrue(result);
        }

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == ModuleId);
        Assert.IsNotNull(module);
        Assert.AreEqual("Disabled", module!.Status);
    }

    [TestMethod]
    public async Task GetDataStoreAsync_WhenModuleMissing_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.GetDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetDataStoreAsync_WhenStoreAbsent_ReportsNotProvisioned()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.GetDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Provisioned);
        Assert.AreEqual("NotProvisioned", result.Status);
        Assert.AreEqual("Isolated", result.DataStoreMode);
    }

    [TestMethod]
    public async Task GetDataStoreAsync_WhenStorePresent_ReportsSchemaAndReadableCredentials()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.GetConnectionStringAsync(ModuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Host=db;Database=dotnetcloud");

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.GetDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Provisioned);
        Assert.IsTrue(result.CredentialsReadable);
        Assert.AreEqual("dnc_example_abc12345", result.SchemaName);
        Assert.AreEqual("dnc_mod_example_abc12345", result.PrincipalName);
        Assert.AreEqual("Active", result.Status);
        Assert.AreEqual(10, result.ConnectionLimit);
    }

    [TestMethod]
    public async Task GetDataStoreAsync_WhenCredentialsCannotBeDecrypted_FlagsRecovery()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.GetConnectionStringAsync(ModuleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.GetDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Provisioned);
        Assert.IsFalse(result.CredentialsReadable);
    }

    [TestMethod]
    public async Task ProvisionDataStoreAsync_WhenModuleNotIsolated_ReturnsFailureWithoutProvisioning()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.ProvisionDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        StringAssert.Contains(result.Error, "isolated");
        dataStores.Verify(
            d => d.EnsureProvisionedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProvisionDataStoreAsync_WhenIsolated_UsesQuotaLimitAndMarksSandboxActive()
    {
        using var db = CreateContext();
        SeedModule(
            db,
            "Enabled",
            dataStoreMode: ModuleDataStoreMode.Isolated,
            sandboxStatus: ModuleSandboxStatus.PendingProvisioning);

        db.ModuleQuotas.Add(new ModuleQuota
        {
            ModuleId = ModuleId,
            ConnectionLimit = 15,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.EnsureProvisionedAsync(ModuleId, 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_example_abc12345", "dnc_mod_example_abc12345", null));

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.ProvisionDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);
        Assert.AreEqual("dnc_example_abc12345", result.SchemaName);

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking().FirstAsync(m => m.ModuleId == ModuleId);
        Assert.AreEqual(ModuleSandboxStatus.Active, module.SandboxStatus);
    }

    [TestMethod]
    public async Task ProvisionDataStoreAsync_WhenProvisioningFails_MarksSandboxError()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.EnsureProvisionedAsync(ModuleId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ModuleDataStoreProvisioningResult.Failed("role creation denied"));

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.ProvisionDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        Assert.AreEqual("role creation denied", result.Error);

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking().FirstAsync(m => m.ModuleId == ModuleId);
        Assert.AreEqual(ModuleSandboxStatus.Error, module.SandboxStatus);
    }

    [TestMethod]
    public async Task RotateDataStoreCredentialsAsync_WhenRotationSucceeds_MarksSandboxActive()
    {
        using var db = CreateContext();
        SeedModule(
            db,
            "Enabled",
            dataStoreMode: ModuleDataStoreMode.Isolated,
            sandboxStatus: ModuleSandboxStatus.Error);
        SeedStore(db, ModuleDataStoreStatus.Error, lastError: "old failure");

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.RotateCredentialsAsync(ModuleId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(
                true, "dnc_example_abc12345", "dnc_mod_example_abc12345", null));

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.RotateDataStoreCredentialsAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking().FirstAsync(m => m.ModuleId == ModuleId);
        Assert.AreEqual(ModuleSandboxStatus.Active, module.SandboxStatus);
    }

    [TestMethod]
    public async Task DestroyDataStoreAsync_WhenStorePresent_DropsStoreAndResetsSandboxStatus()
    {
        using var db = CreateContext();
        SeedModule(db, "Disabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.DestroyDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);
        Assert.AreEqual("dnc_example_abc12345", result.SchemaName);
        dataStores.Verify(d => d.DestroyAsync(ModuleId, It.IsAny<CancellationToken>()), Times.Once);

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking().FirstAsync(m => m.ModuleId == ModuleId);
        Assert.AreEqual(ModuleSandboxStatus.PendingProvisioning, module.SandboxStatus);
    }

    [TestMethod]
    public async Task DestroyDataStoreAsync_WhenNoStoreExists_IsIdempotent()
    {
        using var db = CreateContext();
        SeedModule(db, "Disabled", dataStoreMode: ModuleDataStoreMode.Isolated);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.DestroyDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);
        dataStores.Verify(d => d.DestroyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DestroyDataStoreAsync_WhenEngineFails_ReturnsRedactedErrorAndMarksSandboxError()
    {
        using var db = CreateContext();
        SeedModule(db, "Disabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.DestroyAsync(ModuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("role drop failed; Password=s3cret-value;"));

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.DestroyDataStoreAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        Assert.IsFalse(result.Error!.Contains("s3cret-value", StringComparison.Ordinal),
            "Sanitized errors must never contain a password value.");

        using var verify = CreateContext();
        var module = await verify.InstalledModules.AsNoTracking().FirstAsync(m => m.ModuleId == ModuleId);
        Assert.AreEqual(ModuleSandboxStatus.Error, module.SandboxStatus);
    }

    [TestMethod]
    public async Task GetQuotaAsync_WhenModuleMissing_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db, CreateSupervisor().Object);

        var quota = await service.GetQuotaAsync(ModuleId, CancellationToken.None);

        Assert.IsNull(quota);
    }

    [TestMethod]
    public async Task GetQuotaAsync_WhenNoOverride_ReportsEffectiveDefaults()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");
        var service = CreateService(db, CreateSupervisor().Object);

        var quota = await service.GetQuotaAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(quota);
        Assert.IsNull(quota!.MemoryLimitMb, "No override is stored");
        Assert.AreEqual(512, quota.EffectiveMemoryLimitMb);
        Assert.AreEqual(10, quota.EffectiveConnectionLimit);
        Assert.AreEqual(1024, quota.EffectivePidsLimit);
    }

    [TestMethod]
    public async Task UpdateQuotaAsync_WhenValueIsNegative_ReturnsFailureWithoutPersisting()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");
        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.UpdateQuotaAsync(
            ModuleId,
            new UpdateModuleQuotaDto { MemoryLimitMb = -1 },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        StringAssert.Contains(result.Error, nameof(UpdateModuleQuotaDto.MemoryLimitMb));

        using var verify = CreateContext();
        Assert.IsFalse(await verify.ModuleQuotas.AnyAsync(q => q.ModuleId == ModuleId));
    }

    [TestMethod]
    public async Task UpdateQuotaAsync_WhenOverrideExceedsPolicy_StoresOverrideAndClampsEffective()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");
        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.UpdateQuotaAsync(
            ModuleId,
            new UpdateModuleQuotaDto { MemoryLimitMb = 8192, CpuPercent = 25 },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);
        Assert.AreEqual(8192, result.Quota!.MemoryLimitMb);
        Assert.AreEqual(4096, result.Quota.EffectiveMemoryLimitMb, "Policy MaxMemoryLimitMb clamps the override");
        Assert.AreEqual(25, result.Quota.EffectiveCpuPercent);
    }

    [TestMethod]
    public async Task UpdateQuotaAsync_WithZero_ClearsTheOverride()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");
        db.ModuleQuotas.Add(new ModuleQuota
        {
            ModuleId = ModuleId,
            MemoryLimitMb = 256,
            ConnectionLimit = 4,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();

        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.UpdateQuotaAsync(
            ModuleId,
            new UpdateModuleQuotaDto { MemoryLimitMb = 0, ConnectionLimit = null },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success);
        Assert.IsNull(result.Quota!.MemoryLimitMb);
        Assert.IsNull(result.Quota.ConnectionLimit);
        Assert.AreEqual(10, result.Quota.EffectiveConnectionLimit);
    }

    // -----------------------------------------------------------------------
    // Uninstall (keep vs destroy)
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task UninstallModuleAsync_RequiredModule_ReturnsFailureAndKeepsInstallation()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", isRequired: true);

        var supervisor = CreateSupervisor();
        var service = CreateService(db, supervisor.Object);

        var result = await service.UninstallModuleAsync(
            ModuleId, new UninstallModuleDto { DestroyData = true }, Guid.CreateVersion7(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        StringAssert.Contains(result.Error, "required");

        supervisor.Verify(s => s.StopModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        using var verify = CreateContext();
        Assert.IsTrue(await verify.InstalledModules.AnyAsync(m => m.ModuleId == ModuleId));
    }

    [TestMethod]
    public async Task UninstallModuleAsync_MissingModule_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.UninstallModuleAsync(
            ModuleId, new UninstallModuleDto(), Guid.CreateVersion7(), CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UninstallModuleAsync_KeepData_MarksStoreOrphanedAndDeletesInstallation()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        var supervisor = CreateSupervisor();
        var service = CreateService(db, supervisor.Object, dataStores.Object);

        var result = await service.UninstallModuleAsync(
            ModuleId,
            new UninstallModuleDto { DestroyData = false, RemoveFiles = false },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success, result.Error);
        Assert.IsTrue(result.DataStoreRetained);
        Assert.IsFalse(result.DataStoreDestroyed);
        Assert.IsFalse(result.FilesRemoved);

        dataStores.Verify(
            d => d.DestroyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        supervisor.Verify(s => s.StopModuleAsync(ModuleId, It.IsAny<CancellationToken>()), Times.Once);

        using var verify = CreateContext();
        Assert.IsFalse(await verify.InstalledModules.AnyAsync(m => m.ModuleId == ModuleId));

        var store = await verify.ModuleDataStores.FirstOrDefaultAsync(d => d.ModuleId == ModuleId);
        Assert.IsNotNull(store, "the store row must survive 'keep data'");
        Assert.AreEqual(ModuleDataStoreStatus.Orphaned, store!.Status);
        Assert.IsNull(store.EncryptedConnectionString);
    }

    [TestMethod]
    public async Task UninstallModuleAsync_DestroyData_DropsStoreAndDeletesInstallation()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.DestroyAsync(ModuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.UninstallModuleAsync(
            ModuleId,
            new UninstallModuleDto { DestroyData = true, RemoveFiles = false },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Success, result.Error);
        Assert.IsTrue(result.DataStoreDestroyed);
        Assert.IsFalse(result.DataStoreRetained);

        dataStores.Verify(d => d.DestroyAsync(ModuleId, It.IsAny<CancellationToken>()), Times.Once);

        using var verify = CreateContext();
        Assert.IsFalse(await verify.InstalledModules.AnyAsync(m => m.ModuleId == ModuleId));
    }

    [TestMethod]
    public async Task UninstallModuleAsync_DestroyFails_KeepsInstallationForRetry()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Active);

        var dataStores = new Mock<IModuleDataStoreProvisioningService>();
        dataStores
            .Setup(d => d.DestroyAsync(ModuleId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("password=secret could not drop schema"));

        var service = CreateService(db, CreateSupervisor().Object, dataStores.Object);

        var result = await service.UninstallModuleAsync(
            ModuleId,
            new UninstallModuleDto { DestroyData = true, RemoveFiles = false },
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Success);
        Assert.IsFalse(result.Error!.Contains("secret", StringComparison.OrdinalIgnoreCase),
            "the sanitized error must not leak credentials");

        using var verify = CreateContext();
        Assert.IsTrue(await verify.InstalledModules.AnyAsync(m => m.ModuleId == ModuleId));
    }

    [TestMethod]
    public async Task UninstallModuleAsync_RemovesModuleDirectoryInsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dnc-uninstall-{Guid.NewGuid():N}");
        var modulesRoot = Path.Combine(root, "modules");
        var moduleDirectory = Path.Combine(modulesRoot, ModuleId);
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, $"{ModuleId}.dll"), "fake");

        try
        {
            using var db = CreateContext();
            SeedModule(db, "Enabled");

            var service = CreateService(db, CreateSupervisor().Object, modulesDirectory: modulesRoot);

            var result = await service.UninstallModuleAsync(
                ModuleId,
                new UninstallModuleDto { DestroyData = false, RemoveFiles = true },
                Guid.CreateVersion7(),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Success, result.Error);
            Assert.IsTrue(result.FilesRemoved);
            Assert.IsFalse(Directory.Exists(moduleDirectory));
            Assert.IsTrue(Directory.Exists(modulesRoot), "the modules root itself must never be deleted");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Launch credential
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task GetCredentialAsync_NotInstalled_ReturnsNull()
    {
        using var db = CreateContext();
        var service = CreateService(db, CreateSupervisor().Object);

        Assert.IsNull(await service.GetCredentialAsync(ModuleId));
    }

    [TestMethod]
    public async Task RotateCredentialAsync_NotRunning_RecordsRotation()
    {
        using var db = CreateContext();
        SeedModule(db, "Disabled");

        var credentials = new ModuleCredentialStore();
        var supervisor = CreateSupervisor();
        supervisor.Setup(s => s.IsModuleRunning(ModuleId)).Returns(false);

        var service = CreateService(db, supervisor.Object, credentials: credentials);

        var result = await service.RotateCredentialAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Active);
        Assert.IsNotNull(result.RotatedAtUtc);
        supervisor.Verify(
            s => s.RestartModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RotateCredentialAsync_Running_RestartsModuleToApplyNewSecret()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");

        var supervisor = CreateSupervisor();
        supervisor.Setup(s => s.IsModuleRunning(ModuleId)).Returns(true);
        supervisor
            .Setup(s => s.RestartModuleAsync(ModuleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(db, supervisor.Object);

        var result = await service.RotateCredentialAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        supervisor.Verify(s => s.RestartModuleAsync(ModuleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RevokeCredentialAsync_ReturnsRevokedRecord()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled");

        var credentials = new ModuleCredentialStore();
        credentials.Issue(ModuleId);

        var service = CreateService(db, CreateSupervisor().Object, credentials: credentials);

        var result = await service.RevokeCredentialAsync(ModuleId, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result!.Active);
        Assert.IsNotNull(result.RevokedAtUtc);
        Assert.IsFalse(credentials.HasCredential(ModuleId));
    }

    [TestMethod]
    public async Task ListOrphanedDataStoresAsync_ReturnsOnlyOrphanedStores()
    {
        using var db = CreateContext();
        SeedModule(db, "Enabled", dataStoreMode: ModuleDataStoreMode.Isolated);
        SeedStore(db, ModuleDataStoreStatus.Orphaned);
        db.ModuleDataStores.Add(new ModuleDataStore
        {
            ModuleId = "vendor.active",
            Provider = "PostgreSQL",
            SchemaName = "dnc_active_abc12345",
            PrincipalName = "dnc_mod_active_abc12345",
            Status = ModuleDataStoreStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        db.SaveChanges();

        var service = CreateService(db, CreateSupervisor().Object);

        var result = await service.ListOrphanedDataStoresAsync(CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(ModuleId, result[0].ModuleId);
        Assert.AreEqual("Orphaned", result[0].Status);
    }
}
