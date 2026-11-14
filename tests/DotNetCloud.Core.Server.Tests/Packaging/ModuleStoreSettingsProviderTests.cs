using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.ModulePackaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for the admin-editable module store URL: the effective options layer the
/// <c>dotnetcloud.core</c>/<c>ModuleStoreIndexUrl</c> setting over <c>Modules:Store:IndexUrl</c>,
/// and the store client re-resolves its source so a saved URL applies without a restart.
/// </summary>
[TestClass]
public class ModuleStoreSettingsProviderTests
{
    private const string ConfiguredUrl = "https://configured.test/index/v1/index.json";
    private const string AdminUrl = "https://admin.test/index/v1/index.json";

    private TestDbContextFactory _dbFactory = null!;
    private FakeTimeProvider _time = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"ModuleStoreSettings_{Guid.CreateVersion7():N}")
            .Options;

        _dbFactory = new TestDbContextFactory(options);
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero));
    }

    private ModuleStoreSettingsProvider CreateProvider() =>
        new(_dbFactory, _time, NullLogger<ModuleStoreSettingsProvider>.Instance);

    private async Task SetAdminUrlAsync(string value)
    {
        await using var db = _dbFactory.CreateDbContext();

        var existing = await db.SystemSettings
            .AsTracking()
            .FirstOrDefaultAsync(setting => setting.Module == SystemSettingKeys.CoreModule
                && setting.Key == SystemSettingKeys.ModuleStoreIndexUrl);

        if (existing is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Module = SystemSettingKeys.CoreModule,
                Key = SystemSettingKeys.ModuleStoreIndexUrl,
                Value = value,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static ModuleStoreOptions ConfiguredOptions() =>
        new() { IndexUrl = ConfiguredUrl };

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_NoAdminRow_UsesConfiguredIndexUrl()
    {
        var effective = await CreateProvider().GetEffectiveOptionsAsync(ConfiguredOptions());

        Assert.AreEqual(ConfiguredUrl, effective.IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_AdminRowOverridesConfiguredIndexUrl()
    {
        await SetAdminUrlAsync(AdminUrl);

        var effective = await CreateProvider().GetEffectiveOptionsAsync(ConfiguredOptions());

        Assert.AreEqual(AdminUrl, effective.IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_BlankAdminRow_FallsBackToConfiguredIndexUrl()
    {
        await SetAdminUrlAsync("   ");

        var effective = await CreateProvider().GetEffectiveOptionsAsync(ConfiguredOptions());

        Assert.AreEqual(ConfiguredUrl, effective.IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_InvalidAdminUrl_FallsBackToConfiguredIndexUrl()
    {
        // Plain http on a non-loopback host is refused by the store client, so it must not be
        // selected as the effective source either.
        await SetAdminUrlAsync("http://store.test/index/v1/index.json");

        var effective = await CreateProvider().GetEffectiveOptionsAsync(ConfiguredOptions());

        Assert.AreEqual(ConfiguredUrl, effective.IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_AdminOverride_DoesNotMutateConfiguredOptions()
    {
        await SetAdminUrlAsync(AdminUrl);
        var configured = ConfiguredOptions();

        var effective = await CreateProvider().GetEffectiveOptionsAsync(configured);

        Assert.AreEqual(AdminUrl, effective.IndexUrl);
        Assert.AreEqual(ConfiguredUrl, configured.IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_WithinCacheDuration_DoesNotReReadTheRow()
    {
        var provider = CreateProvider();
        Assert.AreEqual(ConfiguredUrl, (await provider.GetEffectiveOptionsAsync(ConfiguredOptions())).IndexUrl);

        await SetAdminUrlAsync(AdminUrl);
        _time.Advance(ModuleStoreSettingsProvider.CacheDuration - TimeSpan.FromSeconds(1));

        Assert.AreEqual(ConfiguredUrl, (await provider.GetEffectiveOptionsAsync(ConfiguredOptions())).IndexUrl);
    }

    [TestMethod]
    public async Task GetEffectiveOptionsAsync_AfterCacheDuration_PicksUpTheAdminUrl()
    {
        var provider = CreateProvider();
        Assert.AreEqual(ConfiguredUrl, (await provider.GetEffectiveOptionsAsync(ConfiguredOptions())).IndexUrl);

        await SetAdminUrlAsync(AdminUrl);
        _time.Advance(ModuleStoreSettingsProvider.CacheDuration + TimeSpan.FromSeconds(1));

        Assert.AreEqual(AdminUrl, (await provider.GetEffectiveOptionsAsync(ConfiguredOptions())).IndexUrl);
    }

    [TestMethod]
    public async Task ConfiguredModuleStoreService_AdminOverride_SwitchesSourceWithoutRestart()
    {
        var provider = CreateProvider();
        var service = new ConfiguredModuleStoreService(
            options => new RecordingStore(options.IndexUrl ?? options.LocalFeedPath ?? "local"),
            provider,
            ConfiguredOptions());

        Assert.AreEqual(ConfiguredUrl, service.SourceId);
        await service.ListPackagesAsync();
        Assert.AreEqual(ConfiguredUrl, service.SourceId);

        await SetAdminUrlAsync(AdminUrl);
        _time.Advance(ModuleStoreSettingsProvider.CacheDuration + TimeSpan.FromSeconds(1));

        await service.ListPackagesAsync();

        Assert.AreEqual(AdminUrl, service.SourceId);
        Assert.AreEqual(ModuleStoreSourceKind.RemoteIndex, service.SourceKind);
    }

    [TestMethod]
    public async Task ConfiguredModuleStoreService_SameSource_ReusesTheClient()
    {
        var builds = 0;
        var provider = CreateProvider();
        var service = new ConfiguredModuleStoreService(
            options =>
            {
                builds++;
                return new RecordingStore(options.IndexUrl ?? "local");
            },
            provider,
            ConfiguredOptions());

        await service.ListPackagesAsync();
        _time.Advance(ModuleStoreSettingsProvider.CacheDuration + TimeSpan.FromSeconds(1));
        await service.ResolveAsync("org.example");

        Assert.AreEqual(1, builds, "An unchanged source must not rebuild the store client (index caches stay warm).");
    }

    private sealed class RecordingStore : IModuleStoreService
    {
        private readonly string _id;

        public RecordingStore(string id) => _id = id;

        public string SourceId => _id;

        public ModuleStoreSourceKind SourceKind =>
            _id.StartsWith("https:", StringComparison.Ordinal)
                ? ModuleStoreSourceKind.RemoteIndex
                : ModuleStoreSourceKind.LocalFeed;

        public Task<IReadOnlyList<ModuleStorePackage>> ListPackagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleStorePackage>>([]);

        public Task<ModuleStoreResolution?> ResolveAsync(
            string moduleId,
            string? version = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ModuleStoreResolution?>(null);
    }

    private sealed class TestDbContextFactory : IDbContextFactory
    {
        private readonly DbContextOptions<CoreDbContext> _options;
        private readonly PostgreSqlNamingStrategy _namingStrategy = new();

        public TestDbContextFactory(DbContextOptions<CoreDbContext> options) => _options = options;

        public CoreDbContext CreateDbContext() => new(_options, _namingStrategy);

        public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

        public ITableNamingStrategy NamingStrategy => _namingStrategy;
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
