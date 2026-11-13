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
/// Tests for <see cref="ModuleQuotaCache"/>: effective quotas are cached for a TTL so rate
/// limiting never queries the database on the hot path, and an explicit invalidation wins.
/// </summary>
[TestClass]
public class ModuleQuotaCacheTests
{
    private const string ModuleId = "dotnetcloud.cache-test";

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"ModuleQuotaCacheTests_{Guid.NewGuid():N}";

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        return services.BuildServiceProvider();
    }

    private ModuleQuotaResolver CreateResolver(ServiceProvider provider) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleQuotasOptions()),
            Options.Create(new ProcessSupervisorOptions()));

    private void SetConnectionLimit(int limit)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var row = db.ModuleQuotas.FirstOrDefault(q => q.ModuleId == ModuleId);
        if (row is null)
        {
            db.ModuleQuotas.Add(new ModuleQuota
            {
                ModuleId = ModuleId,
                ConnectionLimit = limit,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            db.ModuleQuotas.Update(row);
            row.ConnectionLimit = limit;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    [TestMethod]
    public async Task GetAsync_WithinTtl_ReturnsTheCachedQuota()
    {
        SetConnectionLimit(4);

        using var provider = CreateProvider();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        var cache = new ModuleQuotaCache(CreateResolver(provider), clock, TimeSpan.FromMinutes(1));

        Assert.AreEqual(4, (await cache.GetAsync(ModuleId)).ConnectionLimit);

        SetConnectionLimit(7);
        Assert.AreEqual(
            4,
            (await cache.GetAsync(ModuleId)).ConnectionLimit,
            "The cached value must win inside the TTL");
    }

    [TestMethod]
    public async Task GetAsync_AfterTtl_ResolvesAgain()
    {
        SetConnectionLimit(4);

        using var provider = CreateProvider();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        var cache = new ModuleQuotaCache(CreateResolver(provider), clock, TimeSpan.FromMinutes(1));

        Assert.AreEqual(4, (await cache.GetAsync(ModuleId)).ConnectionLimit);

        SetConnectionLimit(7);
        clock.Now = clock.Now.AddMinutes(1).AddSeconds(1);

        Assert.AreEqual(7, (await cache.GetAsync(ModuleId)).ConnectionLimit);
    }

    [TestMethod]
    public async Task Invalidate_ForcesReResolve()
    {
        SetConnectionLimit(4);

        using var provider = CreateProvider();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
        var cache = new ModuleQuotaCache(CreateResolver(provider), clock, TimeSpan.FromMinutes(1));

        Assert.AreEqual(4, (await cache.GetAsync(ModuleId)).ConnectionLimit);

        SetConnectionLimit(7);
        cache.Invalidate(ModuleId);

        Assert.AreEqual(7, (await cache.GetAsync(ModuleId)).ConnectionLimit);
    }
}
