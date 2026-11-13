using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for <see cref="ModuleSettingDefaultsSeeder"/>: defaults declared by a manifest land in the
/// module settings plane exactly once, and an administrator's value is never overwritten.
/// </summary>
[TestClass]
public class ModuleSettingDefaultsSeederTests
{
    private const string ModuleId = "org.example.customapp";

    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase($"ModuleSettingDefaultsSeederTests_{Guid.NewGuid():N}"));
        services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup() => _provider.Dispose();

    private CoreDbContext CreateDb() =>
        _provider.CreateScope().ServiceProvider.GetRequiredService<CoreDbContext>();

    private static List<ManifestSettingDefault> Defaults() =>
    [
        new() { Key = "ScanEnabled", Value = "true" },
        new() { Key = "ScanIntervalMinutes", Value = "30" },
    ];

    [TestMethod]
    public async Task SeedAsync_EmptyStore_WritesEveryDefault()
    {
        await using var db = CreateDb();

        var written = await ModuleSettingDefaultsSeeder.SeedAsync(db, ModuleId, Defaults());

        Assert.AreEqual(2, written);
        var stored = await db.ModuleSettings
            .AsNoTracking()
            .Where(setting => setting.ModuleId == ModuleId)
            .OrderBy(setting => setting.Key)
            .ToListAsync();

        Assert.AreEqual(2, stored.Count);
        Assert.AreEqual("ScanEnabled", stored[0].Key);
        Assert.AreEqual("true", stored[0].Value);
        Assert.IsFalse(stored[0].IsSecret);
    }

    [TestMethod]
    public async Task SeedAsync_ExistingValue_IsNotOverwritten()
    {
        await using var db = CreateDb();

        db.ModuleSettings.Add(new Core.Data.Entities.Modules.ModuleSetting
        {
            ModuleId = ModuleId,
            Key = "ScanEnabled",
            Value = "false",
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var written = await ModuleSettingDefaultsSeeder.SeedAsync(db, ModuleId, Defaults());

        // Only the key that had no value is written.
        Assert.AreEqual(1, written);

        var scanEnabled = await db.ModuleSettings
            .AsNoTracking()
            .SingleAsync(setting => setting.ModuleId == ModuleId && setting.Key == "ScanEnabled");

        Assert.AreEqual("false", scanEnabled.Value);
    }

    [TestMethod]
    public async Task SeedAsync_CalledTwice_IsIdempotent()
    {
        await using var db = CreateDb();

        await ModuleSettingDefaultsSeeder.SeedAsync(db, ModuleId, Defaults());
        var written = await ModuleSettingDefaultsSeeder.SeedAsync(db, ModuleId, Defaults());

        Assert.AreEqual(0, written);

        var count = await db.ModuleSettings
            .AsNoTracking()
            .CountAsync(setting => setting.ModuleId == ModuleId);

        Assert.AreEqual(2, count);
    }
}
