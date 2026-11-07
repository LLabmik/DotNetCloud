using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Quotas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Tests.Quotas;

/// <summary>
/// Tests for <see cref="ModuleQuotaResolver"/>: the effective limit is
/// <c>min(manifest request, admin override, policy ceiling)</c>, and a dimension with no value on
/// any level stays unlimited.
/// </summary>
[TestClass]
public class ModuleQuotaResolverTests
{
    private const string ModuleId = "dotnetcloud.quota-test";

    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"ModuleQuotaResolverTests_{Guid.NewGuid():N}";

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        return services.BuildServiceProvider();
    }

    private static ModuleQuotaResolver CreateResolver(
        ServiceProvider provider, ModuleQuotasOptions? policy = null) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(policy ?? new ModuleQuotasOptions()),
            Options.Create(new ProcessSupervisorOptions()));

    private void SeedOverride(Action<ModuleQuota> configure)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var row = new ModuleQuota { ModuleId = ModuleId, UpdatedAtUtc = DateTime.UtcNow };
        configure(row);
        db.ModuleQuotas.Add(row);
        db.SaveChanges();
    }

    [TestMethod]
    public async Task ResolveAsync_WithNoRequestsOrOverrides_UsesPolicyDefaults()
    {
        using var provider = CreateProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(ModuleId, new ModuleQuotaRequest());

        Assert.AreEqual(512, quota.MemoryLimitMb, "Supervisor default memory limit should apply");
        Assert.AreEqual(10, quota.ConnectionLimit, "Policy default connection limit should apply");
        Assert.AreEqual(1024, quota.PidsLimit, "Task limit is policy-wide");
        Assert.IsNull(quota.CpuPercent, "CPU stays unlimited when nothing requests it");
        Assert.IsNull(quota.DataDirMb);
        Assert.IsNull(quota.DatabaseMb);
    }

    [TestMethod]
    public async Task ResolveAsync_ManifestRequestBelowCeiling_IsHonoured()
    {
        using var provider = CreateProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(
            ModuleId,
            new ModuleQuotaRequest(MemoryLimitMb: 256, CpuPercent: 25, ConnectionLimit: 5));

        Assert.AreEqual(256, quota.MemoryLimitMb);
        Assert.AreEqual(25, quota.CpuPercent);
        Assert.AreEqual(5, quota.ConnectionLimit);
    }

    [TestMethod]
    public async Task ResolveAsync_RequestAboveCeiling_IsClamped()
    {
        using var provider = CreateProvider();
        var resolver = CreateResolver(
            provider,
            new ModuleQuotasOptions { MaxMemoryLimitMb = 1024, MaxCpuPercent = 50, MaxConnectionLimit = 8 });

        var quota = await resolver.ResolveAsync(
            ModuleId,
            new ModuleQuotaRequest(MemoryLimitMb: 8192, CpuPercent: 100, ConnectionLimit: 64));

        Assert.AreEqual(1024, quota.MemoryLimitMb);
        Assert.AreEqual(50, quota.CpuPercent);
        Assert.AreEqual(8, quota.ConnectionLimit);
    }

    [TestMethod]
    public async Task ResolveAsync_AdminOverrideLowersManifestRequest()
    {
        SeedOverride(row =>
        {
            row.MemoryLimitMb = 128;
            row.CpuPercent = 10;
            row.ConnectionLimit = 3;
        });

        using var provider = CreateProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(
            ModuleId,
            new ModuleQuotaRequest(MemoryLimitMb: 1024, CpuPercent: 100, ConnectionLimit: 16));

        Assert.AreEqual(128, quota.MemoryLimitMb);
        Assert.AreEqual(10, quota.CpuPercent);
        Assert.AreEqual(3, quota.ConnectionLimit);
    }

    [TestMethod]
    public async Task ResolveAsync_AdminOverrideAboveCeiling_IsClampedToPolicy()
    {
        SeedOverride(row => row.ConnectionLimit = 64);

        using var provider = CreateProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(ModuleId, new ModuleQuotaRequest());

        Assert.AreEqual(20, quota.ConnectionLimit, "Policy MaxConnectionLimit must cap the override");
    }

    [TestMethod]
    public async Task ResolveAsync_ZeroOrNegativeValues_AreTreatedAsUnset()
    {
        SeedOverride(row =>
        {
            row.MemoryLimitMb = 0;
            row.CpuPercent = -5;
        });

        using var provider = CreateProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(ModuleId, new ModuleQuotaRequest(MemoryLimitMb: 0));

        Assert.AreEqual(512, quota.MemoryLimitMb, "A zero request falls back to the default");
        Assert.IsNull(quota.CpuPercent);
    }

    [TestMethod]
    public async Task ResolveAsync_WithDatabaseFailure_FallsBackToPolicy()
    {
        // No schema/context registered: the resolver must not throw and must use the policy ceiling.
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var quota = await resolver.ResolveAsync(
            ModuleId, new ModuleQuotaRequest(MemoryLimitMb: 2048));

        Assert.AreEqual(2048, quota.MemoryLimitMb);
        Assert.AreEqual(10, quota.ConnectionLimit);
    }
}
