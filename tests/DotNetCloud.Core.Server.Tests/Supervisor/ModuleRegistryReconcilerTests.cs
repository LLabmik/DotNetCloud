using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Guards the <c>InstalledModules</c> reconciliation. The live Track C run found that every update
/// was silently dropped: <see cref="CoreDbContext"/> is <c>NoTracking</c> by default, so the registry
/// kept reporting <c>Version 0.0.0</c> and <c>dataStoreMode: None</c> for modules that were actually
/// running sandboxed with an isolated store.
/// </summary>
[TestClass]
public class ModuleRegistryReconcilerTests
{
    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"ModuleRegistryReconcilerTests_{Guid.NewGuid():N}";

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                // Mirror production: CoreDbContext is registered NoTracking, which is what made the
                // reconciliation lose its writes.
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        return services.BuildServiceProvider();
    }

    private static ModuleManifestData V2Manifest(
        string moduleId, string version, ModuleDataStoreMode mode) =>
        new()
        {
            ManifestVersion = 2,
            Id = moduleId,
            Name = moduleId,
            Version = version,
            DataStore = mode == ModuleDataStoreMode.Isolated ? "isolated" : "none",
            HandlesUserErasure = true,
        };

    private void SeedModule(
        string moduleId,
        string version = "0.0.0",
        ModuleTrustTier tier = ModuleTrustTier.Bundled,
        ModuleDataStoreMode mode = ModuleDataStoreMode.None)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = moduleId,
            Version = version,
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow,
            TrustTier = tier,
            DataStoreMode = mode,
            SandboxStatus = ModuleSandboxStatus.PendingProvisioning,
        });

        db.SaveChanges();
    }

    private InstalledModule ReadModule(string moduleId)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        return db.InstalledModules.AsNoTracking().First(m => m.ModuleId == moduleId);
    }

    [TestMethod]
    public async Task ReconcileAsync_ExistingRow_UpdatesVersionAndDataStoreMode()
    {
        // The exact live symptom: a module converted to a v2 sandboxed host kept the stale row.
        const string moduleId = "dotnetcloud.email";
        SeedModule(moduleId);

        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var (added, updated) = await ModuleRegistryReconciler.ReconcileAsync(
            db,
            [new ModuleRegistryEntry(moduleId, V2Manifest(moduleId, "1.2.0", ModuleDataStoreMode.Isolated), false)],
            CancellationToken.None);

        Assert.AreEqual(0, added);
        Assert.AreEqual(1, updated);

        var row = ReadModule(moduleId);
        Assert.AreEqual("1.2.0", row.Version);
        Assert.AreEqual(ModuleDataStoreMode.Isolated, row.DataStoreMode);
        Assert.AreEqual(ModuleTrustTier.Bundled, row.TrustTier);
    }

    [TestMethod]
    public async Task ReconcileAsync_ExistingStoreTierRow_IsNeverPromotedToBundled()
    {
        const string moduleId = "dotnetcloud.bookmarks";
        SeedModule(moduleId, tier: ModuleTrustTier.Store);

        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        await ModuleRegistryReconciler.ReconcileAsync(
            db,
            [new ModuleRegistryEntry(moduleId, V2Manifest(moduleId, "1.2.0", ModuleDataStoreMode.Isolated), false)],
            CancellationToken.None);

        Assert.AreEqual(ModuleTrustTier.Store, ReadModule(moduleId).TrustTier);
    }

    [TestMethod]
    public async Task ReconcileAsync_NewSandboxedModule_IsPendingProvisioning()
    {
        const string moduleId = "dotnetcloud.music";

        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var (added, updated) = await ModuleRegistryReconciler.ReconcileAsync(
            db,
            [new ModuleRegistryEntry(moduleId, V2Manifest(moduleId, "1.0.0", ModuleDataStoreMode.Isolated), false)],
            CancellationToken.None);

        Assert.AreEqual(1, added);
        Assert.AreEqual(0, updated);

        var row = ReadModule(moduleId);
        Assert.AreEqual(ModuleTrustTier.Bundled, row.TrustTier);
        Assert.AreEqual(ModuleSandboxStatus.PendingProvisioning, row.SandboxStatus);
        Assert.AreEqual("Enabled", row.Status);
    }

    [TestMethod]
    public async Task ReconcileAsync_RequiredModule_IsActiveAndRequiredTier()
    {
        const string moduleId = "dotnetcloud.files";
        SeedModule(moduleId, tier: ModuleTrustTier.Bundled);

        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        await ModuleRegistryReconciler.ReconcileAsync(
            db,
            [new ModuleRegistryEntry(moduleId, V2Manifest(moduleId, "1.0.0", ModuleDataStoreMode.None), true)],
            CancellationToken.None);

        var row = ReadModule(moduleId);
        Assert.AreEqual(ModuleTrustTier.Required, row.TrustTier);
        Assert.AreEqual(ModuleSandboxStatus.Active, row.SandboxStatus);
        Assert.IsTrue(row.IsRequired);
    }
}
