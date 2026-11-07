using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Quotas;

/// <summary>
/// Tests for <see cref="ModuleUsageMonitor"/>: limit arithmetic, the 90% recovery hysteresis, and
/// the suspension/resume flow driven by measured data-directory usage.
/// </summary>
[TestClass]
public class ModuleUsageMonitorTests
{
    private const string ModuleId = "org.example.usage-monitor";
    private const long Megabyte = 1024L * 1024L;

    private string _dbName = null!;
    private string _dataRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"ModuleUsageMonitorTests_{Guid.NewGuid():N}";
        _dataRoot = Path.Combine(Path.GetTempPath(), $"dnc-monitor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        services.AddSingleton<IAuditLogger>(new NoopAuditLogger());
        return services.BuildServiceProvider();
    }

    private void SeedModule(ModuleSandboxStatus status, int dataDirMb)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = ModuleId,
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow,
            DataStoreMode = ModuleDataStoreMode.Isolated,
            SandboxStatus = status,
        });

        db.ModuleQuotas.Add(new ModuleQuota
        {
            ModuleId = ModuleId,
            DataDirMb = dataDirMb,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.ModuleDataStores.Add(new ModuleDataStore
        {
            ModuleId = ModuleId,
            Provider = "PostgreSQL",
            SchemaName = "dnc_usage_monitor_00000000",
            PrincipalName = "dnc_mod_usage_monitor_00000000",
            Status = ModuleDataStoreStatus.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        db.SaveChanges();
    }

    private ModuleUsageMonitor CreateMonitor(ServiceProvider provider, IProcessSupervisor supervisor)
    {
        var resolver = new ModuleQuotaResolver(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleQuotasOptions()),
            Options.Create(new ProcessSupervisorOptions()));

        var measurer = new ModuleUsageMeasurer(
            new CoreDatabaseOptions("Host=127.0.0.1;Port=1;Database=unused;Timeout=1", DatabaseProvider.PostgreSQL),
            NullLogger<ModuleUsageMeasurer>.Instance,
            _dataRoot);

        return new ModuleUsageMonitor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            resolver,
            measurer,
            supervisor,
            Options.Create(new ModuleQuotasOptions()),
            TimeProvider.System,
            NullLogger<ModuleUsageMonitor>.Instance);
    }

    private void WriteDataFile(int megabytes)
    {
        var dataDirectory = Path.Combine(_dataRoot, "modules", ModuleId, "data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllBytes(Path.Combine(dataDirectory, "payload.bin"), new byte[megabytes * Megabyte]);
    }

    private ModuleSandboxStatus ReadStatus()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        return db.InstalledModules.AsNoTracking().First(m => m.ModuleId == ModuleId).SandboxStatus;
    }

    [TestMethod]
    public void IsOverLimit_ComparesBytesAgainstMegabytes()
    {
        Assert.IsTrue(ModuleUsageMonitor.IsOverLimit(2 * Megabyte, 1));
        Assert.IsFalse(ModuleUsageMonitor.IsOverLimit(Megabyte, 1));
        Assert.IsFalse(ModuleUsageMonitor.IsOverLimit(null, 1), "Unmeasured usage is never a violation");
        Assert.IsFalse(ModuleUsageMonitor.IsOverLimit(2 * Megabyte, null), "Unlimited dimensions are never violated");
    }

    [TestMethod]
    public void IsWithinRecovery_RequiresNinetyPercent()
    {
        Assert.IsTrue(ModuleUsageMonitor.IsWithinRecovery(Megabyte / 2, 1));
        Assert.IsTrue(ModuleUsageMonitor.IsWithinRecovery(null, 1));
        Assert.IsFalse(ModuleUsageMonitor.IsWithinRecovery(Megabyte, 1), "Exactly at the limit is not recovery");
    }

    [TestMethod]
    public async Task RunOnceAsync_WhenDataDirectoryOverQuota_StopsModuleAndMarksQuotaExceeded()
    {
        SeedModule(ModuleSandboxStatus.Active, dataDirMb: 1);
        WriteDataFile(megabytes: 2);

        using var provider = CreateProvider();
        var supervisor = new Mock<IProcessSupervisor>();
        var monitor = CreateMonitor(provider, supervisor.Object);

        await monitor.RunOnceAsync(CancellationToken.None);

        supervisor.Verify(s => s.StopModuleAsync(ModuleId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(ModuleSandboxStatus.QuotaExceeded, ReadStatus());
    }

    [TestMethod]
    public async Task RunOnceAsync_WhenAlreadySuspended_DoesNotStopTwice()
    {
        SeedModule(ModuleSandboxStatus.QuotaExceeded, dataDirMb: 1);
        WriteDataFile(megabytes: 2);

        using var provider = CreateProvider();
        var supervisor = new Mock<IProcessSupervisor>();
        var monitor = CreateMonitor(provider, supervisor.Object);

        await monitor.RunOnceAsync(CancellationToken.None);

        supervisor.Verify(s => s.StopModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(ModuleSandboxStatus.QuotaExceeded, ReadStatus());
    }

    [TestMethod]
    public async Task RunOnceAsync_WhenUsageRecovers_ClearsTheSuspension()
    {
        SeedModule(ModuleSandboxStatus.QuotaExceeded, dataDirMb: 1);
        WriteDataFile(megabytes: 0);

        using var provider = CreateProvider();
        var monitor = CreateMonitor(provider, new Mock<IProcessSupervisor>().Object);

        await monitor.RunOnceAsync(CancellationToken.None);

        Assert.AreEqual(ModuleSandboxStatus.Active, ReadStatus());
    }

    [TestMethod]
    public async Task RunOnceAsync_WhenNoMeasuredLimitConfigured_DoesNotStopModules()
    {
        SeedModule(ModuleSandboxStatus.Active, dataDirMb: 0);
        WriteDataFile(megabytes: 2);

        using var provider = CreateProvider();
        var supervisor = new Mock<IProcessSupervisor>();
        var monitor = CreateMonitor(provider, supervisor.Object);

        await monitor.RunOnceAsync(CancellationToken.None);

        supervisor.Verify(s => s.StopModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(ModuleSandboxStatus.Active, ReadStatus());
    }
}
