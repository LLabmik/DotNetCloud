using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Tests.Modules;

/// <summary>
/// Tests for <see cref="ModuleSettingsStore"/>.
/// </summary>
[TestClass]
public class ModuleSettingsStoreTests
{
    private CoreDbContext _context = null!;
    private ModuleSettingsStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: $"ModuleSettingsTestDb_{Guid.CreateVersion7()}")
            .Options;

        _context = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _store = new ModuleSettingsStore(_context);
    }

    [TestCleanup]
    public void Cleanup() => _context?.Dispose();

    [TestMethod]
    public async Task SetAsync_NewKey_ThenGetAsync_ReturnsValue()
    {
        await _store.SetAsync("dotnetcloud.example", "theme", "dark");

        var value = await _store.GetAsync("dotnetcloud.example", "theme");

        Assert.AreEqual("dark", value);
    }

    [TestMethod]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        var value = await _store.GetAsync("dotnetcloud.example", "missing");

        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task SetAsync_ExistingKey_UpdatesInPlace()
    {
        await _store.SetAsync("dotnetcloud.example", "theme", "dark");
        await _store.SetAsync("dotnetcloud.example", "theme", "light");

        var value = await _store.GetAsync("dotnetcloud.example", "theme");
        var rows = await _context.ModuleSettings.CountAsync(s => s.Key == "theme");

        Assert.AreEqual("light", value);
        Assert.AreEqual(1, rows);
    }

    [TestMethod]
    public async Task SetAsync_SameKeyForTwoModules_IsNamespaced()
    {
        await _store.SetAsync("dotnetcloud.one", "theme", "dark");
        await _store.SetAsync("dotnetcloud.two", "theme", "light");

        Assert.AreEqual("dark", await _store.GetAsync("dotnetcloud.one", "theme"));
        Assert.AreEqual("light", await _store.GetAsync("dotnetcloud.two", "theme"));
    }

    [TestMethod]
    public async Task SetAsync_InvalidKey_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SetAsync("dotnetcloud.example", "bad key\nwith newline", "value"));
    }

    [TestMethod]
    public async Task SetAsync_OversizedValue_Throws()
    {
        var oversized = new string('x', ModuleSettingLimits.MaxValueLength + 1);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => _store.SetAsync("dotnetcloud.example", "blob", oversized));
    }

    [TestMethod]
    public async Task SetAsync_RecordsActingUserAndSecretFlag()
    {
        var userId = Guid.CreateVersion7();

        await _store.SetAsync("dotnetcloud.example", "token", "abc", isSecret: true, updatedByUserId: userId);

        var row = await _context.ModuleSettings.SingleAsync(s => s.Key == "token");
        Assert.IsTrue(row.IsSecret);
        Assert.AreEqual(userId, row.UpdatedByUserId);
    }
}
